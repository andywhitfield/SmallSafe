using System.Security.Claims;
using Dropbox.Api;
using Dropbox.Api.Files;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SmallSafe.Secure;
using SmallSafe.Secure.Model;
using SmallSafe.Secure.Services;
using SmallSafe.Web.Data;
using SmallSafe.Web.Data.Models;

namespace SmallSafe.Web.Services;

public class UserService(
    ILogger<UserService> logger,
    IOptionsSnapshot<DropboxConfig> dropboxConfig,
    SqliteDataContext dbContext,
    IEncryptDecrypt encryptDecrypt,
    ISafeDbService safeDbService)
    : IUserService
{
    public async Task<bool> IsNewUserAsync(ClaimsPrincipal user)
    {
        var email = GetEmailFromPrincipal(user);
        var userAccount = await dbContext.UserAccounts!.FirstOrDefaultAsync(ua => ua.Email == email && ua.DeletedDateTime == null);
        return userAccount == null || !userAccount.IsAccountConfigured;
    }

    public async Task<UserAccount> GetUserAsync(ClaimsPrincipal user)
    {
        var email = GetEmailFromPrincipal(user) ?? throw new ArgumentException("Could not get email from user principal", nameof(user));
        return await GetUserByEmailAsync(email) ?? throw new ArgumentException($"No active user account found for authentication id {email}", nameof(user));
    }

    public Task<UserAccount?> GetUserByEmailAsync(string email)
        => dbContext.UserAccounts!.FirstOrDefaultAsync(a => a.DeletedDateTime == null && a.Email == email);

    public async Task<UserAccount> CreateUserAsync(string email, byte[] credentialId, byte[] publicKey, byte[] userHandle)
    {
        var newUser = dbContext.UserAccounts!.Add(new() 
        {
            CreatedDateTime = DateTime.UtcNow,
            Email = email,
            TwoFactorKey = Guid.NewGuid().ToString()
        });
        dbContext.UserAccountCredentials!.Add(new()
        {
            UserAccount = newUser.Entity,
            CredentialId = credentialId,
            PublicKey = publicKey,
            UserHandle = userHandle
        });
        await dbContext.SaveChangesAsync();
        return newUser.Entity;
    }

    public async Task UpdateUserDbAsync(UserAccount user, byte[] safeDb)
    {
        user.EncyptedSafeDb = safeDb;
        user.LastUpdateDateTime = DateTime.UtcNow;

        if (user.IsConnectedToDropbox)
            await CopyToDropboxAsync(user);
        await dbContext.SaveChangesAsync();
    }

    public Task UpdateUserDropboxAsync(UserAccount user, string? dropboxAccessToken, string? dropboxRefreshToken)
    {
        user.DropboxAccessToken = dropboxAccessToken;
        user.DropboxRefreshToken = dropboxRefreshToken;
        user.LastUpdateDateTime = DateTime.UtcNow;
        return dbContext.SaveChangesAsync();
    }

    public Task LoginSuccessAsync(UserAccount user)
    {
        user.LastTwoFactorSuccess = DateTime.UtcNow;
        user.TwoFactorFailureCount = 0;
        return dbContext.SaveChangesAsync();
    }

    public Task LoginFailureAsync(UserAccount user)
    {
        user.LastTwoFactorFailure = DateTime.UtcNow;
        user.TwoFactorFailureCount++;
        return dbContext.SaveChangesAsync();
    }

    public IAsyncEnumerable<UserAccountCredential> GetUserCredentialsAsync(UserAccount user)
        => dbContext.UserAccountCredentials!.Where(uac => uac.DeletedDateTime == null && uac.UserAccountId == user.UserAccountId).AsAsyncEnumerable();

    public Task<UserAccountCredential?> GetUserCredentialByUserHandleAsync(byte[] userHandle)
        => dbContext.UserAccountCredentials!.FirstOrDefaultAsync(uac => uac.DeletedDateTime == null && uac.UserHandle.SequenceEqual(userHandle));

    public Task SetSignatureCountAsync(UserAccountCredential userAccountCredential, uint signatureCount)
    {
        userAccountCredential.SignatureCount = signatureCount;
        return dbContext.SaveChangesAsync();
    }

    private static string? GetEmailFromPrincipal(ClaimsPrincipal user) => user?.FindFirstValue(ClaimTypes.Name);

    private async Task CopyToDropboxAsync(UserAccount user)
    {
        logger.LogInformation("Saving SafeDB file to Dropbox for user {UserAccountId}", user.UserAccountId);

        using MemoryStream contentStream = new(user.EncyptedSafeDb ?? []);
        using DropboxClient dropboxClient = new(user.DropboxAccessToken, user.DropboxRefreshToken,
            dropboxConfig.Value.SmallSafeAppKey, dropboxConfig.Value.SmallSafeAppSecret, new DropboxClientConfig());
        if (!await dropboxClient.RefreshAccessToken(["files.content.write"]))
        {
            logger.LogError("Could not refresh Dropbox access token for user {UserAccountId}", user.UserAccountId);
            return;
        }

        var file = await dropboxClient.Files.UploadAsync(
            $"/{dropboxConfig.Value.Filename ?? "smallsafe.db.json"}",
            WriteMode.Overwrite.Instance,
            body: contentStream);
        logger.LogTrace("Saved to dropbox {FilePathDisplay}/{FileName} rev {FileRev}", file.PathDisplay, file.Name, file.Rev);
    }

    public async Task MigrateSafeDbIfNeededAsync(ClaimsPrincipal user, string masterPassword)
    {
        var email = GetEmailFromPrincipal(user);
        var userAccount = await dbContext.UserAccounts!.FirstOrDefaultAsync(ua => ua.Email == email && ua.DeletedDateTime == null);
        if (userAccount != null && !string.IsNullOrEmpty(userAccount.TwoFactorKey) && userAccount.SafeDb != null)
        {
            logger.LogInformation("Migrating SafeDB for user {UserAccountId} with email {Email}", userAccount.UserAccountId, userAccount.Email);
            var legacySafeDb = System.Text.Json.JsonSerializer.Deserialize<LegacySafeDb>(userAccount.SafeDb);
            if (legacySafeDb != null && legacySafeDb.IV != null && legacySafeDb.Salt != null && legacySafeDb.EncryptedSafeGroups != null)
            {
                logger.LogDebug("Deserialized legacy safedb");
                List<SafeGroup> newGroups = [];
                var legacySerializedGroups = await encryptDecrypt.DecryptAsync(masterPassword, legacySafeDb.IV, legacySafeDb.Salt, Convert.FromBase64String(legacySafeDb.EncryptedSafeGroups));
                var legacyGroups = System.Text.Json.JsonSerializer.Deserialize<IEnumerable<LegacySafeGroup>>(legacySerializedGroups);
                foreach (var legacyGroup in legacyGroups ?? [])
                {
                    logger.LogDebug("Migrating group {GroupName}", legacyGroup.Name);
                    SafeGroup newGroup = new()
                    {
                        Id = legacyGroup.Id,
                        Name = legacyGroup.Name,
                        Entries = []
                    };
                    foreach (var legacyEntry in legacyGroup.Entries ?? [])
                    {
                        if (legacyEntry.IV == null || legacyEntry.Salt == null || legacyEntry.EncryptedValue == null)
                        {
                            logger.LogWarning("Legacy entry {EntryName} in group {GroupName} is missing encryption parameters, skipping entry", legacyEntry.Name, legacyGroup.Name);
                            continue;
                        }

                        logger.LogDebug("Group {LegacyGroupName}, migrating entry {EntryName}", legacyGroup.Name, legacyEntry.Name);
                        newGroup.Entries.Add(new SafeEntry()
                        {
                            Id = legacyEntry.Id,
                            Name = legacyEntry.Name,
                            EntryValue = await encryptDecrypt.DecryptAsync(masterPassword, legacyEntry.IV, legacyEntry.Salt, Convert.FromBase64String(legacyEntry.EncryptedValue))
                        });
                    }
                    newGroups.Add(newGroup);
                }

                await using MemoryStream stream = new();
                await safeDbService.WriteAsync(masterPassword, newGroups, stream);

                userAccount.SafeDb = null;
                userAccount.EncyptedSafeDb = stream.ToArray();
                await dbContext.SaveChangesAsync();
            }
            else
            {
                logger.LogWarning("User {UserAccountId} has a safedb that cannot be migrated, skipping migration", userAccount.UserAccountId);
            }
        }
    }

    class LegacySafeDb
    {
        public byte[]? IV { get; set; }
        public byte[]? Salt { get; set; }
        public string? EncryptedSafeGroups { get; set; }
    }

    class LegacySafeGroup
    {
        public Guid Id { get; set; }
        public string? Name { get; set; }
        public List<LegacySafeEntry>? Entries { get; set; }
    }

    class LegacySafeEntry
    {
        public Guid Id { get; set; }
        public string? Name { get; set; }
        public string? EncryptedValue { get; set; }
        public byte[]? IV { get; set; }
        public byte[]? Salt { get; set; }
    }
}

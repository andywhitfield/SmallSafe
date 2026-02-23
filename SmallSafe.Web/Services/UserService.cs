using System.Security.Claims;
using Dropbox.Api;
using Dropbox.Api.Files;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SmallSafe.Web.Data;
using SmallSafe.Web.Data.Models;

namespace SmallSafe.Web.Services;

public class UserService(
    ILogger<UserService> logger,
    IOptionsSnapshot<DropboxConfig> dropboxConfig,
    SqliteDataContext dbContext)
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
}

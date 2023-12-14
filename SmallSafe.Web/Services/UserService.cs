using System.Security.Claims;
using System.Text;
using Dropbox.Api;
using Dropbox.Api.Files;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SmallSafe.Web.Data;
using SmallSafe.Web.Data.Models;

namespace SmallSafe.Web.Services;

public class UserService(ILogger<UserService> logger, IOptionsSnapshot<DropboxConfig> dropboxConfig,
    SqliteDataContext dbContext) : IUserService
{
    public async Task<bool> IsNewUserAsync(ClaimsPrincipal user)
    {
        var authenticationUri = GetIdentifierFromPrincipal(user);
        var userAccount = await dbContext.UserAccounts!.FirstOrDefaultAsync(ua => ua.AuthenticationUri == authenticationUri && ua.DeletedDateTime == null);
        return userAccount == null || !userAccount.IsAccountConfigured;
    }

    public async Task<UserAccount> GetUserAsync(ClaimsPrincipal user)
    {
        var authenticationUri = GetIdentifierFromPrincipal(user) ?? throw new ArgumentException("Could not get identifier from user principal", nameof(user));
        var userAccount = await dbContext.UserAccounts!.FirstOrDefaultAsync(ua => ua.AuthenticationUri == authenticationUri && ua.DeletedDateTime == null);
        return userAccount ?? throw new ArgumentException($"No active user account found for authentication id {authenticationUri}", nameof(user));
    }

    public async Task<UserAccount> GetOrCreateUserAsync(ClaimsPrincipal user)
    {
        var authenticationUri = GetIdentifierFromPrincipal(user) ?? throw new ArgumentException("Could not get identifier from user principal", nameof(user));
        var userAccount = await dbContext.UserAccounts!.FirstOrDefaultAsync(ua => ua.AuthenticationUri == authenticationUri && ua.DeletedDateTime == null);
        return userAccount ?? await CreateUserAsync(authenticationUri);
    }

    public async Task UpdateUserDbAsync(UserAccount user, string safeDb)
    {
        user.SafeDb = safeDb;
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

    private async Task<UserAccount> CreateUserAsync(string authenticationUri)
    {
        var newUser = dbContext.UserAccounts!.Add(new() 
        {
            CreatedDateTime = DateTime.UtcNow,
            AuthenticationUri = authenticationUri,
            TwoFactorKey = Guid.NewGuid().ToString()
        });
        await dbContext.SaveChangesAsync();
        return newUser.Entity;
    }

    private static string? GetIdentifierFromPrincipal(ClaimsPrincipal user) => user?.FindFirstValue("name");

    private async Task CopyToDropboxAsync(UserAccount user)
    {
        logger.LogInformation($"Saving SafeDB file to Dropbox for user {user.UserAccountId}");

        using MemoryStream contentStream = new(Encoding.ASCII.GetBytes(user.SafeDb ?? ""));
        using DropboxClient dropboxClient = new(user.DropboxAccessToken, user.DropboxRefreshToken,
            dropboxConfig.Value.SmallSafeAppKey, dropboxConfig.Value.SmallSafeAppSecret, new DropboxClientConfig());
        if (!await dropboxClient.RefreshAccessToken(["files.content.write"]))
        {
            logger.LogError($"Could not refresh Dropbox access token for user {user.UserAccountId}");
            return;
        }

        var file = await dropboxClient.Files.UploadAsync(
            $"/{dropboxConfig.Value.Filename ?? "smallsafe.db.json"}",
            WriteMode.Overwrite.Instance,
            body: contentStream);
        logger.LogTrace($"Saved to dropbox {file.PathDisplay}/{file.Name} rev {file.Rev}");
    }
}

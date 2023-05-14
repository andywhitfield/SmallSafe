using System.Security.Claims;
using System.Text;
using Dropbox.Api;
using Dropbox.Api.Files;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SmallSafe.Web.Data;
using SmallSafe.Web.Data.Models;

namespace SmallSafe.Web.Services;

public class UserService : IUserService
{
    private readonly ILogger<UserService> _logger;
    private readonly IOptionsSnapshot<DropboxConfig> _dropboxConfig;
    private readonly SqliteDataContext _dbContext;

    public UserService(ILogger<UserService> logger, IOptionsSnapshot<DropboxConfig> dropboxConfig, SqliteDataContext dbContext)
    {
        _logger = logger;
        _dropboxConfig = dropboxConfig;
        _dbContext = dbContext;
    }

    public async Task<bool> IsNewUserAsync(ClaimsPrincipal user)
    {
        var authenticationUri = GetIdentifierFromPrincipal(user);
        var userAccount = await _dbContext.UserAccounts!.FirstOrDefaultAsync(ua => ua.AuthenticationUri == authenticationUri && ua.DeletedDateTime == null);
        return userAccount == null || !userAccount.IsAccountConfigured;
    }

    public async Task<UserAccount> GetUserAsync(ClaimsPrincipal user)
    {
        var authenticationUri = GetIdentifierFromPrincipal(user) ?? throw new ArgumentException("Could not get identifier from user principal", nameof(user));
        var userAccount = await _dbContext.UserAccounts!.FirstOrDefaultAsync(ua => ua.AuthenticationUri == authenticationUri && ua.DeletedDateTime == null);
        return userAccount ?? throw new ArgumentException($"No active user account found for authentication id {authenticationUri}", nameof(user));
    }

    public async Task<UserAccount> GetOrCreateUserAsync(ClaimsPrincipal user)
    {
        var authenticationUri = GetIdentifierFromPrincipal(user) ?? throw new ArgumentException("Could not get identifier from user principal", nameof(user));
        var userAccount = await _dbContext.UserAccounts!.FirstOrDefaultAsync(ua => ua.AuthenticationUri == authenticationUri && ua.DeletedDateTime == null);
        return userAccount ?? await CreateUserAsync(authenticationUri);
    }

    public async Task UpdateUserDbAsync(UserAccount user, string safeDb)
    {
        user.SafeDb = safeDb;
        user.LastUpdateDateTime = DateTime.UtcNow;

        if (user.IsConnectedToDropbox)
            await CopyToDropboxAsync(user);
        await _dbContext.SaveChangesAsync();
    }

    public Task UpdateUserDropboxAsync(UserAccount user, string? dropboxAccessToken, string? dropboxRefreshToken)
    {
        user.DropboxAccessToken = dropboxAccessToken;
        user.DropboxRefreshToken = dropboxRefreshToken;
        user.LastUpdateDateTime = DateTime.UtcNow;
        return _dbContext.SaveChangesAsync();
    }

    public Task LoginSuccessAsync(UserAccount user)
    {
        user.LastTwoFactorSuccess = DateTime.UtcNow;
        user.TwoFactorFailureCount = 0;
        return _dbContext.SaveChangesAsync();
    }

    public Task LoginFailureAsync(UserAccount user)
    {
        user.LastTwoFactorFailure = DateTime.UtcNow;
        user.TwoFactorFailureCount++;
        return _dbContext.SaveChangesAsync();
    }

    private async Task<UserAccount> CreateUserAsync(string authenticationUri)
    {
        var newUser = _dbContext.UserAccounts!.Add(new() 
        {
            CreatedDateTime = DateTime.UtcNow,
            AuthenticationUri = authenticationUri,
            TwoFactorKey = Guid.NewGuid().ToString()
        });
        await _dbContext.SaveChangesAsync();
        return newUser.Entity;
    }

    private string? GetIdentifierFromPrincipal(ClaimsPrincipal user) => user?.FindFirstValue("sub");

    private async Task CopyToDropboxAsync(UserAccount user)
    {
        _logger.LogInformation($"Saving SafeDB file to Dropbox for user {user.UserAccountId}");

        using var contentStream = new MemoryStream(Encoding.ASCII.GetBytes(user.SafeDb ?? ""));
        using var dropboxClient = new DropboxClient(user.DropboxAccessToken, user.DropboxRefreshToken,
            _dropboxConfig.Value.SmallSafeAppKey, _dropboxConfig.Value.SmallSafeAppSecret, new DropboxClientConfig());
        if (!await dropboxClient.RefreshAccessToken(new[] { "files.content.write" }))
        {
            _logger.LogError($"Could not refresh Dropbox access token for user {user.UserAccountId}");
            return;
        }

        var file = await dropboxClient.Files.UploadAsync(
            $"/{_dropboxConfig.Value.Filename ?? "smallsafe.db.json"}",
            WriteMode.Overwrite.Instance,
            body: contentStream);
        _logger.LogTrace($"Saved to dropbox {file.PathDisplay}/{file.Name} rev {file.Rev}");
    }
}

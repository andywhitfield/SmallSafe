using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SmallSafe.Web.Data;
using SmallSafe.Web.Data.Models;

namespace SmallSafe.Web.Services;

public class UserService : IUserService
{
    private readonly SqliteDataContext _dbContext;

    public UserService(SqliteDataContext dbContext) => _dbContext = dbContext;

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

    public Task UpdateUserDbAsync(UserAccount user, string safeDb)
    {
        user.SafeDb = safeDb;
        user.LastUpdateDateTime = DateTime.UtcNow;
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
}
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SmallSafe.Web.Data;

namespace SmallSafe.Web.Services;

public class UserService : IUserService
{
    private readonly SqliteDataContext _dbContext;

    public UserService(SqliteDataContext dbContext) => _dbContext = dbContext;
    public async Task<bool> IsNewUserAsync(ClaimsPrincipal user)
    {
        var userAccount = await _dbContext.UserAccounts!.FirstOrDefaultAsync();
        return userAccount == null; // TODO: check for master password / 2FA setup
    }
}
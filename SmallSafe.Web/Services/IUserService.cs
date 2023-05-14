using System.Security.Claims;
using SmallSafe.Web.Data.Models;

namespace SmallSafe.Web.Services;

public interface IUserService
{
    Task<bool> IsNewUserAsync(ClaimsPrincipal user);
    Task<UserAccount> GetUserAsync(ClaimsPrincipal user);
    Task<UserAccount> GetOrCreateUserAsync(ClaimsPrincipal user);
    Task UpdateUserDbAsync(UserAccount user, string safeDb);
    Task UpdateUserDropboxAsync(UserAccount user, string? dropboxAccessToken, string? dropboxRefreshToken);
    Task LoginSuccessAsync(UserAccount user);
    Task LoginFailureAsync(UserAccount user);
}
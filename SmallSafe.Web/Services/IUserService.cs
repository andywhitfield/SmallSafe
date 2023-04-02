using System.Security.Claims;

namespace SmallSafe.Web.Services;

public interface IUserService
{
    Task<bool> IsNewUserAsync(ClaimsPrincipal user);
}
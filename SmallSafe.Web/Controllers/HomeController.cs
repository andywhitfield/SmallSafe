using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmallSafe.Web.Authorization;
using SmallSafe.Web.Services;
using SmallSafe.Web.ViewModels.Home;

namespace SmallSafe.Web.Controllers;

public class HomeController(
    ILogger<HomeController> logger,
    IAuthorizationService authorizationService,
    IUserService userService,
    ISafeDbReadWriteService safeDbReadWriteService,
    IAuthorizationSession authorizationSession)
    : Controller
{
    [Authorize]
    [HttpGet("~/")]
    public async Task<IActionResult> Index()
    {
        if (User == null)
        {
            logger.LogInformation("No user, redirecting to login page");
            return Redirect("~/signin");
        }

        // temporary logic to migrate the user db
        if (authorizationSession.IsValidated)
            await userService.MigrateSafeDbIfNeededAsync(User, authorizationSession.MasterPassword);

        if (await userService.IsNewUserAsync(User))
        {
            logger.LogInformation("User is new, redirecting to new user setup page");
            return Redirect("~/newuser");
        }

        if (!(await authorizationService.AuthorizeAsync(User, TwoFactorRequirement.PolicyName)).Succeeded)
        {
            logger.LogInformation("User hasn't entered their 2fa or password, redirecting to 2fa page");
            return Redirect("~/twofactor");
        }

        var user = await userService.GetUserAsync(User);
        var groups = await safeDbReadWriteService.ReadGroupsAsync(user, authorizationSession.MasterPassword);
        logger.LogDebug("Got groups for user: [{Groups}]", string.Join(',', groups.Where(g => g.DeletedTimestamp == null).Select(g => g.Name)));
        return View(new IndexViewModel(HttpContext, groups.Where(g => g.DeletedTimestamp == null)));
    }

    public IActionResult Error() => View(new ErrorViewModel(HttpContext));
}
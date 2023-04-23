using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmallSafe.Web.Authorization;
using SmallSafe.Web.Services;
using SmallSafe.Web.ViewModels.Home;

namespace SmallSafe.Web.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly IAuthorizationService _authorizationService;
    private readonly IUserService _userService;
    private readonly ISafeDbReadWriteService _safeDbReadWriteService;
    private readonly IAuthorizationSession _authorizationSession;

    public HomeController(ILogger<HomeController> logger, IAuthorizationService authorizationService,
        IUserService userService, ISafeDbReadWriteService safeDbReadWriteService,
        IAuthorizationSession authorizationSession)
    {
        _logger = logger;
        _authorizationService = authorizationService;
        _userService = userService;
        _safeDbReadWriteService = safeDbReadWriteService;
        _authorizationSession = authorizationSession;
    }

    [Authorize]
    [HttpGet("~/")]
    public async Task<IActionResult> Index()
    {
        if (User == null)
        {
            _logger.LogInformation("No user, redirecting to login page");
            return Redirect("~/signin");
        }

        if (await _userService.IsNewUserAsync(User))
        {
            _logger.LogInformation("User is new, redirecting to new user setup page");
            return Redirect("~/newuser");
        }

        if (!(await _authorizationService.AuthorizeAsync(User, TwoFactorRequirement.PolicyName)).Succeeded)
        {
            _logger.LogInformation("User hasn't entered their 2fa or password, redirecting to 2fa page");
            return Redirect("~/twofactor");
        }

        var user = await _userService.GetUserAsync(User);
        var groups = await _safeDbReadWriteService.ReadGroupsAsync(user, _authorizationSession.MasterPassword);
        if (groups == null)
        {
            _logger.LogInformation("Invalid groups for user, logging out");
            // TODO
            return Redirect("~/signin");
        }

        _logger.LogDebug($"Got groups for user: [{string.Join(',', groups.Select(g => g.Name))}]");
        return View(new IndexViewModel(HttpContext));
    }

    public IActionResult Error() => View(new ErrorViewModel(HttpContext));
}
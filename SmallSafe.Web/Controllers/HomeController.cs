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

    public HomeController(ILogger<HomeController> logger, IAuthorizationService authorizationService,
        IUserService userService)
    {
        _logger = logger;
        _authorizationService = authorizationService;
        _userService = userService;
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

        return View(new IndexViewModel(HttpContext));
    }

    public IActionResult Error() => View(new ErrorViewModel(HttpContext));
}
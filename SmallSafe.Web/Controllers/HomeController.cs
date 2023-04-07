using System.Text;
using System.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmallSafe.Secure.Model;
using SmallSafe.Secure.Services;
using SmallSafe.Web.Services;
using SmallSafe.Web.ViewModels.Home;

namespace SmallSafe.Web.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly IUserService _userService;
    private readonly ITwoFactor _twoFactor;
    private readonly ISafeDbService _safeDbService;

    public HomeController(ILogger<HomeController> logger, IUserService userService,
        ITwoFactor twoFactor, ISafeDbService safeDbService)
    {
        _logger = logger;
        _userService = userService;
        _twoFactor = twoFactor;
        _safeDbService = safeDbService;
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
        
        // TODO: need to have some validation to ensure user has completed 2fa login

        return View(new IndexViewModel(HttpContext));
    }

    public IActionResult Error() => View(new ErrorViewModel(HttpContext));

    [HttpGet("~/signin")]
    public IActionResult Signin([FromQuery] string returnUrl) => View("Login", new LoginViewModel(HttpContext, returnUrl));

    [HttpPost("~/signin")]
    [ValidateAntiForgeryToken]
    public IActionResult SigninChallenge([FromForm] string returnUrl) => Challenge(new AuthenticationProperties { RedirectUri = $"/signedin?returnUrl={HttpUtility.UrlEncode(returnUrl)}" }, OpenIdConnectDefaults.AuthenticationScheme);

    [Authorize]
    [HttpGet("~/signedin")]
    public IActionResult SignedIn([FromQuery] string returnUrl) => Redirect(
        !string.IsNullOrEmpty(returnUrl) && Uri.TryCreate(returnUrl, UriKind.Relative, out var redirectUri)
            ? redirectUri.ToString()
            : "~/");

    [HttpPost("~/signout")]
    [ValidateAntiForgeryToken]
    public IActionResult Signout()
    {
        HttpContext.Session.Clear();
        return SignOut(CookieAuthenticationDefaults.AuthenticationScheme, OpenIdConnectDefaults.AuthenticationScheme);
    }

    [Authorize]
    [HttpGet("~/newuser")]
    public async Task<IActionResult> NewUser()
    {
        var user = await _userService.GetOrCreateUserAsync(User);
        var (qrCode, manualKey) = _twoFactor.GenerateSetupCodeForUser(user);
        return View(new NewUserViewModel(HttpContext, qrCode, manualKey));
    }

    [Authorize]
    [HttpPost("~/newuser")]
    public async Task<IActionResult> NewUser([FromForm] string masterpassword, [FromForm] string twofa)
    {
        var user = await _userService.GetUserAsync(User);
        if (!string.IsNullOrEmpty(masterpassword) && _twoFactor.ValidateTwoFactorCodeForUser(user, twofa))
        {
            _logger.LogInformation("Successfully created new safedb & validated 2fa code");

            using MemoryStream stream = new();
            await _safeDbService.WriteAsync(masterpassword, Array.Empty<SafeGroup>(), stream);
            await _userService.UpdateUserDbAsync(user, Encoding.UTF8.GetString(stream.ToArray()));
            return Redirect("~/");
        }

        _logger.LogWarning("Missing master password or invalid 2fa code, returning to new user setup page");
        var (qrCode, manualKey) = _twoFactor.GenerateSetupCodeForUser(user);
        return View(new NewUserViewModel(HttpContext, qrCode, manualKey));
    }
}
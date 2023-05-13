using System.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmallSafe.Secure.Model;
using SmallSafe.Web.Authorization;
using SmallSafe.Web.Services;
using SmallSafe.Web.ViewModels.Login;

namespace SmallSafe.Web.Controllers;

public class LoginController : Controller
{
    private readonly ILogger<LoginController> _logger;
    private readonly IAuthorizationService _authorizationService;
    private readonly IAuthorizationSession _authorizationSession;
    private readonly IUserService _userService;
    private readonly ITwoFactor _twoFactor;
    private readonly ISafeDbReadWriteService _safeDbReadWriteService;

    public LoginController(ILogger<LoginController> logger, IAuthorizationService authorizationService,
        IAuthorizationSession authorizationSession, IUserService userService, ITwoFactor twoFactor,
        ISafeDbReadWriteService safeDbReadWriteService)
    {
        _logger = logger;
        _authorizationService = authorizationService;
        _authorizationSession = authorizationSession;
        _userService = userService;
        _twoFactor = twoFactor;
        _safeDbReadWriteService = safeDbReadWriteService;
    }

    [HttpGet("~/signin")]
    public IActionResult Signin([FromQuery] string returnUrl) => View("Login", new LoginViewModel(HttpContext, returnUrl));

    [HttpPost("~/signin")]
    [ValidateAntiForgeryToken]
    public IActionResult SigninChallenge([FromForm] string returnUrl) => Challenge(new AuthenticationProperties { RedirectUri = $"/signedin?returnUrl={HttpUtility.UrlEncode(returnUrl)}" }, OpenIdConnectDefaults.AuthenticationScheme);

    [Authorize]
    [HttpGet("~/signedin")]
    public IActionResult SignedIn([FromQuery] string returnUrl) => RedirectTo(returnUrl);

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
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> NewUser([FromForm] string? masterpassword, [FromForm] string? twofa)
    {
        var user = await _userService.GetUserAsync(User);
        if (!string.IsNullOrEmpty(masterpassword) && _twoFactor.ValidateTwoFactorCodeForUser(user, twofa))
        {
            _logger.LogInformation("Successfully created new safedb & validated 2fa code");

            await _safeDbReadWriteService.WriteGroupsAsync(user, masterpassword, Array.Empty<SafeGroup>());
            await _userService.LoginSuccessAsync(user);
            _authorizationSession.Validate(masterpassword);

            return Redirect("~/");
        }

        await _userService.LoginFailureAsync(user);
        _logger.LogWarning("Missing master password or invalid 2fa code, returning to new user setup page");
        var (qrCode, manualKey) = _twoFactor.GenerateSetupCodeForUser(user);
        return View(new NewUserViewModel(HttpContext, qrCode, manualKey));
    }

    [Authorize]
    [HttpGet("~/twofactor")]
    public IActionResult TwoFactorMissing([FromQuery] string? returnUrl) =>
        View("TwoFactorLogin", new TwoFactorLoginViewModel(HttpContext, returnUrl));

    [Authorize]
    [HttpPost("~/twofactor")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TwoFactorLogin([FromForm] string? masterpassword, [FromForm] string? twofa, [FromForm] string? returnUrl)
    {
        var user = await _userService.GetUserAsync(User);
        if (!string.IsNullOrEmpty(masterpassword) &&
            await _safeDbReadWriteService.TryReadGroupsAsync(user, masterpassword) != null &&
            _twoFactor.ValidateTwoFactorCodeForUser(user, twofa))
        {
            _logger.LogInformation("Successfully logged in & validated 2fa code");
            await _userService.LoginSuccessAsync(user);
            _authorizationSession.Validate(masterpassword);

            return RedirectTo(returnUrl);
        }

        await _userService.LoginFailureAsync(user);
        _logger.LogWarning("Bad master password or invalid 2fa code, returning to login page");
        return View("TwoFactorLogin", new TwoFactorLoginViewModel(HttpContext, returnUrl, true));
    }

    private RedirectResult RedirectTo(string? returnUrl) =>
        Redirect(!string.IsNullOrEmpty(returnUrl) && Uri.TryCreate(returnUrl, UriKind.Relative, out var redirectUri)
            ? redirectUri.ToString()
            : "~/");
}
using System.Web;
using Google.Authenticator;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmallSafe.Web.Services;
using SmallSafe.Web.ViewModels.Home;

namespace SmallSafe.Web.Controllers;

public class HomeController : Controller
{
    private readonly IUserService _userService;

    public HomeController(IUserService userService) => _userService = userService;

    [Authorize]
    [HttpGet("~/")]
    public async Task<IActionResult> Index()
    {
        if (User == null)
            return Redirect("~/signin");
        
        if (await _userService.IsNewUserAsync(User))
            return Redirect("~/newuser");

        string key = "abcdefghij";
        TwoFactorAuthenticator tfa = new();
        var setupInfo = tfa.GenerateSetupCode("Test Two Factor", "user@example.com", key, false, 3);
        var qrCodeImageUrl = setupInfo.QrCodeSetupImageUrl;
        var manualEntrySetupCode = setupInfo.ManualEntryKey;

        return View(new IndexViewModel(HttpContext, qrCodeImageUrl, manualEntrySetupCode, null));
    }

    [Authorize]
    [HttpPost("~/validate")]
    public IActionResult Validate([FromForm]string totp)
    {
        string key = "abcdefghij";
        TwoFactorAuthenticator tfa = new();
        var setupInfo = tfa.GenerateSetupCode("Test Two Factor", "user@example.com", key, false, 3);
        var qrCodeImageUrl = setupInfo.QrCodeSetupImageUrl;
        var manualEntrySetupCode = setupInfo.ManualEntryKey;
        var result = tfa.ValidateTwoFactorPIN(key, totp);

        return View("Index", new IndexViewModel(HttpContext, qrCodeImageUrl, manualEntrySetupCode, result));
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
    public IActionResult NewUser() => View(new NewUserViewModel(HttpContext));
}
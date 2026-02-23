using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmallSafe.Secure.Model;
using SmallSafe.Web.Authorization;
using SmallSafe.Web.Services;
using SmallSafe.Web.ViewModels.Login;

namespace SmallSafe.Web.Controllers;

public class LoginController(ILogger<LoginController> logger,
    IAuthorizationSession authorizationSession, IUserService userService, ITwoFactor twoFactor,
    ISafeDbReadWriteService safeDbReadWriteService)
    : Controller
{
    [HttpGet("~/signin")]
    public IActionResult Signin([FromQuery] string? returnUrl) => View("Login", new LoginViewModel(HttpContext, returnUrl));

    [HttpPost("~/signin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Signin([FromForm] string? returnUrl, [FromForm, Required] string email, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return View("Login", new LoginViewModel(HttpContext, returnUrl));

        var (isReturningUser, verifyOptions) = await authorizationSession.HandleSigninRequest(email, cancellationToken);
        return View("LoginVerify", new LoginVerifyViewModel(HttpContext, returnUrl, email, isReturningUser, verifyOptions));
    }

    [HttpPost("~/signin/verify")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SigninVerify(
        [FromForm] string? returnUrl,
        [FromForm, Required] string email,
        [FromForm, Required] string verifyOptions,
        [FromForm, Required] string verifyResponse,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return Redirect("~/signin");

        var isValid = await authorizationSession.HandleSigninVerifyRequest(HttpContext, email, verifyOptions, verifyResponse, cancellationToken);
        if (isValid)
        {
            var redirectUri = "~/";
            if (!string.IsNullOrEmpty(returnUrl) && Uri.TryCreate(returnUrl, UriKind.Relative, out var uri))
                redirectUri = uri.ToString();

            return Redirect(redirectUri);
        }
        
        return Redirect("~/signin");
    }

    [HttpPost("~/signout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Signout()
    {
        HttpContext.Session.Clear();
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Redirect("~/");
    }

    [Authorize]
    [HttpGet("~/newuser")]
    public async Task<IActionResult> NewUser()
    {
        var user = await userService.GetUserAsync(User);
        var (qrCode, manualKey) = twoFactor.GenerateSetupCodeForUser(user);
        return View(new NewUserViewModel(HttpContext, qrCode, manualKey));
    }

    [Authorize]
    [HttpPost("~/newuser")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> NewUser([FromForm] string? masterpassword, [FromForm] string? twofa)
    {
        var user = await userService.GetUserAsync(User);
        if (!string.IsNullOrEmpty(masterpassword) && twoFactor.ValidateTwoFactorCodeForUser(user, twofa))
        {
            logger.LogInformation("Successfully created new safedb & validated 2fa code");

            await safeDbReadWriteService.WriteGroupsAsync(user, masterpassword, Array.Empty<SafeGroup>());
            await userService.LoginSuccessAsync(user);
            authorizationSession.Validate(masterpassword);

            return Redirect("~/");
        }

        await userService.LoginFailureAsync(user);
        logger.LogWarning("Missing master password or invalid 2fa code, returning to new user setup page");
        var (qrCode, manualKey) = twoFactor.GenerateSetupCodeForUser(user);
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
        // temporary logic to migrate the user db
        await userService.MigrateSafeDbIfNeededAsync(User, masterpassword ?? "");

        var user = await userService.GetUserAsync(User);
        if (!string.IsNullOrEmpty(masterpassword) &&
            await safeDbReadWriteService.TryReadGroupsAsync(user, masterpassword) != null &&
            twoFactor.ValidateTwoFactorCodeForUser(user, twofa))
        {
            logger.LogInformation("Successfully logged in & validated 2fa code");
            await userService.LoginSuccessAsync(user);
            authorizationSession.Validate(masterpassword);

            return RedirectTo(returnUrl);
        }

        await userService.LoginFailureAsync(user);
        logger.LogWarning("Bad master password or invalid 2fa code, returning to login page");
        return View("TwoFactorLogin", new TwoFactorLoginViewModel(HttpContext, returnUrl, true));
    }

    private RedirectResult RedirectTo(string? returnUrl) =>
        Redirect(!string.IsNullOrEmpty(returnUrl) && Uri.TryCreate(returnUrl, UriKind.Relative, out var redirectUri)
            ? redirectUri.ToString()
            : "~/");
}
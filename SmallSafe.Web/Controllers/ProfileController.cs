using Dropbox.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SmallSafe.Secure.Model;
using SmallSafe.Web.Authorization;
using SmallSafe.Web.Data;
using SmallSafe.Web.Services;
using SmallSafe.Web.ViewModels.Profile;

namespace SmallSafe.Web.Controllers;

[Authorize(Policy = TwoFactorRequirement.PolicyName)]
public class ProfileController(
    ILogger<ProfileController> logger,
    IOptionsSnapshot<DropboxConfig> dropboxConfig,
    IUserService userService,
    IAuthorizationSession authorizationSession,
    ISafeDbReadWriteService safeDbReadWriteService,
    ITwoFactor twoFactor)
    : Controller
{
    [HttpGet("~/profile")]
    public async Task<IActionResult> Index([FromQuery] bool? passwordUpdated)
    {
        var user = await userService.GetUserAsync(User);
        return View(new IndexViewModel(HttpContext, user.IsConnectedToDropbox, passwordUpdated: passwordUpdated ?? false));
    }

    [HttpPost("~/profile/password")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateMasterPassword([FromForm] string? currentpassword, [FromForm] string? newpassword, [FromForm] string? twofa)
    {
        var user = await userService.GetUserAsync(User);
        IEnumerable<SafeGroup>? groups;
        if (!string.IsNullOrEmpty(currentpassword) &&
            !string.IsNullOrEmpty(newpassword) &&
            (groups = await safeDbReadWriteService.TryReadGroupsAsync(user, currentpassword)) != null &&
            twoFactor.ValidateTwoFactorCodeForUser(user, twofa))
        {
            logger.LogInformation("Current password & 2fa code valid, updating master password");
            await safeDbReadWriteService.WriteGroupsAsync(user, newpassword, groups);
            await userService.LoginSuccessAsync(user);
            authorizationSession.Validate(newpassword);

            return Redirect("~/profile?passwordUpdated=true");
        }

        await userService.LoginFailureAsync(user);
        logger.LogWarning("Bad master password or invalid 2fa code, returning to profile page and password won't be updated");
        return View("Index", new IndexViewModel(HttpContext, user.IsConnectedToDropbox, true));
    }

    [HttpPost("~/profile/dropbox-connect")]
    [ValidateAntiForgeryToken]
    public IActionResult ConnectToDropbox()
    {
        var dropboxRedirect = DropboxOAuth2Helper.GetAuthorizeUri(OAuthResponseType.Code, dropboxConfig.Value.SmallSafeAppKey, RedirectUri, tokenAccessType: TokenAccessType.Offline, scopeList: new[] {"files.content.write"});
        logger.LogInformation("Getting user token from Dropbox: {DropboxRedirect} (redirect={RedirectUri})", dropboxRedirect, RedirectUri);
        return Redirect(dropboxRedirect.ToString());
    }

    [HttpGet("~/profile/dropbox-authentication")]
    public async Task<ActionResult> DropboxAuthentication(string code, string state)
    {
        var response = await DropboxOAuth2Helper.ProcessCodeFlowAsync(code, dropboxConfig.Value.SmallSafeAppKey, dropboxConfig.Value.SmallSafeAppSecret, RedirectUri.ToString());
        logger.LogInformation("Got user tokens from Dropbox: {AccessToken} / {RefreshToken}", response.AccessToken, response.RefreshToken);

        var user = await userService.GetUserAsync(User);
        await userService.UpdateUserDropboxAsync(user, response.AccessToken, response.RefreshToken);

        logger.LogInformation("Updating user {UserAccountId} dropbox connection", user.UserAccountId);
        return Redirect("~/profile");
    }

    [HttpPost("~/profile/dropbox-disconnect")]
    [ValidateAntiForgeryToken]
    public async Task<ActionResult> DisconnectFromDropbox()
    {
        var user = await userService.GetUserAsync(User);
        logger.LogInformation("Removing Dropbox connection from user {UserAccountId}", user.UserAccountId);
        await userService.UpdateUserDropboxAsync(user, null, null);
        return Redirect("~/profile");
    }

    private Uri RedirectUri
    {
        get
        {
            UriBuilder uriBuilder = new()
            {
                Scheme = Request.Scheme,
                Host = Request.Host.Host
            };
            if (Request.Host.Port.HasValue && Request.Host.Port != 443 && Request.Host.Port != 80)
                uriBuilder.Port = Request.Host.Port.Value;
            uriBuilder.Path = "profile/dropbox-authentication";
            return uriBuilder.Uri;
        }
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmallSafe.Web.Authorization;
using SmallSafe.Web.Services;
using SmallSafe.Web.ViewModels.Find;

namespace SmallSafe.Web.Controllers;

[Authorize, Authorize(Policy = TwoFactorRequirement.PolicyName)]
public class FindController(ILogger<FindController> logger, IUserService userService,
    IAuthorizationSession authorizationSession, ISafeDbReadWriteService safeDbReadWriteService)
    : Controller
{
    [HttpPost("~/find"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Index([FromForm] string? q)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            var referer = Request.GetTypedHeaders().Referer?.ToString() ?? "~/";
            logger.LogDebug("No find string provided, redirecting to referer: {Referer}", referer);
            return Redirect(referer);
        }

        var find = q.Trim();
        logger.LogDebug("Searching for group or entry [{Find}]", find);
        var user = await userService.GetUserAsync(User);
        var groups = await safeDbReadWriteService.ReadGroupsAsync(user, authorizationSession.MasterPassword);

        var matchedGroups = groups.Where(g => g.DeletedTimestamp == null && (g.Name?.Contains(find, StringComparison.OrdinalIgnoreCase) ?? false));
        var matchedEntries = groups.Where(g => g.DeletedTimestamp == null)
            .SelectMany(g => (g.Entries ?? []).Select(e => (Group: g, Entry: e)))
            .Where(e => e.Entry.DeletedTimestamp == null && (e.Entry.Name?.Contains(find, StringComparison.OrdinalIgnoreCase) ?? false));

        return View(new IndexViewModel(HttpContext, find, matchedGroups, matchedEntries));
    }
}
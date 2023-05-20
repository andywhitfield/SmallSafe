using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmallSafe.Secure.Model;
using SmallSafe.Web.Authorization;
using SmallSafe.Web.Services;
using SmallSafe.Web.ViewModels.Find;

namespace SmallSafe.Web.Controllers;

[Authorize(Policy = TwoFactorRequirement.PolicyName)]
public class FindController : Controller
{
    private readonly ILogger<FindController> _logger;
    private readonly IUserService _userService;
    private readonly IAuthorizationSession _authorizationSession;
    private readonly ISafeDbReadWriteService _safeDbReadWriteService;

    public FindController(ILogger<FindController> logger, IUserService userService,
        IAuthorizationSession authorizationSession, ISafeDbReadWriteService safeDbReadWriteService)
    {
        _logger = logger;
        _userService = userService;
        _authorizationSession = authorizationSession;
        _safeDbReadWriteService = safeDbReadWriteService;
    }

    [HttpPost("~/find"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Index([FromForm] string? q)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            var referer = Request.GetTypedHeaders().Referer?.ToString() ?? "~/";
            _logger.LogDebug($"No find string provided, redirecting to referer: {referer}");
            return Redirect(referer);
        }

        var find = q.Trim();
        _logger.LogDebug($"Searching for group or entry [{find}]");
        var user = await _userService.GetUserAsync(User);
        var groups = await _safeDbReadWriteService.ReadGroupsAsync(user, _authorizationSession.MasterPassword);

        var matchedGroups = groups.Where(g => g.Name?.Contains(find, StringComparison.OrdinalIgnoreCase) ?? false);
        var matchedEntries = groups
            .SelectMany(g => (g.Entries ?? Enumerable.Empty<SafeEntry>()).Select(e => (Group: g, Entry: e)))
            .Where(e => e.Entry.Name?.Contains(find, StringComparison.OrdinalIgnoreCase) ?? false);

        return View(new IndexViewModel(HttpContext, find, matchedGroups, matchedEntries));
    }
}
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmallSafe.Web.Authorization;
using SmallSafe.Web.Services;
using SmallSafe.Web.ViewModels.Group;

namespace SmallSafe.Web.Controllers;

[Authorize(Policy = TwoFactorRequirement.PolicyName)]
public class GroupController : Controller
{
    private readonly ILogger<GroupController> _logger;
    private readonly IUserService _userService;
    private readonly IAuthorizationSession _authorizationSession;
    private readonly ISafeDbReadWriteService _safeDbReadWriteService;

    public GroupController(ILogger<GroupController> logger, IUserService userService,
        IAuthorizationSession authorizationSession, ISafeDbReadWriteService safeDbReadWriteService)
    {
        _logger = logger;
        _userService = userService;
        _authorizationSession = authorizationSession;
        _safeDbReadWriteService = safeDbReadWriteService;
    }

    [HttpGet("~/group/{groupId:guid}")]
    public async Task<IActionResult> Index(Guid groupId)
    {
        _logger.LogDebug($"Viewing group {groupId}");
        var user = await _userService.GetUserAsync(User);
        var groups = await _safeDbReadWriteService.ReadGroupsAsync(user, _authorizationSession.MasterPassword);
        var group = groups.FirstOrDefault(g => g.Id == groupId);
        if (group == null)
        {
            _logger.LogWarning($"Group [{groupId}] not found, redirecting to home page");
            return Redirect("~/");
        }

        return View(new IndexViewModel(HttpContext, group));
    }

    [HttpPost("~/group"), ValidateAntiForgeryToken]
    public async Task<IActionResult> AddNewGroup([FromForm] string? name)
    {
        if (string.IsNullOrEmpty(name))
        {
            _logger.LogDebug("No group name provided, redirecting to home page");
            return Redirect("~/");
        }

        _logger.LogDebug($"Adding new group {name}");
        var user = await _userService.GetUserAsync(User);
        var groups = await _safeDbReadWriteService.ReadGroupsAsync(user, _authorizationSession.MasterPassword);
        if (groups.Any(g => string.Equals(g.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            _logger.LogError($"Group with name [{name}] already exists, cannot add a duplicate");
            return Redirect("~/");
        }

        await _safeDbReadWriteService.WriteGroupsAsync(user, _authorizationSession.MasterPassword, groups.Append(new()
        {
            Name = name
        }));
        _logger.LogDebug($"Successfully added new group {name}");

        return Redirect("~/");
    }

    [HttpPost("~/group/{groupId:guid}/delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteGroup(Guid groupId)
    {
        _logger.LogDebug($"Deleting safe group {groupId}");
        var user = await _userService.GetUserAsync(User);
        var groups = await _safeDbReadWriteService.ReadGroupsAsync(user, _authorizationSession.MasterPassword);
        await _safeDbReadWriteService.WriteGroupsAsync(user, _authorizationSession.MasterPassword, groups.Where(g => g.Id != groupId));
        _logger.LogDebug($"Successfully saved groups");

        return Redirect("~/");
    }
}
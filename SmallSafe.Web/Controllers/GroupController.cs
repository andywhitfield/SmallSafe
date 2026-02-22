using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmallSafe.Web.Authorization;
using SmallSafe.Web.Services;
using SmallSafe.Web.ViewModels.Group;

namespace SmallSafe.Web.Controllers;

[Authorize(Policy = TwoFactorRequirement.PolicyName)]
public class GroupController(ILogger<GroupController> logger, IUserService userService,
    IAuthorizationSession authorizationSession, ISafeDbReadWriteService safeDbReadWriteService)
    : Controller
{
    [HttpGet("~/group/{groupId:guid}")]
    public async Task<IActionResult> Index(Guid groupId)
    {
        logger.LogDebug("Viewing group {GroupId}", groupId);
        var user = await userService.GetUserAsync(User);
        var groups = await safeDbReadWriteService.ReadGroupsAsync(user, authorizationSession.MasterPassword);
        var group = groups.Where(g => g.DeletedTimestamp == null).FirstOrDefault(g => g.Id == groupId);
        if (group == null)
        {
            logger.LogWarning("Group [{GroupId}] not found, redirecting to home page", groupId);
            return Redirect("~/");
        }

        return View(new IndexViewModel(HttpContext, group));
    }

    [HttpPost("~/group"), ValidateAntiForgeryToken]
    public async Task<IActionResult> AddNewGroup([FromForm] string? name)
    {
        if (string.IsNullOrEmpty(name))
        {
            logger.LogDebug("No group name provided, redirecting to home page");
            return Redirect("~/");
        }

        logger.LogDebug("Adding new group {Name}", name);
        var user = await userService.GetUserAsync(User);
        var groups = await safeDbReadWriteService.ReadGroupsAsync(user, authorizationSession.MasterPassword);
        if (groups.Any(g => g.DeletedTimestamp == null && string.Equals(g.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            logger.LogError("Group with name [{Name}] already exists, cannot add a duplicate", name);
            return Redirect("~/");
        }

        await safeDbReadWriteService.WriteGroupsAsync(user, authorizationSession.MasterPassword, groups.Append(new() { Name = name }));
        logger.LogDebug("Successfully added new group {Name}", name);

        return Redirect("~/");
    }

    [HttpPost("~/group/{groupId:guid}/delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteGroup(Guid groupId)
    {
        logger.LogDebug("Deleting safe group {GroupId}", groupId);
        var user = await userService.GetUserAsync(User);
        var groups = await safeDbReadWriteService.ReadGroupsAsync(user, authorizationSession.MasterPassword);
        var group = groups.Where(g => g.DeletedTimestamp == null).FirstOrDefault(g => g.Id == groupId);
        if (group == null)
        {
            logger.LogWarning("Cannot find group with id [{GroupId}] to delete (possibly already deleted), nothing to do", groupId);
        }
        else
        {
            group.DeletedTimestamp = DateTime.UtcNow;
            await safeDbReadWriteService.WriteGroupsAsync(user, authorizationSession.MasterPassword, groups);
            logger.LogDebug("Successfully saved groups");
        }

        return Redirect("~/");
    }

    [HttpPost("~/group/sort"), ValidateAntiForgeryToken]
    public async Task<IActionResult> SortGroups()
    {
        logger.LogDebug("Sorting safe groups by name");
        var user = await userService.GetUserAsync(User);
        var groups = await safeDbReadWriteService.ReadGroupsAsync(user, authorizationSession.MasterPassword);
        await safeDbReadWriteService.WriteGroupsAsync(user, authorizationSession.MasterPassword, groups.OrderBy(g => g.Name?.ToLowerInvariant()));
        logger.LogDebug("Successfully saved groups");

        return Redirect("~/");
    }
}
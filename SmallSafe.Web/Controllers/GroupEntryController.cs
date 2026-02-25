using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmallSafe.Web.Authorization;
using SmallSafe.Web.Services;
using SmallSafe.Web.ViewModels.GroupEntry;

namespace SmallSafe.Web.Controllers;

[Authorize(Policy = TwoFactorRequirement.PolicyName)]
public class GroupEntryController(
    ILogger<GroupEntryController> logger,
    IUserService userService,
    IAuthorizationSession authorizationSession,
    ISafeDbReadWriteService safeDbReadWriteService)
    : Controller
{
    [HttpPost("~/group/{groupId:guid}/entry"), ValidateAntiForgeryToken]
    public async Task<IActionResult> AddNewGroupEntry(Guid groupId, [FromForm] string? name, [FromForm] string? encryptedvalue)
    {
        if (string.IsNullOrEmpty(encryptedvalue))
        {
            logger.LogDebug("No value provided, redirecting to group page");
            return Redirect($"~/group/{groupId}");
        }

        logger.LogDebug("Adding new entry {Name}", name);
        var user = await userService.GetUserAsync(User);
        var groups = await safeDbReadWriteService.ReadGroupsAsync(user, authorizationSession.MasterPassword);
        var group = groups.FirstOrDefault(g => g.DeletedTimestamp == null && g.Id == groupId);
        if (group == null)
        {
            logger.LogError("Group [{GroupId}] not found, redirecting to home page", groupId);
            return Redirect("~/");
        }

        group.Entries ??= [];
        group.Entries.Add(new() { Name = name, EntryValue = encryptedvalue });
        await safeDbReadWriteService.WriteGroupsAsync(user, authorizationSession.MasterPassword, groups);
        logger.LogDebug("Successfully added new entry to group {GroupId}", groupId);

        return Redirect($"~/group/{groupId}");
    }

    [HttpPost("~/group/{groupId:guid}/delete/{entryId:guid}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteEntry(Guid groupId, Guid entryId)
    {
        logger.LogDebug("Deleting safe entry {EntryId} from group {GroupId}", entryId, groupId);
        var user = await userService.GetUserAsync(User);
        var groups = await safeDbReadWriteService.ReadGroupsAsync(user, authorizationSession.MasterPassword);
        var entry = groups.FirstOrDefault(g => g.DeletedTimestamp == null && g.Id == groupId)?.Entries?.FirstOrDefault(e => e.DeletedTimestamp == null && e.Id == entryId);
        if (entry != null)
        {
            entry.DeletedTimestamp = DateTime.UtcNow;
            await safeDbReadWriteService.WriteGroupsAsync(user, authorizationSession.MasterPassword, groups);
            logger.LogDebug("Successfully saved groups");
        }

        return Redirect($"~/group/{groupId}");
    }

    [HttpPost("~/group/{groupId:guid}/edit/{entryId:guid}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateEntry(Guid groupId, Guid entryId, [FromForm] string? newencryptedvalue)
    {
        logger.LogDebug("Updating safe entry {EntryId} from group {GroupId}", entryId, groupId);
        if (string.IsNullOrEmpty(newencryptedvalue))
        {
            logger.LogDebug("No value entered, redirecting to group page");
            return Redirect($"~/group/{groupId}");
        }

        var user = await userService.GetUserAsync(User);
        var groups = await safeDbReadWriteService.ReadGroupsAsync(user, authorizationSession.MasterPassword);
        var group = groups.FirstOrDefault(g => g.DeletedTimestamp == null && g.Id == groupId);
        var entry = group?.Entries?.FirstOrDefault(e => e.DeletedTimestamp == null && e.Id == entryId);
        if (group != null && entry != null)
        {
            if (group.PreserveHistory)
            {
                group.EntriesHistory ??= [];
                group.EntriesHistory.Add(new()
                {
                    Id = entry.Id,
                    Name = entry.Name,
                    EntryValue = entry.EntryValue,
                    CreatedTimestamp = entry.CreatedTimestamp,
                    UpdatedTimestamp = entry.UpdatedTimestamp
                });
            }

            entry.EntryValue = newencryptedvalue;
            entry.UpdatedTimestamp = DateTime.UtcNow;
            await safeDbReadWriteService.WriteGroupsAsync(user, authorizationSession.MasterPassword, groups);
            logger.LogDebug("Successfully saved groups");
        }

        return Redirect($"~/group/{groupId}");
    }

    [HttpPost("~/group/{groupId:guid}/sort"), ValidateAntiForgeryToken]
    public async Task<IActionResult> SortEntries(Guid groupId)
    {
        logger.LogDebug("Sorting safe entries for group {GroupId} by name", groupId);
        var user = await userService.GetUserAsync(User);
        var groups = await safeDbReadWriteService.ReadGroupsAsync(user, authorizationSession.MasterPassword);
        var group = groups.FirstOrDefault(g => g.DeletedTimestamp == null && g.Id == groupId);
        if (group != null)
        {
            group.Entries?.Sort((x, y) => string.Compare(x.Name ?? "", y.Name ?? "", StringComparison.InvariantCultureIgnoreCase));
            await safeDbReadWriteService.WriteGroupsAsync(user, authorizationSession.MasterPassword, groups.OrderBy(g => g.Name?.ToLowerInvariant()));
            logger.LogDebug("Successfully saved groups");
        }

        return Redirect($"~/group/{groupId}");
    }

    [HttpGet("~/group/{groupId:guid}/history/{entryId:guid}")]
    public async Task<IActionResult> EntryHistory(Guid groupId, Guid entryId)
    {
        logger.LogDebug("Viewing entry history for group {GroupId} and entry {EntryId}", groupId, entryId);
        var user = await userService.GetUserAsync(User);
        var groups = await safeDbReadWriteService.ReadGroupsAsync(user, authorizationSession.MasterPassword);
        var group = groups.Where(g => g.DeletedTimestamp == null).FirstOrDefault(g => g.Id == groupId);
        if (group == null)
        {
            logger.LogWarning("Group [{GroupId}] not found, redirecting to home page", groupId);
            return Redirect("~/");
        }
        var entryHistory = group.Entries?.Where(e => e.Id == entryId).Concat(group.EntriesHistory?.Where(e => e.Id == entryId).Reverse() ?? []).ToList() ?? [];
        logger.LogDebug("Found {EntryHistoryCount} history entries for entry {EntryId} in group {GroupId}", entryHistory.Count, entryId, groupId);
        return View(new EntryHistoryViewModel(HttpContext, group, entryHistory));
    }
}
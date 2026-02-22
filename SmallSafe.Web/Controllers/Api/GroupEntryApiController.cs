using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmallSafe.Web.Authorization;
using SmallSafe.Web.Services;
using SmallSafe.Web.ViewModels.Api;

namespace SmallSafe.Web.Controllers.Api;

[ApiController, Authorize(Policy = TwoFactorRequirement.PolicyName)]
public class GroupEntryApiController(
    ILogger<GroupEntryApiController> logger,
    IUserService userService,
    IAuthorizationSession authorizationSession,
    ISafeDbReadWriteService safeDbReadWriteService)
    : ControllerBase
{
    [HttpGet("~/api/group/{groupId:guid}/entry/{entryId:guid}")]
    public async Task<ActionResult<DecryptResult>> DecryptSafeEntry(Guid groupId, Guid entryId)
    {
        logger.LogDebug("Decrypting safe db entry {EntryId} for group {GroupId}", entryId, groupId);
        var user = await userService.GetUserAsync(User);
        var groups = await safeDbReadWriteService.ReadGroupsAsync(user, authorizationSession.MasterPassword);
        var group = groups.FirstOrDefault(g => g.Id == groupId);
        if (group == null)
        {
            logger.LogError("Group [{GroupId}] not found", groupId);
            return BadRequest();
        }

        var entry = group.Entries?.Find(e => e.DeletedTimestamp == null && e.Id == entryId);
        if (entry == null)
        {
            logger.LogError("Entry [{EntryId}] not found in group [{GroupId}]", entryId, groupId);
            return BadRequest();
        }

        return new DecryptResult(entry.EntryValue ?? "");
    }

    [HttpPost("~/api/group/{groupId:guid}/entry/{entryId:guid}/move")]
    public async Task<ActionResult> MoveGroup(Guid groupId, Guid entryId, [FromForm] Guid? prevEntryId)
    {
        logger.LogDebug("Moving group {GroupId} entry {EntryId} to be after {PrevEntryId}", groupId, entryId, prevEntryId);

        var user = await userService.GetUserAsync(User);
        var groups = await safeDbReadWriteService.ReadGroupsAsync(user, authorizationSession.MasterPassword);
        var group = groups.FirstOrDefault(g => g.DeletedTimestamp == null && g.Id == groupId);
        if (group == null)
        {
            logger.LogError("Group [{GroupId}] not found", groupId);
            return BadRequest();
        }

        var entry = group.Entries?.Find(e => e.DeletedTimestamp == null && e.Id == entryId);
        if (entry == null)
        {
            logger.LogError("Entry [{EntryId}] not found", entryId);
            return BadRequest();
        }

        var prevEntry = group.Entries?.Find(e => e.DeletedTimestamp == null && e.Id == prevEntryId);
        if (prevEntry == null && prevEntryId != null)
        {
            logger.LogError("Previous entry [{PrevEntryId}] not found", prevEntryId);
            return BadRequest();
        }

        group.Entries = [.. group.Entries?.Move(entry, prevEntry, e => e.Id) ?? []];
        await safeDbReadWriteService.WriteGroupsAsync(user, authorizationSession.MasterPassword, groups);
        return Ok();
    }
}
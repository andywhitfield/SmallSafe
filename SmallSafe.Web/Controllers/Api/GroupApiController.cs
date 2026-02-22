using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmallSafe.Web.Authorization;
using SmallSafe.Web.Services;

namespace SmallSafe.Web.Controllers.Api;

[ApiController, Authorize(Policy = TwoFactorRequirement.PolicyName)]
public class GroupApiController(
    ILogger<GroupApiController> logger,
    IUserService userService,
    IAuthorizationSession authorizationSession,
    ISafeDbReadWriteService safeDbReadWriteService)
    : ControllerBase
{
    [HttpPost("~/api/group/{groupId:guid}/move")]
    public async Task<ActionResult> MoveGroup(Guid groupId, [FromForm] Guid? prevGroupId)
    {
        logger.LogDebug("Moving group {GroupId} to be after {PrevGroupId}", groupId, prevGroupId);

        var user = await userService.GetUserAsync(User);
        var groups = await safeDbReadWriteService.ReadGroupsAsync(user, authorizationSession.MasterPassword);
        var group = groups.FirstOrDefault(g => g.DeletedTimestamp == null && g.Id == groupId);
        if (group == null)
        {
            logger.LogError("Group [{GroupId}] not found", groupId);
            return BadRequest();
        }

        var prevGroup = groups.FirstOrDefault(g => g.DeletedTimestamp == null && g.Id == prevGroupId);
        if (prevGroup == null && prevGroupId != null)
        {
            logger.LogError("Previous group [{PrevGroupId}] not found", prevGroupId);
            return BadRequest();
        }

        await safeDbReadWriteService.WriteGroupsAsync(user, authorizationSession.MasterPassword, groups.Move(group, prevGroup, g => g.Id));
        return Ok();
    }
}
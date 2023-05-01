using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmallSafe.Secure.Model;
using SmallSafe.Web.Authorization;
using SmallSafe.Web.Services;

namespace SmallSafe.Web.Controllers.Api;

[ApiController, Authorize(Policy = TwoFactorRequirement.PolicyName)]
public class GroupApiController : ControllerBase
{
    private readonly ILogger<GroupApiController> _logger;
    private readonly IUserService _userService;
    private readonly IAuthorizationSession _authorizationSession;
    private readonly ISafeDbReadWriteService _safeDbReadWriteService;

    public GroupApiController(ILogger<GroupApiController> logger,
        IUserService userService,
        IAuthorizationSession authorizationSession,
        ISafeDbReadWriteService safeDbReadWriteService)
    {
        _logger = logger;
        _userService = userService;
        _authorizationSession = authorizationSession;
        _safeDbReadWriteService = safeDbReadWriteService;
    }

    [HttpPost("~/api/group/{groupId:guid}/move")]
    public async Task<ActionResult> MoveGroup(Guid groupId, [FromForm] Guid? prevGroupId)
    {
        _logger.LogDebug($"Moving group {groupId} to be after {prevGroupId}");

        var user = await _userService.GetUserAsync(User);
        var groups = await _safeDbReadWriteService.ReadGroupsAsync(user, _authorizationSession.MasterPassword);
        var group = groups.FirstOrDefault(g => g.Id == groupId);
        if (group == null)
        {
            _logger.LogError($"Group [{groupId}] not found");
            return BadRequest();
        }

        var prevGroup = groups.FirstOrDefault(g => g.Id == prevGroupId);
        if (prevGroup == null && prevGroupId != null)
        {
            _logger.LogError($"Previous group [{prevGroupId}] not found");
            return BadRequest();
        }

        await _safeDbReadWriteService.WriteGroupsAsync(user, _authorizationSession.MasterPassword, groups.Move(group, prevGroup, g => g.Id));
        return Ok();
    }
}
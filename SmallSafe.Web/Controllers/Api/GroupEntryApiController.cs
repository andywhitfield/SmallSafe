using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmallSafe.Secure;
using SmallSafe.Secure.Model;
using SmallSafe.Web.Authorization;
using SmallSafe.Web.Services;
using SmallSafe.Web.ViewModels.Api;

namespace SmallSafe.Web.Controllers.Api;

[ApiController, Authorize(Policy = TwoFactorRequirement.PolicyName)]
public class GroupEntryApiController : ControllerBase
{
    private readonly ILogger<GroupEntryApiController> _logger;
    private readonly IUserService _userService;
    private readonly IAuthorizationSession _authorizationSession;
    private readonly ISafeDbReadWriteService _safeDbReadWriteService;
    private readonly IEncryptDecrypt _encryptDecrypt;

    public GroupEntryApiController(ILogger<GroupEntryApiController> logger,
        IUserService userService,
        IAuthorizationSession authorizationSession,
        ISafeDbReadWriteService safeDbReadWriteService,
        IEncryptDecrypt encryptDecrypt)
    {
        _logger = logger;
        _userService = userService;
        _authorizationSession = authorizationSession;
        _safeDbReadWriteService = safeDbReadWriteService;
        _encryptDecrypt = encryptDecrypt;
    }
    
    [HttpGet("~/api/group/{groupId:guid}/entry/{entryId:guid}")]
    public async Task<ActionResult<DecryptResult>> DecryptSafeEntry(Guid groupId, Guid entryId)
    {
        _logger.LogDebug($"Decrypting safe db entry {entryId} for group {groupId}");
        var user = await _userService.GetUserAsync(User);
        var groups = await _safeDbReadWriteService.ReadGroupsAsync(user, _authorizationSession.MasterPassword);
        var group = groups.FirstOrDefault(g => g.Id == groupId);
        if (group == null)
        {
            _logger.LogError($"Group [{groupId}] not found");
            return BadRequest();
        }

        var entry = group.Entries?.Find(e => e.Id == entryId);
        if (entry == null)
        {
            _logger.LogError($"Entry [{entryId}] not found in group [{groupId}]");
            return BadRequest();
        }

        if (entry.IV == null || entry.Salt == null || entry.EncryptedValue == null)
        {
            _logger.LogError($"Entry [{entryId}] in group [{groupId}] is corrupt, has null iv, salt, and/or value");
            return Problem();
        }

        return new DecryptResult(await _encryptDecrypt.DecryptAsync(_authorizationSession.MasterPassword, entry.IV, entry.Salt, entry.EncryptedValue));
    }

    [HttpPost("~/api/group/{groupId:guid}/entry/{entryId:guid}/move")]
    public async Task<ActionResult> MoveGroup(Guid groupId, Guid entryId, [FromForm] Guid? prevEntryId)
    {
        _logger.LogDebug($"Moving group {groupId} entry {entryId} to be after {prevEntryId}");

        var user = await _userService.GetUserAsync(User);
        var groups = await _safeDbReadWriteService.ReadGroupsAsync(user, _authorizationSession.MasterPassword);
        var group = groups.FirstOrDefault(g => g.Id == groupId);
        if (group == null)
        {
            _logger.LogError($"Group [{groupId}] not found");
            return BadRequest();
        }

        var entry = group.Entries?.Find(e => e.Id == entryId);
        if (entry == null)
        {
            _logger.LogError($"Entry [{entryId}] not found");
            return BadRequest();
        }

        var prevEntry = group.Entries?.Find(e => e.Id == prevEntryId);
        if (prevEntry == null && prevEntryId != null)
        {
            _logger.LogError($"Previous entry [{prevEntryId}] not found");
            return BadRequest();
        }

        group.Entries = (group.Entries?.Move(entry, prevEntry, e => e.Id) ?? Enumerable.Empty<SafeEntry>()).ToList();
        await _safeDbReadWriteService.WriteGroupsAsync(user, _authorizationSession.MasterPassword, groups);
        return Ok();
    }
}
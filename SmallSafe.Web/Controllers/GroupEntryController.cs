using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmallSafe.Secure;
using SmallSafe.Secure.Model;
using SmallSafe.Web.Authorization;
using SmallSafe.Web.Services;

namespace SmallSafe.Web.Controllers;

[Authorize(Policy = TwoFactorRequirement.PolicyName)]
public class GroupEntryController : Controller
{
    private readonly ILogger<GroupEntryController> _logger;
    private readonly IUserService _userService;
    private readonly IAuthorizationSession _authorizationSession;
    private readonly ISafeDbReadWriteService _safeDbReadWriteService;
    private readonly IEncryptDecrypt _encryptDecrypt;

    public GroupEntryController(
        ILogger<GroupEntryController> logger,
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

    [HttpPost("~/group/{groupId:guid}/entry"), ValidateAntiForgeryToken]
    public async Task<IActionResult> AddNewGroupEntry(Guid groupId, [FromForm] string? name, [FromForm] string? encryptedvalue)
    {
        if (string.IsNullOrEmpty(encryptedvalue))
        {
            _logger.LogDebug("No value provided, redirecting to group page");
            return Redirect($"~/group/{groupId}");
        }

        _logger.LogDebug($"Adding new entry {name}");
        var user = await _userService.GetUserAsync(User);
        var groups = await _safeDbReadWriteService.ReadGroupsAsync(user, _authorizationSession.MasterPassword);
        var group = groups.FirstOrDefault(g => g.Id == groupId);
        if (group == null)
        {
            _logger.LogError($"Group [{groupId}] not found, redirecting to home page");
            return Redirect("~/");
        }

        var (encrypted, iv, salt) = await _encryptDecrypt.EncryptAsync(_authorizationSession.MasterPassword, encryptedvalue);
        group.Entries ??= new();
        group.Entries.Add(new() { Name = name, EncryptedValue = encrypted, IV = iv, Salt = salt });
        await _safeDbReadWriteService.WriteGroupsAsync(user, _authorizationSession.MasterPassword, groups);
        _logger.LogDebug($"Successfully added new entry to group {groupId}");

        return Redirect($"~/group/{groupId}");
    }

    [HttpPost("~/group/{groupId:guid}/delete/{entryId:guid}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteEntry(Guid groupId, Guid entryId)
    {
        _logger.LogDebug($"Deleting safe entry {entryId} from group {groupId}");
        var user = await _userService.GetUserAsync(User);
        var groups = await _safeDbReadWriteService.ReadGroupsAsync(user, _authorizationSession.MasterPassword);
        var group = groups.FirstOrDefault(g => g.Id == groupId);
        if (group != null)
        {
            group.Entries = (group.Entries?.Where(e => e.Id != entryId) ?? Enumerable.Empty<SafeEntry>()).ToList();
            await _safeDbReadWriteService.WriteGroupsAsync(user, _authorizationSession.MasterPassword, groups);
            _logger.LogDebug("Successfully saved groups");
        }

        return Redirect($"~/group/{groupId}");
    }

    [HttpPost("~/group/{groupId:guid}/edit/{entryId:guid}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateEntry(Guid groupId, Guid entryId, [FromForm] string? newencryptedvalue)
    {
        _logger.LogDebug($"Updating safe entry {entryId} from group {groupId}");
        if (string.IsNullOrEmpty(newencryptedvalue))
        {
            _logger.LogDebug("No value entered, redirecting to group page");
            return Redirect($"~/group/{groupId}");
        }

        var user = await _userService.GetUserAsync(User);
        var groups = await _safeDbReadWriteService.ReadGroupsAsync(user, _authorizationSession.MasterPassword);
        var entry = groups.FirstOrDefault(g => g.Id == groupId)?.Entries?.FirstOrDefault(e => e.Id == entryId);
        if (entry != null)
        {
            var (encrypted, iv, salt) = await _encryptDecrypt.EncryptAsync(_authorizationSession.MasterPassword, newencryptedvalue);
            entry.EncryptedValue = encrypted;
            entry.IV = iv;
            entry.Salt = salt;

            await _safeDbReadWriteService.WriteGroupsAsync(user, _authorizationSession.MasterPassword, groups);
            _logger.LogDebug("Successfully saved groups");
        }

        return Redirect($"~/group/{groupId}");
    }

    [HttpPost("~/group/{groupId:guid}/sort"), ValidateAntiForgeryToken]
    public async Task<IActionResult> SortEntries(Guid groupId)
    {
        _logger.LogDebug($"Sorting safe entries for group {groupId} by name");
        var user = await _userService.GetUserAsync(User);
        var groups = await _safeDbReadWriteService.ReadGroupsAsync(user, _authorizationSession.MasterPassword);
        var group = groups.FirstOrDefault(g => g.Id == groupId);
        if (group != null)
        {
            group.Entries?.Sort((x, y) => (x.Name ?? "").ToLowerInvariant().CompareTo(y.Name ?? ""));
            await _safeDbReadWriteService.WriteGroupsAsync(user, _authorizationSession.MasterPassword, groups.OrderBy(g => g.Name?.ToLowerInvariant()));
            _logger.LogDebug("Successfully saved groups");
        }

        return Redirect($"~/group/{groupId}");
    }
}
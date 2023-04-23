using System.Text;
using SmallSafe.Secure.Model;
using SmallSafe.Secure.Services;
using SmallSafe.Web.Data.Models;

namespace SmallSafe.Web.Services;

public class SafeDbReadWriteService : ISafeDbReadWriteService
{
    private readonly ILogger<SafeDbReadWriteService> _logger;
    private readonly IUserService _userService;
    private readonly ISafeDbService _safeDbService;

    public SafeDbReadWriteService(ILogger<SafeDbReadWriteService> logger, IUserService userService, ISafeDbService safeDbService)
    {
        _logger = logger;
        _userService = userService;
        _safeDbService = safeDbService;
    }

    public async Task<IEnumerable<SafeGroup>?> ReadGroupsAsync(UserAccount user, string masterpassword)
    {
        if (user.SafeDb == null)
        {
            _logger.LogWarning($"User {user.UserAccountId} has no safedb set");
            return null;
        }

        using MemoryStream stream = new(Encoding.UTF8.GetBytes(user.SafeDb));
        try
        {
            return await _safeDbService.ReadAsync(masterpassword, stream);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, $"Could not read safedb for user {user.UserAccountId}");
            return null;
        }
    }

    public async Task WriteGroupsAsync(UserAccount user, string masterpassword, IEnumerable<SafeGroup> groups)
    {
        using MemoryStream stream = new();
        await _safeDbService.WriteAsync(masterpassword, groups, stream);
        await _userService.UpdateUserDbAsync(user, Encoding.UTF8.GetString(stream.ToArray()));
    }
}
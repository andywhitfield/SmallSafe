using SmallSafe.Secure.Model;
using SmallSafe.Secure.Services;
using SmallSafe.Web.Data.Models;

namespace SmallSafe.Web.Services;

public class SafeDbReadWriteService(
    ILogger<SafeDbReadWriteService> logger,
    IUserService userService,
    ISafeDbService safeDbService)
    : ISafeDbReadWriteService
{
    public async Task<IEnumerable<SafeGroup>?> TryReadGroupsAsync(UserAccount user, string masterpassword)
    {
        try
        {
            return await ReadGroupsAsync(user, masterpassword);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not read safedb for user {UserAccountId}", user.UserAccountId);
            return null;
        }
    }

    public async Task<IEnumerable<SafeGroup>> ReadGroupsAsync(UserAccount user, string masterpassword)
    {
        if (user.EncyptedSafeDb == null)
        {
            logger.LogWarning("User {UserAccountId} has no safedb set", user.UserAccountId);
            throw new InvalidOperationException("User has no safe db");
        }

        await using MemoryStream stream = new(user.EncyptedSafeDb);
        return await safeDbService.ReadAsync(masterpassword, stream);
    }

    public async Task WriteGroupsAsync(UserAccount user, string masterpassword, IEnumerable<SafeGroup> groups)
    {
        await using MemoryStream stream = new();
        logger.LogDebug("Writing new password db");
        await safeDbService.WriteAsync(masterpassword, groups, stream);

        logger.LogDebug("Password db updated, saving to db...");
        await userService.UpdateUserDbAsync(user, stream.ToArray());

        logger.LogDebug("Groups updated");
    }
}
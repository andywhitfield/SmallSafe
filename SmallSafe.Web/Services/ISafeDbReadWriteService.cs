using SmallSafe.Secure.Model;
using SmallSafe.Web.Data.Models;

namespace SmallSafe.Web.Services;

public interface ISafeDbReadWriteService
{
    Task<IEnumerable<SafeGroup>?> TryReadGroupsAsync(UserAccount user, string masterpassword);
    Task<IEnumerable<SafeGroup>> ReadGroupsAsync(UserAccount user, string masterpassword);
    Task WriteGroupsAsync(UserAccount user, string masterpassword, IEnumerable<SafeGroup> groups);
}
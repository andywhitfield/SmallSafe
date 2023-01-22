using SmallSafe.Secure.Model;

namespace SmallSafe.Secure.Services;

public interface ISafeDbService
{
    Task<IEnumerable<SafeGroup>> ReadAsync(string masterPassword, Stream inputStream);
    Task WriteAsync(string masterPassword, IEnumerable<SafeGroup> safeGroups, Stream outputStream);
}

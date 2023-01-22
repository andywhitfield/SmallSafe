using SmallSafe.Secure.Model;

namespace SmallSafe.Secure.Services;

public interface ISafeDbService
{
    /// <exception cref="CryptographicException">Thrown if the master password can't read from the input stream.</exception>
    Task<IEnumerable<SafeGroup>> ReadAsync(string masterPassword, Stream inputStream);
    Task WriteAsync(string masterPassword, IEnumerable<SafeGroup> safeGroups, Stream outputStream);
}

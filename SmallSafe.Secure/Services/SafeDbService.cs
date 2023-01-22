using System.Text.Json;
using SmallSafe.Secure.Model;

namespace SmallSafe.Secure.Services;

public class SafeDbService : ISafeDbService
{
    private readonly IEncryptDecrypt _encryptDecrypt;

    public SafeDbService(IEncryptDecrypt encryptDecrypt) => _encryptDecrypt = encryptDecrypt;

    public async Task WriteAsync(string masterPassword, IEnumerable<SafeGroup> safeGroups, Stream outputStream)
    {
        var (groups, iv, salt) = _encryptDecrypt.Encrypt(masterPassword, JsonSerializer.Serialize(safeGroups));
        SafeDb safeDb = new()
        {
            IV = iv,
            Salt = salt,
            EncryptedSafeGroups = groups
        };
        await JsonSerializer.SerializeAsync(outputStream, safeDb);
    }

    public async Task<IEnumerable<SafeGroup>> ReadAsync(string masterPassword, Stream inputStream)
    {
        var safeDb = await JsonSerializer.DeserializeAsync<SafeDb>(inputStream);
        if (safeDb == null || safeDb.IV == null || safeDb.Salt == null || safeDb.EncryptedSafeGroups == null)
            throw new ArgumentException("Cannot read a valid Safe");

        var serializedGroups = _encryptDecrypt.Decrypt(masterPassword, safeDb.IV, safeDb.Salt, safeDb.EncryptedSafeGroups);
        var groups = JsonSerializer.Deserialize<IEnumerable<SafeGroup>>(serializedGroups);
        return groups ?? throw new ArgumentException("Cannot read a valid Safe");
    }
}
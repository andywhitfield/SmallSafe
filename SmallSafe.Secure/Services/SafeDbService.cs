using System.Text.Json;
using Microsoft.Extensions.Logging;
using SmallSafe.Secure.Model;

namespace SmallSafe.Secure.Services;

public class SafeDbService(ILogger<SafeDbService> logger, IEncryptDecrypt encryptDecrypt) : ISafeDbService
{
    public async Task WriteAsync(string masterPassword, IEnumerable<SafeGroup> safeGroups, Stream outputStream)
    {
        logger.LogDebug("Encrypting...");
        var (groups, iv, salt) = await encryptDecrypt.EncryptAsync(masterPassword, JsonSerializer.Serialize(safeGroups));

        logger.LogDebug("Encrypted");
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

        var serializedGroups = await encryptDecrypt.DecryptAsync(masterPassword, safeDb.IV, safeDb.Salt, safeDb.EncryptedSafeGroups);
        var groups = JsonSerializer.Deserialize<IEnumerable<SafeGroup>>(serializedGroups);
        return groups ?? throw new ArgumentException("Cannot read a valid Safe");
    }
}
using System.Security.Cryptography;
using System.Text;

namespace SmallSafe.Secure;

public sealed class EncryptDecrypt : IDisposable, IEncryptDecrypt
{
    private const int _iterations = 100_000;
    private static readonly HashAlgorithmName _hashFunction = HashAlgorithmName.SHA512;

    private readonly SymmetricAlgorithm algorithm = Aes.Create();
    private readonly RandomNumberGenerator random = RandomNumberGenerator.Create();

    public async Task<(string EncryptedValueBase64Encoded, byte[] IV, byte[] Salt)> EncryptAsync(string password, string unencryptedValue)
    {
        var salt = GenerateRandomSalt();
        algorithm.GenerateIV();
        algorithm.Key = Rfc2898DeriveBytes.Pbkdf2(password, salt, _iterations, _hashFunction, algorithm.KeySize / 8);
        await using MemoryStream encryptionStreamBacking = new();
        await using (CryptoStream encrypt = new(encryptionStreamBacking, algorithm.CreateEncryptor(), CryptoStreamMode.Write))
        {
            var unencryptedValueBytes = Encoding.Unicode.GetBytes(unencryptedValue);
            await encrypt.WriteAsync(unencryptedValueBytes, 0, unencryptedValueBytes.Length);
            await encrypt.FlushFinalBlockAsync();
        }
        return (Convert.ToBase64String(encryptionStreamBacking.ToArray()), algorithm.IV, salt);
    }

    public async Task<string> DecryptAsync(string password, byte[] iv, byte[] salt, string encryptedValueBase64Encoded)
    {
        algorithm.IV = iv;
        algorithm.Key = Rfc2898DeriveBytes.Pbkdf2(password, salt, _iterations, _hashFunction, algorithm.KeySize / 8);
        await using MemoryStream decryptionStreamBacking = new();
        await using (CryptoStream decrypt = new(decryptionStreamBacking, algorithm.CreateDecryptor(), CryptoStreamMode.Write))
        {
            var enryptedValueBytes = Convert.FromBase64String(encryptedValueBase64Encoded);
            await decrypt.WriteAsync(enryptedValueBytes, 0, enryptedValueBytes.Length);
            await decrypt.FlushAsync();
        }
        return Encoding.Unicode.GetString(decryptionStreamBacking.ToArray());
    }

    private byte[] GenerateRandomSalt()
    {
        byte[] salt = new byte[algorithm.KeySize];
        random.GetBytes(salt);
        return salt;
    }

    public void Dispose()
    {
        algorithm.Dispose();
        random.Dispose();
    }
}
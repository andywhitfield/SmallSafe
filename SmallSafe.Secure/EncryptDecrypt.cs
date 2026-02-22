using System.Security.Cryptography;
using System.Text;

namespace SmallSafe.Secure;

public sealed class EncryptDecrypt : IDisposable, IEncryptDecrypt
{
    private const int _iterations = 100_000;
    private static readonly HashAlgorithmName _hashFunction = HashAlgorithmName.SHA512;

    private readonly SymmetricAlgorithm algorithm = Aes.Create();
    private readonly RandomNumberGenerator random = RandomNumberGenerator.Create();

    public async Task<(byte[] EncryptedValue, byte[] IV, byte[] Salt)> EncryptAsync(string password, string unencryptedValue)
    {
        var salt = GenerateRandomSalt();
        algorithm.GenerateIV();
        algorithm.Key = Rfc2898DeriveBytes.Pbkdf2(password, salt, _iterations, _hashFunction, algorithm.KeySize / 8);
        await using MemoryStream encryptionStreamBacking = new();
        await using (CryptoStream encrypt = new(encryptionStreamBacking, algorithm.CreateEncryptor(), CryptoStreamMode.Write))
        {
            var unencryptedValueBytes = Encoding.Unicode.GetBytes(unencryptedValue);
            await encrypt.WriteAsync(new ReadOnlyMemory<byte>(unencryptedValueBytes));
            await encrypt.FlushFinalBlockAsync();
        }
        return (encryptionStreamBacking.ToArray(), algorithm.IV, salt);
    }

    public async Task<string> DecryptAsync(string password, byte[] iv, byte[] salt, byte[] encryptedValue)
    {
        algorithm.IV = iv;
        algorithm.Key = Rfc2898DeriveBytes.Pbkdf2(password, salt, _iterations, _hashFunction, algorithm.KeySize / 8);
        await using MemoryStream decryptionStreamBacking = new();
        await using (CryptoStream decrypt = new(decryptionStreamBacking, algorithm.CreateDecryptor(), CryptoStreamMode.Write))
        {
            await decrypt.WriteAsync(new ReadOnlyMemory<byte>(encryptedValue));
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
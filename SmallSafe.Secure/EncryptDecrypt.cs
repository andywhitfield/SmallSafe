using System.Security.Cryptography;
using System.Text;

namespace SmallSafe.Secure;

public class EncryptDecrypt : IDisposable, IEncryptDecrypt
{
    private const int _iterations = 100_000;
    private static readonly HashAlgorithmName _hashFunction = HashAlgorithmName.SHA512;

    private readonly SymmetricAlgorithm algorithm = Aes.Create();
    private readonly RandomNumberGenerator random = RandomNumberGenerator.Create();

    public (string EncryptedValueBase64Encoded, byte[] IV, byte[] Salt) Encrypt(string password, string unencryptedValue)
    {
        algorithm.GenerateIV();
        using Rfc2898DeriveBytes passwordToBytes = new(password, algorithm.KeySize, _iterations, _hashFunction);
        var salt = GenerateRandomSalt();
        passwordToBytes.Salt = salt;
        algorithm.Key = passwordToBytes.GetBytes(algorithm.KeySize / 8);
        using MemoryStream encryptionStreamBacking = new();
        using (CryptoStream encrypt = new(encryptionStreamBacking, algorithm.CreateEncryptor(), CryptoStreamMode.Write))
        {
            var unencryptedValueBytes = Encoding.Unicode.GetBytes(unencryptedValue);
            encrypt.Write(unencryptedValueBytes, 0, unencryptedValueBytes.Length);
            encrypt.FlushFinalBlock();
        }
        return (Convert.ToBase64String(encryptionStreamBacking.ToArray()), algorithm.IV, salt);
    }

    public string Decrypt(string password, byte[] iv, byte[] salt, string encryptedValueBase64Encoded)
    {
        algorithm.IV = iv;
        using Rfc2898DeriveBytes passwordToBytes = new(password, algorithm.KeySize, _iterations, _hashFunction);
        passwordToBytes.Salt = salt;
        algorithm.Key = passwordToBytes.GetBytes(algorithm.KeySize / 8);
        using MemoryStream decryptionStreamBacking = new();
        using (CryptoStream decrypt = new(decryptionStreamBacking, algorithm.CreateDecryptor(), CryptoStreamMode.Write))
        {
            var enryptedValueBytes = Convert.FromBase64String(encryptedValueBase64Encoded);
            decrypt.Write(enryptedValueBytes, 0, enryptedValueBytes.Length);
            decrypt.Flush();
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
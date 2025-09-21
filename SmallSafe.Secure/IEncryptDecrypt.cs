namespace SmallSafe.Secure;

public interface IEncryptDecrypt
{
    Task<string> DecryptAsync(string password, byte[] iv, byte[] salt, string encryptedValueBase64Encoded);
    Task<(string EncryptedValueBase64Encoded, byte[] IV, byte[] Salt)> EncryptAsync(string password, string unencryptedValue);
}

namespace SmallSafe.Secure;

public interface IEncryptDecrypt
{
    Task<string> DecryptAsync(string password, byte[] iv, byte[] salt, byte[] encryptedValue);
    Task<(byte[] EncryptedValue, byte[] IV, byte[] Salt)> EncryptAsync(string password, string unencryptedValue);
}

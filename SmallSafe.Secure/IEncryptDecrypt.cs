namespace SmallSafe.Secure;

public interface IEncryptDecrypt
{
    string Decrypt(string password, byte[] iv, byte[] salt, string encryptedValueBase64Encoded);
    (string EncryptedValueBase64Encoded, byte[] IV, byte[] Salt) Encrypt(string password, string unencryptedValue);
}

using System.Security.Cryptography;

namespace SmallSafe.Secure.Test;

[TestClass]
public class EncryptDecryptTest
{
    [TestMethod]
    public async Task ValidEncryptThenDecrypt()
    {
        (byte[] EncryptedValue, byte[] IV, byte[] Salt) encrypted;
        using (EncryptDecrypt encryptDecrypt = new())
        {
            encrypted = await encryptDecrypt.EncryptAsync("master password", "value to encrypt");
            Assert.IsNotEmpty(encrypted.EncryptedValue);

            var decrypted = await encryptDecrypt.DecryptAsync("master password", encrypted.IV, encrypted.Salt, encrypted.EncryptedValue);
            Assert.AreEqual("value to encrypt", decrypted);
        }

        using (EncryptDecrypt encryptDecrypt = new())
        {
            var decrypted = await encryptDecrypt.DecryptAsync("master password", encrypted.IV, encrypted.Salt, encrypted.EncryptedValue);
            Assert.AreEqual("value to encrypt", decrypted);
        }
    }

    [TestMethod]
    public async Task EncryptingTwiceShouldGenerateDifferentResult()
    {
        using EncryptDecrypt encryptDecrypt = new();
        var encrypted1 = await encryptDecrypt.EncryptAsync("master password", "value to encrypt");
        var encrypted2 = await encryptDecrypt.EncryptAsync("master password", "value to encrypt");
        Assert.AreNotEqual(Convert.ToBase64String(encrypted1.EncryptedValue), Convert.ToBase64String(encrypted2.EncryptedValue));
        Assert.AreNotEqual(Convert.ToBase64String(encrypted1.Salt), Convert.ToBase64String(encrypted2.Salt));
        Assert.AreNotEqual(Convert.ToBase64String(encrypted1.IV), Convert.ToBase64String(encrypted2.IV));
    }

    [TestMethod]
    public async Task ShouldFailIfDecryptWithWrongPassword()
    {
        using EncryptDecrypt encryptDecrypt = new();
        var encrypted = await encryptDecrypt.EncryptAsync("master password", "value to encrypt");
        await Assert.ThrowsExactlyAsync<CryptographicException>(async () => await encryptDecrypt.DecryptAsync("not the master password", encrypted.IV, encrypted.Salt, encrypted.EncryptedValue));
    }

    [TestMethod]
    public async Task ShouldNotDecryptCorrectlyGivenTheWrongIV()
    {
        using EncryptDecrypt encryptDecrypt = new();
        var encrypted = await encryptDecrypt.EncryptAsync("master password", "value to encrypt");
        Assert.AreNotEqual("value to encrypt", await encryptDecrypt.DecryptAsync("master password", RandomNumberGenerator.GetBytes(encrypted.IV.Length), encrypted.Salt, encrypted.EncryptedValue));
    }

    [TestMethod]
    public async Task ShouldFailIfDecryptWithWrongSalt()
    {
        using EncryptDecrypt encryptDecrypt = new();
        var encrypted = await encryptDecrypt.EncryptAsync("master password", "value to encrypt");
        await Assert.ThrowsExactlyAsync<CryptographicException>(async () => await encryptDecrypt.DecryptAsync("master password", encrypted.IV, RandomNumberGenerator.GetBytes(encrypted.Salt.Length), encrypted.EncryptedValue));
    }
}
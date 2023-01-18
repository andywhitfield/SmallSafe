using System.Security.Cryptography;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SmallSafe.Secure.Test;

[TestClass]
public class EncryptDecryptTest
{
    [TestMethod]
    public void ValidEncryptThenDecrypt()
    {
        (string EncryptedValueBase64Encoded, byte[] IV, byte[] Salt) encrypted;
        using (EncryptDecrypt encryptDecrypt = new())
        {
            encrypted = encryptDecrypt.Encrypt("master password", "value to encrypt");
            Assert.AreNotEqual("value to encrypt", encrypted.EncryptedValueBase64Encoded);

            var decrypted = encryptDecrypt.Decrypt("master password", encrypted.IV, encrypted.Salt, encrypted.EncryptedValueBase64Encoded);
            Assert.AreEqual("value to encrypt", decrypted);
        }

        using (EncryptDecrypt encryptDecrypt = new())
        {
            var decrypted = encryptDecrypt.Decrypt("master password", encrypted.IV, encrypted.Salt, encrypted.EncryptedValueBase64Encoded);
            Assert.AreEqual("value to encrypt", decrypted);
        }
    }

    [TestMethod]
    public void EncryptingTwiceShouldGenerateDifferentResult()
    {
        using EncryptDecrypt encryptDecrypt = new();
        var encrypted1 = encryptDecrypt.Encrypt("master password", "value to encrypt");
        var encrypted2 = encryptDecrypt.Encrypt("master password", "value to encrypt");
        Assert.AreNotEqual(encrypted1.EncryptedValueBase64Encoded, encrypted2.EncryptedValueBase64Encoded);
        Assert.AreNotEqual(Convert.ToBase64String(encrypted1.Salt), Convert.ToBase64String(encrypted2.Salt));
        Assert.AreNotEqual(Convert.ToBase64String(encrypted1.IV), Convert.ToBase64String(encrypted2.IV));
    }

    [TestMethod]
    public void ShouldFailIfDecryptWithWrongPassword()
    {
        using EncryptDecrypt encryptDecrypt = new();
        var encrypted = encryptDecrypt.Encrypt("master password", "value to encrypt");
        Assert.ThrowsException<CryptographicException>(() => encryptDecrypt.Decrypt("not the master password", encrypted.IV, encrypted.Salt, encrypted.EncryptedValueBase64Encoded));
    }

    [TestMethod]
    public void ShouldNotDecryptCorrectlyGivenTheWrongIV()
    {
        using EncryptDecrypt encryptDecrypt = new();
        var encrypted = encryptDecrypt.Encrypt("master password", "value to encrypt");
        Assert.AreNotEqual("value to encrypt", encryptDecrypt.Decrypt("master password", RandomNumberGenerator.GetBytes(encrypted.IV.Length), encrypted.Salt, encrypted.EncryptedValueBase64Encoded));
    }

    [TestMethod]
    public void ShouldFailIfDecryptWithWrongSalt()
    {
        using EncryptDecrypt encryptDecrypt = new();
        var encrypted = encryptDecrypt.Encrypt("master password", "value to encrypt");
        Assert.ThrowsException<CryptographicException>(() => encryptDecrypt.Decrypt("master password", encrypted.IV, RandomNumberGenerator.GetBytes(encrypted.Salt.Length), encrypted.EncryptedValueBase64Encoded));
    }
}
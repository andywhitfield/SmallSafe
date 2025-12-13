using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Moq;
using SmallSafe.Secure.Model;
using SmallSafe.Secure.Services;

namespace SmallSafe.Secure.Test.Services;

[TestClass]
public class SafeDbServiceTest
{
    [TestMethod]
    public async Task CanWriteThenRead()
    {
        using MemoryStream mem = new();

        SafeDbService safeDbService = new(Mock.Of<ILogger<SafeDbService>>(), new EncryptDecrypt());
        await safeDbService.WriteAsync(
            "master password",
            [
                new() { Name = "test group 1", Entries = new List<SafeEntry>{ new() { Name = "grp 1 entry 1", EncryptedValue = "a" }, new() { Name = "grp 1 entry 2", EncryptedValue = "b" } } },
                new() { Name = "test group 2", Entries = new List<SafeEntry>{ new() { Name = "grp 2 entry 1", EncryptedValue = "c" } } }
            ],
            mem);
        await mem.FlushAsync();

        Assert.AreNotEqual(0, mem.Length);
        // get the json string written to the stream
        var jsonOutput = Encoding.UTF8.GetString(mem.ToArray());

        Assert.DoesNotContain("test group 1", jsonOutput, jsonOutput);
        Assert.DoesNotContain("grp 1 entry 1", jsonOutput, jsonOutput);
        Assert.DoesNotContain("grp 1 entry 2", jsonOutput, jsonOutput);
        Assert.DoesNotContain("test group 2", jsonOutput, jsonOutput);
        Assert.DoesNotContain("grp 2 entry 1", jsonOutput, jsonOutput);

        Assert.Contains("\"IV\"", jsonOutput, jsonOutput);
        Assert.Contains("\"Salt\"", jsonOutput, jsonOutput);
        Assert.Contains("\"EncryptedSafeGroups\"", jsonOutput, jsonOutput);

        mem.Position = 0;
        // and read back again...
        var readGroups = await safeDbService.ReadAsync("master password", mem);
        Assert.AreEqual(2, readGroups.Count());
        var grp = readGroups.First();
        Assert.AreEqual("test group 1", grp.Name);
        Assert.IsNotNull(grp.Entries);
        Assert.HasCount(2, grp.Entries);
        Assert.AreEqual("grp 1 entry 1", grp.Entries.First().Name);
        Assert.AreEqual("a", grp.Entries.First().EncryptedValue);
        Assert.AreEqual("grp 1 entry 2", grp.Entries.Last().Name);
        Assert.AreEqual("b", grp.Entries.Last().EncryptedValue);
        
        grp = readGroups.Last();
        Assert.AreEqual("test group 2", grp.Name);
        Assert.IsNotNull(grp.Entries);
        Assert.HasCount(1, grp.Entries);
        Assert.AreEqual("grp 2 entry 1", grp.Entries.Single().Name);
        Assert.AreEqual("c", grp.Entries.Single().EncryptedValue);
    }

    [TestMethod]
    public async Task ThrowsWhenAttemptingToReadUsingAnIncorrectPassword()
    {
        using MemoryStream mem = new();

        SafeDbService safeDbService = new(Mock.Of<ILogger<SafeDbService>>(), new EncryptDecrypt());
        await safeDbService.WriteAsync(
            "master password",
            [
                new() { Name = "test group 1", Entries = new List<SafeEntry>{ new() { Name = "grp 1 entry 1", EncryptedValue = "a" }, new() { Name = "grp 1 entry 2", EncryptedValue = "b" } } },
                new() { Name = "test group 2", Entries = new List<SafeEntry>{ new() { Name = "grp 2 entry 1", EncryptedValue = "c" } } }
            ],
            mem);
        await mem.FlushAsync();

        Assert.AreNotEqual(0, mem.Length);

        mem.Position = 0;
        await Assert.ThrowsExactlyAsync<CryptographicException>(async () => await safeDbService.ReadAsync("wrong master password", mem));

        mem.Position = 0;
        await Assert.ThrowsExactlyAsync<CryptographicException>(async () => await safeDbService.ReadAsync("", mem));

        mem.Position = 0;
        #pragma warning disable CS8625
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await safeDbService.ReadAsync(null, mem));
        #pragma warning restore CS8625
    }

    [TestMethod]
    public async Task ThrowsWhenAttemptingToReadAnInvalidSafe()
    {
        SafeDbService safeDbService = new(Mock.Of<ILogger<SafeDbService>>(), new EncryptDecrypt());
        // try a null stream
        #pragma warning disable CS8625
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await safeDbService.ReadAsync("master password", null));
        #pragma warning restore CS8625

        using MemoryStream mem = new();

        // empty string
        await Assert.ThrowsExactlyAsync<JsonException>(async () => await safeDbService.ReadAsync("master password", mem));

        var invalidSafeFile = Encoding.UTF8.GetBytes("{\"IsAValidFile\": false}");
        await mem.WriteAsync(invalidSafeFile, 0, invalidSafeFile.Length);
        mem.Position = 0;

        await Assert.ThrowsExactlyAsync<ArgumentException>(async () => await safeDbService.ReadAsync("master password", mem));

        // invalid json
        mem.SetLength(0);
        var invalidJson = Encoding.UTF8.GetBytes("{ not json ");
        await mem.WriteAsync(invalidJson, 0, invalidJson.Length);

        await Assert.ThrowsExactlyAsync<JsonException>(async () => await safeDbService.ReadAsync("master password", mem));
    }
}
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SmallSafe.Web.Data;
using SmallSafe.Web.Services;
using SmallSafe.Web.ViewModels.Api;

namespace SmallSafe.Web.Test.Api;

[TestClass]
public class GroupEntryApiControllerTest
{
    private const string _groupIdGuid = "5d420ab6-f970-40cf-8236-6efb94ec2f23";
    private const string _entry1IdGuid = "8e343b15-dac4-471d-8b6a-7aa45a2355b5";
    private const string _entry2IdGuid = "d4201785-2692-48dd-a68f-2a946f3a4800";
    private static readonly Guid _groupId = Guid.ParseExact(_groupIdGuid, "D");
    private static readonly Guid _entry1Id = Guid.ParseExact(_entry1IdGuid, "D");
    private static readonly Guid _entry2Id = Guid.ParseExact(_entry2IdGuid, "D");
    private readonly TestWebApplicationFactory _factory = new();
    private readonly DateTime _now = DateTime.UtcNow;

    [TestInitialize]
    public async Task InitializeAsync()
    {
        using var serviceScope = _factory.Services.CreateScope();
        using var context = serviceScope.ServiceProvider.GetRequiredService<SqliteDataContext>();
        context.Migrate();
        var user = await context.UserAccounts!.AddAsync(new() { Email = "test-user-1", TwoFactorKey = "test-key" });
        await context.SaveChangesAsync();
        await serviceScope.ServiceProvider.GetRequiredService<ISafeDbReadWriteService>().WriteGroupsAsync(
            user.Entity,
            "test-pw", [
                new() { Id = _groupId, Name = "test group 1", Entries = [
                    new() { Id = _entry1Id, Name = "test entry 1", EntryValue = "test entry value 1", UpdatedTimestamp = _now.AddMinutes(-1) },
                    new() { Id = _entry2Id, Name = "test entry 2", EntryValue = "test entry value 2", UpdatedTimestamp = _now.AddMinutes(-2) }
                ],
                EntriesHistory = [
                    new() { Id = _entry1Id, Name = "test entry 1", EntryValue = "test entry value 1 hist 1", UpdatedTimestamp = _now.AddMinutes(-10) },
                    new() { Id = _entry2Id, Name = "test entry 2", EntryValue = "test entry value 2 hist 1", UpdatedTimestamp = _now.AddMinutes(-9) },
                    new() { Id = _entry1Id, Name = "test entry 1", EntryValue = "test entry value 1 hist 2", UpdatedTimestamp = _now.AddMinutes(-8) }
                ]
            }]);
    }

    [TestMethod]
    [DataRow(_entry1IdGuid, "test entry value 1")]
    [DataRow(_entry2IdGuid, "test entry value 2")]
    public async Task Get_entry_value(string entryIdGuid, string expectedEntryValue)
    {
        using var client = await _factory.GetLoggedInClient();
        var response = await client.GetAsync($"/api/group/{_groupId}/entry/{entryIdGuid}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<DecryptResult>();
        result.Should().NotBeNull();
        result.value.Should().Be(expectedEntryValue);
    }

    [TestMethod]
    [DataRow(_entry1IdGuid, 1, "test entry value 1")]
    [DataRow(_entry1IdGuid, 8, "test entry value 1 hist 2")]
    [DataRow(_entry1IdGuid, 10, "test entry value 1 hist 1")]
    [DataRow(_entry2IdGuid, 2, "test entry value 2")]
    [DataRow(_entry2IdGuid, 9, "test entry value 2 hist 1")]
    public async Task Get_entry_value_asof(string entryIdGuid, int asofMinuteOffset, string expectedEntryValue)
    {
        using var client = await _factory.GetLoggedInClient();
        var response = await client.GetAsync($"/api/group/{_groupId}/entry/{entryIdGuid}?asof={_now.AddMinutes(-asofMinuteOffset):o}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<DecryptResult>();
        result.Should().NotBeNull();
        result.value.Should().Be(expectedEntryValue);
    }
}

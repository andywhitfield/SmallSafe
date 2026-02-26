using System.Net;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SmallSafe.Web.Data;
using SmallSafe.Web.Services;

namespace SmallSafe.Web.Test;

[TestClass]
public class EntryHistoryPageTest
{
    private static readonly Guid _groupId = Guid.NewGuid();
    private static readonly Guid _entry1Id = Guid.NewGuid();
    private static readonly Guid _entry2Id = Guid.NewGuid();

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
    public async Task Can_view_entry_history()
    {
        using var client = await _factory.GetLoggedInClient();
        var response = await client.GetAsync($"/group/{_groupId}/history/{_entry1Id}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadAsStringAsync();
        result.Should().NotBeNull();
        result.Should().Contain("test entry 1")
            .And.Contain(_now.AddMinutes(-1).ToString("o"))
            .And.Contain(_now.AddMinutes(-10).ToString("o"))
            .And.Contain(_now.AddMinutes(-8).ToString("o"))
            .And.NotContain("test entry value 2")
            .And.NotContain(_now.AddMinutes(-2).ToString("o"))
            .And.NotContain(_now.AddMinutes(-9).ToString("o"));
    }

    [TestCleanup]
    public ValueTask Cleanup() => _factory.DisposeAsync();
}
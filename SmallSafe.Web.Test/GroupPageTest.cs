using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmallSafe.Secure.Model;
using SmallSafe.Web.Data;
using SmallSafe.Web.Services;

namespace SmallSafe.Web.Test;

[TestClass]
public class GroupPageTest
{
    private static readonly Guid _groupId = Guid.NewGuid();
    private static readonly Guid _entry1Id = Guid.NewGuid();
    private static readonly Guid _entry2Id = Guid.NewGuid();
    private static readonly Guid _entry3Id = Guid.NewGuid();

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
                    new() { Id = _entry2Id, Name = "test entry 2", EntryValue = "test entry value 2", UpdatedTimestamp = _now.AddMinutes(-2), DeletedTimestamp = _now.AddMinutes(-2) },
                    new() { Id = _entry3Id, Name = "test entry 3", EntryValue = "test entry value 3", UpdatedTimestamp = _now.AddMinutes(-3) }
                ]
            }]);
    }

    [TestMethod]
    public async Task View_all_entries_for_group()
    {
        using var client = await _factory.GetLoggedInClient();
        using var response = await client.GetAsync($"/group/{_groupId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadAsStringAsync();
        result.Should().NotBeNull();
        result.Should().Contain("test entry 1")
            .And.NotContain("test entry value 1")
            .And.NotContain("test entry 2")
            .And.NotContain("test entry value 2")
            .And.Contain("test entry 3")
            .And.NotContain("test entry value 3");
    }

    [TestMethod]
    public async Task View_deleted_entries_for_group()
    {
        using var client = await _factory.GetLoggedInClient();
        using var response = await client.GetAsync($"/group/{_groupId}?showdeleted=true");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadAsStringAsync();
        result.Should().NotBeNull();
        result.Should().Contain("test entry 1")
            .And.NotContain("test entry value 1")
            .And.Contain("test entry 2")
            .And.NotContain("test entry value 2")
            .And.Contain("test entry 3")
            .And.NotContain("test entry value 3");
    }

    [TestMethod]
    public async Task Should_not_show_save_option_for_deleted_entry()
    {
        var group = await WriteNewGroupAsync();

        using var client = await _factory.GetLoggedInClient();
        using var response = await client.GetAsync($"/group/{group.Id}?showdeleted=true");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadAsStringAsync();
        result.Should().NotBeNull();
        result.Should().Contain("test group")
            .And.Contain("deleted entry")
            .And.NotContain("ss-save-value");
    }

    private async Task<SafeGroup> WriteNewGroupAsync()
    {
        using var serviceScope = _factory.Services.CreateScope();
        using var context = serviceScope.ServiceProvider.GetRequiredService<SqliteDataContext>();
        var user = await context.UserAccounts!.FirstAsync();
        var safeDbReadWriteService = serviceScope.ServiceProvider.GetRequiredService<ISafeDbReadWriteService>();
        var groups = await safeDbReadWriteService.ReadGroupsAsync(user, "test-pw");
        SafeGroup group = new()
        {
            Name = "test group",
            Entries = [new() { Name = "deleted entry", DeletedTimestamp = _now }]
        };
        groups = groups.Append(group);
        await safeDbReadWriteService.WriteGroupsAsync(user, "test-pw", groups);        
        return group;
    }

    [TestCleanup]
    public ValueTask Cleanup() => _factory.DisposeAsync();
}
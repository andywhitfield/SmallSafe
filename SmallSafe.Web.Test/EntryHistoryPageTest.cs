using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
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
                    new() { Id = _entry2Id, Name = "test entry 2", EntryValue = "test entry value 2", UpdatedTimestamp = _now.AddMinutes(-2) },
                    new() { Id = _entry3Id, Name = "test entry 3", EntryValue = "test entry value 3", UpdatedTimestamp = _now.AddMinutes(-3) }
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
        using var response = await client.GetAsync($"/group/{_groupId}/history/{_entry1Id}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadAsStringAsync();
        result.Should().NotBeNull();
        result.Should().Contain("test entry 1")
            .And.Contain(_now.AddMinutes(-1).ToString("o"))
            .And.Contain(_now.AddMinutes(-10).ToString("o"))
            .And.Contain(_now.AddMinutes(-8).ToString("o"))
            .And.NotContain("test entry value 2")
            .And.NotContain(_now.AddMinutes(-2).ToString("o"))
            .And.NotContain(_now.AddMinutes(-9).ToString("o"))
            .And.NotContain("test entry value 3")
            .And.NotContain(_now.AddMinutes(-3).ToString("o"));
    }

    [TestMethod]
    public async Task Can_delete_entry_history()
    {
        using var client = await _factory.GetLoggedInClient();
        using var responseGet = await client.GetAsync($"/group/{_groupId}/history/{_entry1Id}");
        responseGet.StatusCode.Should().Be(HttpStatusCode.OK);

        var deleteHistoryAction = $"/group/{_groupId}/history/{_entry1Id}/delete";
        var validationToken = TestWebApplicationFactory.GetFormValidationToken(await responseGet.Content.ReadAsStringAsync(), deleteHistoryAction);
        using var responsePost = await client.PostAsync(deleteHistoryAction, new FormUrlEncodedContent([KeyValuePair.Create("__RequestVerificationToken", validationToken)]));
        responsePost.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await responsePost.Content.ReadAsStringAsync();
        content.Should().Contain("test entry 1")
            .And.Contain("test entry 2")
            .And.Contain("test entry 3");

        using var serviceScope = _factory.Services.CreateScope();
        using var context = serviceScope.ServiceProvider.GetRequiredService<SqliteDataContext>();
        var groups = await serviceScope.ServiceProvider.GetRequiredService<ISafeDbReadWriteService>().ReadGroupsAsync(await context.UserAccounts!.SingleAsync(), "test-pw");
        groups.Should().HaveCount(1);
        var group = groups.Single();
        group.Entries.Should().HaveCount(3);
        group.Entries.Should().Contain(e => e.Id == _entry1Id);
        group.Entries.Should().Contain(e => e.Id == _entry2Id);
        group.Entries.Should().Contain(e => e.Id == _entry3Id);
        group.EntriesHistory.Should().HaveCount(1);
        group.EntriesHistory.Where(e => e.Id == _entry1Id).Should().BeEmpty();
        group.EntriesHistory.Where(e => e.Id == _entry2Id).Should().HaveCount(1);
    }

    [TestMethod]
    public async Task Should_redirect_to_home_page_when_deleting_from_unknown_group()
    {
        using var client = await _factory.GetLoggedInClient();
        using var responseGet = await client.GetAsync($"/group/{_groupId}/history/{_entry1Id}");
        responseGet.StatusCode.Should().Be(HttpStatusCode.OK);

        var deleteHistoryAction = $"/group/{_groupId}/history/{_entry1Id}/delete";
        var validationToken = TestWebApplicationFactory.GetFormValidationToken(await responseGet.Content.ReadAsStringAsync(), deleteHistoryAction);
        deleteHistoryAction = deleteHistoryAction.Replace(_groupId.ToString(), Guid.NewGuid().ToString());
        using var responsePost = await client.PostAsync(deleteHistoryAction, new FormUrlEncodedContent([KeyValuePair.Create("__RequestVerificationToken", validationToken)]));
        responsePost.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await responsePost.Content.ReadAsStringAsync();
        // this should be the home page, so should list the groups and no entry values
        content.Should().Contain("test group 1");
        content.Should().NotContain("test entry 1")
            .And.NotContain("test entry 2")
            .And.NotContain("test entry 3");
    }

    [TestMethod]
    public async Task Can_delete_entry_history_for_an_entry_with_no_history()
    {
        using var client = await _factory.GetLoggedInClient();
        using var responseGet = await client.GetAsync($"/group/{_groupId}/history/{_entry3Id}");
        responseGet.StatusCode.Should().Be(HttpStatusCode.OK);

        var deleteHistoryAction = $"/group/{_groupId}/history/{_entry3Id}/delete";
        var validationToken = TestWebApplicationFactory.GetFormValidationToken(await responseGet.Content.ReadAsStringAsync(), deleteHistoryAction);
        using var responsePost = await client.PostAsync(deleteHistoryAction, new FormUrlEncodedContent([KeyValuePair.Create("__RequestVerificationToken", validationToken)]));
        responsePost.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await responsePost.Content.ReadAsStringAsync();
        content.Should().Contain("test entry 3")
            .And.Contain("test entry 1")
            .And.Contain("test entry 2");

        using var serviceScope = _factory.Services.CreateScope();
        using var context = serviceScope.ServiceProvider.GetRequiredService<SqliteDataContext>();
        var groups = await serviceScope.ServiceProvider.GetRequiredService<ISafeDbReadWriteService>().ReadGroupsAsync(await context.UserAccounts!.SingleAsync(), "test-pw");
        groups.Should().HaveCount(1);
        var group = groups.Single();
        group.Entries.Should().HaveCount(3);
        group.Entries.Should().Contain(e => e.Id == _entry1Id);
        group.Entries.Should().Contain(e => e.Id == _entry2Id);
        group.Entries.Should().Contain(e => e.Id == _entry3Id);
        group.EntriesHistory.Should().HaveCount(3);
        group.EntriesHistory.Where(e => e.Id == _entry1Id).Should().HaveCount(2);
        group.EntriesHistory.Where(e => e.Id == _entry2Id).Should().HaveCount(1);
    }

    [TestCleanup]
    public ValueTask Cleanup() => _factory.DisposeAsync();
}
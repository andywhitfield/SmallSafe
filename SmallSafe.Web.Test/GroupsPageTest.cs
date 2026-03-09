using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmallSafe.Web.Data;
using SmallSafe.Web.Services;

namespace SmallSafe.Web.Test;

[TestClass]
public class GroupsPageTest
{
    private static readonly Guid _groupId1 = Guid.NewGuid();
    private static readonly Guid _groupId2 = Guid.NewGuid();

    private readonly TestWebApplicationFactory _factory = new();
    private readonly DateTime _now = DateTime.UtcNow;

    [TestInitialize]
    public async Task InitializeAsync()
    {
        await using var serviceScope = _factory.Services.CreateAsyncScope();
        var context = serviceScope.ServiceProvider.GetRequiredService<SqliteDataContext>();
        context.Migrate();
        var user = await context.UserAccounts!.AddAsync(new() { Email = "test-user-1", TwoFactorKey = "test-key" });
        await context.SaveChangesAsync();
        await serviceScope.ServiceProvider.GetRequiredService<ISafeDbReadWriteService>().WriteGroupsAsync(
            user.Entity,
            "test-pw", [
                new() { Id = _groupId1, Name = "test group 1", Entries = [new() { Name = "test entry 1 for group 1" }] },
                new() { Id = _groupId2, Name = "test group 2", DeletedTimestamp = DateTime.UtcNow, Entries = [new() { Name = "test entry 1 for group 2" }] }
            ]);
    }

    [TestMethod]
    public async Task View_all_groups_doesnt_show_deleted_groups_by_default()
    {
        using var client = await _factory.GetLoggedInClient();
        using var response = await client.GetAsync("/");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadAsStringAsync();
        result.Should().NotBeNull();
        result.Should().Contain("test group 1")
            .And.NotContain("test group 2");
    }

    [TestMethod]
    public async Task View_all_groups_including_deleted_groups()
    {
        using var client = await _factory.GetLoggedInClient();
        using var response = await client.GetAsync("/?showdeleted=true");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadAsStringAsync();
        result.Should().NotBeNull();
        result.Should().Contain("test group 1")
            .And.Contain("test group 2");
    }

    [TestMethod]
    public async Task Deleting_a_group_Should_set_the_deleted_timestamp()
    {
        using var client = await _factory.GetLoggedInClient();
        using var response = await client.GetAsync("/");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadAsStringAsync();
        var deleteGroupAction = $"/group/{_groupId1}/delete";
        var validationToken = TestWebApplicationFactory.GetFormValidationToken(result, deleteGroupAction);
        
        using var responsePost = await client.PostAsync(deleteGroupAction, new FormUrlEncodedContent([KeyValuePair.Create("__RequestVerificationToken", validationToken)]));
        responsePost.StatusCode.Should().Be(HttpStatusCode.OK);
        var finalResult = await responsePost.Content.ReadAsStringAsync();
        finalResult.Should().NotBeNull();
        finalResult.Should().NotContain("test group 1")
            .And.NotContain("test group 2");

        using var serviceScope = _factory.Services.CreateScope();
        using var context = serviceScope.ServiceProvider.GetRequiredService<SqliteDataContext>();
        var user = await context.UserAccounts!.FirstAsync();
        var safeDbReadWriteService = serviceScope.ServiceProvider.GetRequiredService<ISafeDbReadWriteService>();
        var groups = await safeDbReadWriteService.ReadGroupsAsync(user, "test-pw");
        groups.Single(g => g.Id == _groupId1).DeletedTimestamp.Should().NotBeNull();
    }

    [TestMethod]
    public async Task Deleting_a_deleted_group_Should_permanently_remove_it()
    {
        using var client = await _factory.GetLoggedInClient();
        using var response = await client.GetAsync("/?showdeleted=true");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadAsStringAsync();
        var deleteGroupAction = $"/group/{_groupId2}/delete";
        var validationToken = TestWebApplicationFactory.GetFormValidationToken(result, deleteGroupAction);
        
        using var responsePost = await client.PostAsync(deleteGroupAction, new FormUrlEncodedContent([KeyValuePair.Create("__RequestVerificationToken", validationToken)]));
        responsePost.StatusCode.Should().Be(HttpStatusCode.OK);
        var finalResult = await responsePost.Content.ReadAsStringAsync();
        finalResult.Should().NotBeNull();
        finalResult.Should().Contain("test group 1")
            .And.NotContain("test group 2");

        using var finalResponse = await client.GetAsync("/?showdeleted=true");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await responsePost.Content.ReadAsStringAsync())
            .Should().NotBeNull()
            .And.Contain("test group 1")
            .And.NotContain("test group 2");

        using var serviceScope = _factory.Services.CreateScope();
        using var context = serviceScope.ServiceProvider.GetRequiredService<SqliteDataContext>();
        var user = await context.UserAccounts!.FirstAsync();
        var safeDbReadWriteService = serviceScope.ServiceProvider.GetRequiredService<ISafeDbReadWriteService>();
        var groups = await safeDbReadWriteService.ReadGroupsAsync(user, "test-pw");
        groups.Should().NotContain(g => g.Id == _groupId2).And.Contain(g => g.Id == _groupId1);
    }

    [TestCleanup]
    public ValueTask Cleanup() => _factory.DisposeAsync();
}
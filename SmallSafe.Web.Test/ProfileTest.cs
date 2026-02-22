using System.Net;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SmallSafe.Secure.Model;
using SmallSafe.Web.Data;
using SmallSafe.Web.Services;

namespace SmallSafe.Web.Test;

[TestClass]
public class ProfileTest
{
    private readonly TestWebApplicationFactory _factory = new();
    private readonly Guid _groupId = Guid.NewGuid();
    private readonly Guid _entryId = Guid.NewGuid();

    [TestInitialize]
    public async Task InitializeAsync()
    {
        using var serviceScope = _factory.Services.CreateScope();
        using var context = serviceScope.ServiceProvider.GetRequiredService<SqliteDataContext>();
        context.Migrate();
        var user = await context.UserAccounts!.AddAsync(new() { Email = "test-user-1", TwoFactorKey = "test-key" });
        await context.SaveChangesAsync();        
        await serviceScope.ServiceProvider.GetRequiredService<ISafeDbReadWriteService>().WriteGroupsAsync(user.Entity, "test-pw", new SafeGroup[] { new() { Id = _groupId, Name = "test group 1", Entries = new List<SafeEntry> { new() { Id = _entryId, Name = "test entry", EntryValue = "test entry value" } } } });
    }

    [TestMethod]
    public async Task Can_update_password()
    {
        using var client = _factory.CreateAuthenticatedClient();

        var response = await client.GetAsync("/");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var responseContent = await _factory.LoginAsync(client, await response.Content.ReadAsStringAsync());
        responseContent.Should().Contain("test group 1");
        // decrypt entry 1 using old password
        responseContent = await client.GetStringAsync($"/group/{_groupId}");
        responseContent.Should().Contain("test group 1");
        responseContent.Should().Contain("test entry");
        responseContent = await client.GetStringAsync($"/api/group/{_groupId}/entry/{_entryId}");
        responseContent.Should().Contain("test entry value");

        // now change the master password
        responseContent = await (await client.GetAsync("/profile")).Content.ReadAsStringAsync();
        responseContent.Should().Contain("Change master password");
        responseContent = await ChangePasswordAsync(client, responseContent);
        responseContent.Should().Contain("Your master password has been successfully updated");

        responseContent = await (await client.GetAsync("/")).Content.ReadAsStringAsync();
        responseContent.Should().Contain("test group 1");
        // decrypt entry 1 using new password
        responseContent = await client.GetStringAsync($"/group/{_groupId}");
        responseContent.Should().Contain("test group 1");
        responseContent.Should().Contain("test entry");
        responseContent = await client.GetStringAsync($"/api/group/{_groupId}/entry/{_entryId}");
        responseContent.Should().Contain("test entry value");
    }

    public async Task<string> ChangePasswordAsync(HttpClient client, string page)
    {
        var loginAction = "/profile/password";
        var validationToken = TestWebApplicationFactory.GetFormValidationToken(page, loginAction);

        using var response = await client.PostAsync(loginAction, new FormUrlEncodedContent(new[] {
            KeyValuePair.Create("__RequestVerificationToken", validationToken),
            KeyValuePair.Create("currentpassword", "test-pw"),
            KeyValuePair.Create("newpassword", "new-test-pw"),
            KeyValuePair.Create("twofa", "123456")
        }));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadAsStringAsync();
    }

    [TestCleanup]
    public void Cleanup() => _factory.Dispose();
}
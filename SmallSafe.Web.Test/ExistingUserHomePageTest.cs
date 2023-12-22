using System.Net;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SmallSafe.Secure.Model;
using SmallSafe.Web.Data;
using SmallSafe.Web.Services;

namespace SmallSafe.Web.Test;

[TestClass]
public class ExistingUserHomePageTest
{
    private readonly TestWebApplicationFactory _factory = new();

    [TestInitialize]
    public async Task InitializeAsync()
    {
        using var serviceScope = _factory.Services.CreateScope();
        using var context = serviceScope.ServiceProvider.GetRequiredService<SqliteDataContext>();
        context.Migrate();
        var user = await context.UserAccounts!.AddAsync(new() { Email = "test-user-1", TwoFactorKey = "test-key" });
        await context.SaveChangesAsync();
        await serviceScope.ServiceProvider.GetRequiredService<ISafeDbReadWriteService>().WriteGroupsAsync(user.Entity, "test-pw", Enumerable.Empty<SafeGroup>());
    }

    [TestMethod]
    public async Task Given_existing_user_Should_display_welcome_back_page()
    {
        using var client = _factory.CreateAuthenticatedClient();
        var response = await client.GetAsync("/");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var responseContent = await response.Content.ReadAsStringAsync();
        responseContent.Should().Contain("Logout")
            .And.Contain("Welcome back")
            .And.Contain("Enter your master password")
            .And.Contain("Enter the code displayed in your authenticator app");
    }

    [TestMethod]
    public async Task Given_existing_user_When_logged_on_Should_display_empty_groups()
    {
        using var client = _factory.CreateAuthenticatedClient();

        var response = await client.GetAsync("/");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var responseContent = await _factory.LoginAsync(client, await response.Content.ReadAsStringAsync());
        responseContent.Should().Contain("You have no groups");
    }

    [TestCleanup]
    public void Cleanup() => _factory.Dispose();
}
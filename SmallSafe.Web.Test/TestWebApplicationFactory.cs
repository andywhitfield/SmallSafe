using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Moq;
using SmallSafe.Web.Data;
using SmallSafe.Web.Data.Models;
using SmallSafe.Web.Services;

namespace SmallSafe.Web.Test;

public class TestWebApplicationFactory : WebApplicationFactory<Startup>
{
    private readonly Mock<ITwoFactor> _twoFactor = new();
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<SqliteDataContext> _options;

    public TestWebApplicationFactory()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<SqliteDataContext>().UseSqlite(_connection).Options;
    }

    protected override IHostBuilder CreateHostBuilder() => Host
        .CreateDefaultBuilder()
        .ConfigureWebHostDefaults(x => x.UseStartup<Startup>().UseTestServer().ConfigureTestServices(services =>
        {
            services.Replace(ServiceDescriptor.Transient<ITwoFactor>(_ => _twoFactor.Object));
            services.Replace(ServiceDescriptor.Scoped<SqliteDataContext>(_ => new SqliteDataContext(_options)));
            services
                .AddAuthentication("Test")
                .AddScheme<AuthenticationSchemeOptions, TestStubAuthHandler>("Test", null);
        }));

    public HttpClient CreateAuthenticatedClient(bool allowAutoRedirect = true)
    {
        _twoFactor.Setup(x => x.ValidateTwoFactorCodeForUser(It.IsAny<UserAccount>(), It.IsAny<string?>())).Returns(true);
        var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = allowAutoRedirect });
        client.DefaultRequestHeaders.Authorization = new("Test");
        return client;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
            _connection.Dispose();
    }

    public static string GetFormValidationToken(string responseContent, string formAction)
    {
        var validationToken = responseContent.Substring(responseContent.IndexOf($"action=\"{formAction}\""));
        validationToken = validationToken.Substring(validationToken.IndexOf("__RequestVerificationToken"));
        validationToken = validationToken.Substring(validationToken.IndexOf("value=\"") + 7);
        validationToken = validationToken.Substring(0, validationToken.IndexOf('"'));
        return validationToken;
    }

    public async Task<string> LoginAsync(HttpClient client, string page)
    {
        var loginAction = "/twofactor";
        var validationToken = TestWebApplicationFactory.GetFormValidationToken(page, loginAction);

        using var response = await client.PostAsync(loginAction, new FormUrlEncodedContent(new[] {
            KeyValuePair.Create("__RequestVerificationToken", validationToken),
            KeyValuePair.Create("masterpassword", "test-pw"),
            KeyValuePair.Create("twofa", "123456")
        }));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadAsStringAsync();
    }
}

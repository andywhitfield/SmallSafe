using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SmallSafe.Web.Test;

public class TestStubAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        Claim[] claims = [new(ClaimTypes.Name, "Test user"), new("name", "http://test/user/1")];
        ClaimsIdentity identity = new(claims, "Test");
        ClaimsPrincipal principal = new(identity);
        AuthenticationTicket ticket = new(principal, "Test");

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

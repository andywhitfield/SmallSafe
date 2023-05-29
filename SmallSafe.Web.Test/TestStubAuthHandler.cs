using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SmallSafe.Web.Test;

public class TestStubAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TestStubAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger, UrlEncoder encoder, ISystemClock clock)
    : base(options, logger, encoder, clock)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new Claim[] { new(ClaimTypes.Name, "Test user"), new("sub", "http://test/user/1") };
        ClaimsIdentity identity = new(claims, "Test");
        ClaimsPrincipal principal = new(identity);
        AuthenticationTicket ticket = new(principal, "Test");

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

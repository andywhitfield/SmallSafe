using Microsoft.AspNetCore.Authorization;

namespace SmallSafe.Web.Authorization;

public class TwoFactorHandler : AuthorizationHandler<TwoFactorRequirement>
{
    private readonly ILogger<TwoFactorHandler> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TwoFactorHandler(ILogger<TwoFactorHandler> logger, IHttpContextAccessor httpContextAccessor)
    {
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
    }

    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, TwoFactorRequirement requirement)
    {
        if (context.User != null && _httpContextAccessor.HttpContext?.Session?.GetInt32("twofa") == 1)
        {
            _logger.LogTrace("User has valid 2fa token");
            // TODO check session has password & 2fa success token
            // TODO should also check the db to ensure the last two fa success is recent / hasn't been superceeded by a failure, that kind of thing
            context.Succeed(requirement);
        }
        else
        {
            _logger.LogTrace($"User missing [{context.User == null}] or has invalid 2fa token [{_httpContextAccessor.HttpContext?.Session?.GetInt32("twofa")}]");
        }

        return Task.CompletedTask;
    }
}
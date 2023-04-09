using Microsoft.AspNetCore.Authorization;

namespace SmallSafe.Web.Authorization;

public class TwoFactorRequirement : IAuthorizationRequirement
{
    public const string PolicyName = "TwoFactor";
}
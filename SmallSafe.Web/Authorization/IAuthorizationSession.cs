namespace SmallSafe.Web.Authorization;

public interface IAuthorizationSession
{
    Task<(bool IsReturningUser, string VerifyOptions)> HandleSigninRequest(string email, CancellationToken cancellationToken);
    Task<bool> HandleSigninVerifyRequest(HttpContext httpContext, string email, string verifyOptions, string verifyResponse, CancellationToken cancellationToken);

    string MasterPassword { get; }
    bool IsValidated { get; }

    void Validate(string masterPassword);
}

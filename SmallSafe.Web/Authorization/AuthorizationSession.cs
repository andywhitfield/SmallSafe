namespace SmallSafe.Web.Authorization;

public class AuthorizationSession : IAuthorizationSession
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuthorizationSession(IHttpContextAccessor httpContextAccessor) => _httpContextAccessor = httpContextAccessor;

    public string MasterPassword => (IsValidated ? _httpContextAccessor.HttpContext!.Session.GetString("pw") : null)
        ?? throw new InvalidOperationException("Authorization session has not been validated");

    public bool IsValidated => _httpContextAccessor.HttpContext?.Session.GetInt32("val") == 1;

    public void Validate(string masterPassword)
    {
        var session = (_httpContextAccessor.HttpContext?.Session ?? throw new InvalidOperationException("Cannot access the auth session"));
        session.SetInt32("val", 1);
        session.SetString("pw", masterPassword);
    }
}
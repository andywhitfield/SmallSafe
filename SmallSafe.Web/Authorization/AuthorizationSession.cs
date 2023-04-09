namespace SmallSafe.Web.Authorization;

public class AuthorizationSession : IAuthorizationSession
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuthorizationSession(IHttpContextAccessor httpContextAccessor) => _httpContextAccessor = httpContextAccessor;

    public string MasterPassword => (IsValidated ? _httpContextAccessor.HttpContext!.Session.GetString("pw") : null)
        ?? throw new InvalidOperationException("Authorization session has not been validated");

    public bool IsValidated
    {
        get
        {
            var ticks = _httpContextAccessor.HttpContext?.Session.Get("dt");
            if (ticks == null)
                return false;
            
            DateTime createdDate = new(BitConverter.ToInt64(ticks));
            // to be valid, the session start time shouldn't be in the future and shouldn't be older than an hour
            return createdDate < DateTime.UtcNow && (DateTime.UtcNow - createdDate) <= TimeSpan.FromHours(1);
        }
    }

    public void Validate(string masterPassword)
    {
        var session = (_httpContextAccessor.HttpContext?.Session ?? throw new InvalidOperationException("Cannot access the auth session"));
        session.Set("dt", BitConverter.GetBytes(DateTime.UtcNow.Ticks));
        session.SetString("pw", masterPassword);
    }
}
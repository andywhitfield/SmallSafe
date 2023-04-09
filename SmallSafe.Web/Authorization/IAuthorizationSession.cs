namespace SmallSafe.Web.Authorization;

public interface IAuthorizationSession
{
    string MasterPassword { get; }
    bool IsValidated { get; }

    void Validate(string masterPassword);
}

namespace SmallSafe.Web.ViewModels.Profile;

public class IndexViewModel : BaseViewModel
{
    public IndexViewModel(HttpContext context, bool isConnectedToDropbox,
        bool isPasswordOrTwoFaInvalid = false, bool passwordUpdated = false) : base(context)
    {
        IsConnectedToDropbox = isConnectedToDropbox;
        IsPasswordOrTwoFaInvalid = isPasswordOrTwoFaInvalid;
        PasswordUpdated = passwordUpdated;
    }

    public bool IsConnectedToDropbox { get; init; }
    public bool IsPasswordOrTwoFaInvalid { get; init; }
    public bool PasswordUpdated { get; init; }
}
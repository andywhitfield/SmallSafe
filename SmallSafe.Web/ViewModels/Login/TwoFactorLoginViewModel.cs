namespace SmallSafe.Web.ViewModels.Login;

public class TwoFactorLoginViewModel : BaseViewModel
{
    public TwoFactorLoginViewModel(HttpContext context, string? returnUrl, bool isPasswordOrTwoFaInvalid = false) : base(context)
    {
        ReturnUrl = returnUrl;
        IsPasswordOrTwoFaInvalid = isPasswordOrTwoFaInvalid;
    }

    public string? ReturnUrl { get; init; }
    public bool IsPasswordOrTwoFaInvalid { get; init; }
}

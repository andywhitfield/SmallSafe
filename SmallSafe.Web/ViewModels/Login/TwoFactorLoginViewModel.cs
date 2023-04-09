namespace SmallSafe.Web.ViewModels.Login;

public class TwoFactorLoginViewModel : BaseViewModel
{
    public TwoFactorLoginViewModel(HttpContext context, string? returnUrl) : base(context)
    {
        ReturnUrl = returnUrl;
    }

    public string? ReturnUrl { get; init; }
}

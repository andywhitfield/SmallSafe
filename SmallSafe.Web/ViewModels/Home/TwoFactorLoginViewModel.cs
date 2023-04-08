namespace SmallSafe.Web.ViewModels.Home;

public class TwoFactorLoginViewModel : BaseViewModel
{
    public TwoFactorLoginViewModel(HttpContext context, string? returnUrl) : base(context)
    {
        ReturnUrl = returnUrl;
    }

    public string? ReturnUrl { get; init; }
}

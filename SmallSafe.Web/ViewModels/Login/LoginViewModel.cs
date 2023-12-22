namespace SmallSafe.Web.ViewModels.Login;

public class LoginViewModel(HttpContext context, string? returnUrl) : BaseViewModel(context)
{
    public string? ReturnUrl { get; } = returnUrl;
}

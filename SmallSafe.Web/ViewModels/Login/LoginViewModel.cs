namespace SmallSafe.Web.ViewModels.Login;
public class LoginViewModel : BaseViewModel
{
    public string ReturnUrl { get; }
    public LoginViewModel(HttpContext context, string returnUrl) : base(context) => ReturnUrl = returnUrl;
}

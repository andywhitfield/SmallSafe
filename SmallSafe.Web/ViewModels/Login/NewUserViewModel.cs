namespace SmallSafe.Web.ViewModels.Login;

public class NewUserViewModel : BaseViewModel
{
    public string TwoFactorImageUrl { get; init; }
    public string TwoFactorManualEntrySetupCode { get; init; }

    public NewUserViewModel(HttpContext context, string twoFactorImageUrl, string twoFactorManualEntrySetupCode) : base(context)
    {
        TwoFactorImageUrl = twoFactorImageUrl;
        TwoFactorManualEntrySetupCode = twoFactorManualEntrySetupCode;
    }
}
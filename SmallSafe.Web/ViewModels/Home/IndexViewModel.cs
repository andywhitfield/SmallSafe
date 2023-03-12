namespace SmallSafe.Web.ViewModels.Home;

public class IndexViewModel : BaseViewModel
{
    public string QrCodeImageUrl { get; init; }
    public string ManualEntrySetupCode { get; init; }
    public bool? ValidationResult { get; init; }

    public IndexViewModel(HttpContext context, string qrCodeImageUrl, string manualEntrySetupCode, bool? validationResult) : base(context)
    {
        ManualEntrySetupCode = manualEntrySetupCode;
        QrCodeImageUrl = qrCodeImageUrl;
        ValidationResult = validationResult;
    }
}
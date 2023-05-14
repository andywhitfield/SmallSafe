namespace SmallSafe.Web.ViewModels.Profile;

public class IndexViewModel : BaseViewModel
{
    public IndexViewModel(HttpContext context, bool isConnectedToDropbox) : base(context)
    {
        IsConnectedToDropbox = isConnectedToDropbox;
    }

    public bool IsConnectedToDropbox { get; init; }
}
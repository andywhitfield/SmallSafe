namespace SmallSafe.Web.ViewModels;

public abstract class BaseViewModel
{
    protected BaseViewModel(HttpContext context) =>
        IsLoggedIn = context.User?.Identity?.IsAuthenticated ?? false;

    public bool IsLoggedIn { get; init; }
}

namespace SmallSafe.Web.ViewModels;

public abstract class BaseViewModel
{
    protected BaseViewModel(HttpContext context, string? find = null)
    {
        IsLoggedIn = context.User?.Identity?.IsAuthenticated ?? false;
        Find = find ?? "";
    }

    public bool IsLoggedIn { get; init; }
    public string Find { get; init; }
}

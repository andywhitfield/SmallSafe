using SmallSafe.Secure.Model;

namespace SmallSafe.Web.ViewModels.Home;

public class IndexViewModel(HttpContext context, IEnumerable<SafeGroup> groups, bool showDeleted) : BaseViewModel(context)
{
    public IEnumerable<SafeGroup> Groups { get; } = groups;
    public bool ShowDeleted { get; } = showDeleted;
}
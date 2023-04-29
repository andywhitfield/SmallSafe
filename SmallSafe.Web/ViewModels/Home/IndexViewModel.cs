using SmallSafe.Secure.Model;

namespace SmallSafe.Web.ViewModels.Home;

public class IndexViewModel : BaseViewModel
{
    public IEnumerable<SafeGroup> Groups { get; }

    public IndexViewModel(HttpContext context, IEnumerable<SafeGroup> groups) : base(context) => Groups = groups;
}
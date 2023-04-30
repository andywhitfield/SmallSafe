using SmallSafe.Secure.Model;

namespace SmallSafe.Web.ViewModels.Group;

public class IndexViewModel : BaseViewModel
{
    public IndexViewModel(HttpContext context, SafeGroup group) : base(context) => Group = group;

    public SafeGroup Group { get; }
}
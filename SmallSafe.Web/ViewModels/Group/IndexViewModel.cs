using SmallSafe.Secure.Model;

namespace SmallSafe.Web.ViewModels.Group;

public class IndexViewModel(HttpContext context, SafeGroup group, bool showDeleted) : BaseViewModel(context)
{
    public SafeGroup Group { get; } = group;
    public IEnumerable<SafeEntry> Entries => Group.Entries?.Where(e => ShowDeleted || e.DeletedTimestamp == null) ?? [];
    public bool ShowDeleted { get; } = showDeleted;
}
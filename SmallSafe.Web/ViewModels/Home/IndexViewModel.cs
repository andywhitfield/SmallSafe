using SmallSafe.Secure.Model;

namespace SmallSafe.Web.ViewModels.Home;

public class IndexViewModel(HttpContext context, IEnumerable<SafeGroup> groups, bool showDeleted) : BaseViewModel(context)
{
    public IEnumerable<SafeGroup> Groups { get; } = groups;
    public bool ShowDeleted { get; } = showDeleted;
    public string DeleteConfirmText(SafeGroup group) => group.DeletedTimestamp == null
        ? "Are you sure you want to delete this group? Note that this will also delete all password entries in this group."
        : "Are you sure you want to permanently delete this group? This cannot be undone. All passwords in this group will also be permanently deleted.";
}
using SmallSafe.Secure.Model;

namespace SmallSafe.Web.ViewModels.GroupEntry;

public class EntryHistoryViewModel(HttpContext context, SafeGroup group, IEnumerable<SafeEntry> entryHistory)
: BaseViewModel(context)
{
    public SafeGroup Group { get; } = group;
    public IEnumerable<SafeEntry> EntryHistory { get; } = entryHistory;
}
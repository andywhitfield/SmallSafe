using SmallSafe.Secure.Model;

namespace SmallSafe.Web.ViewModels.GroupEntry;

public class EntryHistoryViewModel(HttpContext context, SafeGroup group, Guid safeEntryId, IEnumerable<SafeEntry> entryHistory)
: BaseViewModel(context)
{
    public SafeGroup Group { get; } = group;
    public Guid EntryId { get; } = safeEntryId;
    public IEnumerable<SafeEntry> EntryHistory { get; } = entryHistory;
}
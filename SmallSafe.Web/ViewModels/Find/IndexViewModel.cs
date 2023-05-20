using SmallSafe.Secure.Model;

namespace SmallSafe.Web.ViewModels.Find;

public class IndexViewModel : BaseViewModel
{
    public IndexViewModel(HttpContext context, string find,
        IEnumerable<SafeGroup> foundGroups,
        IEnumerable<(SafeGroup Group, SafeEntry Entry)> foundEntries) : base(context, find)
    {
        FoundGroups = foundGroups;
        FoundEntries = foundEntries;
    }

    public IEnumerable<SafeGroup> FoundGroups { get; init; }
    public IEnumerable<(SafeGroup Group, SafeEntry Entry)> FoundEntries { get; init; }
}
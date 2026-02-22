namespace SmallSafe.Secure.Model;

public class SafeGroup
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string? Name { get; set; }
    public List<SafeEntry>? Entries { get; set; }
    public List<SafeEntry>? EntriesHistory { get; set; }
    public bool PreserveHistory { get; set; } = true;
    public DateTime CreatedTimestamp { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedTimestamp { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedTimestamp { get; set; }
}
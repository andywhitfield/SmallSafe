namespace SmallSafe.Secure.Model;

public class SafeEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string? Name { get; set; }
    public string? EntryValue { get; set; }
    public DateTime CreatedTimestamp { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedTimestamp { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedTimestamp { get; set; }
}

namespace SmallSafe.Secure.Model;

public class SafeGroup
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string? Name { get; set; }
    public List<SafeEntry>? Entries { get; set; }
}
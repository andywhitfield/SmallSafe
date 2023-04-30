namespace SmallSafe.Secure.Model;

public class SafeEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string? Name { get; set; }
    public string? EncryptedValue { get; set; }
    public byte[]? IV { get; set; }
    public byte[]? Salt { get; set; }
}
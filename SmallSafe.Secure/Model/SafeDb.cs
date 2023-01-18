namespace SmallSafe.Secure.Model;

public class SafeDb
{
    public byte[]? IV { get; set; }
    public byte[]? Salt { get; set; }
    public string? EncryptedSafeGroups { get; set; }
}
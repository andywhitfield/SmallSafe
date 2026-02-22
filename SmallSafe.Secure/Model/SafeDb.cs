namespace SmallSafe.Secure.Model;

public class SafeDb
{
    public byte[]? IV { get; set; }
    public byte[]? Salt { get; set; }
    public byte[]? EncryptedSafeGroups { get; set; }
}
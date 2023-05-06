namespace SmallSafe.Secure.Dictionary;

public interface IWordDictionary
{
    ICollection<string> Words { get; }
    Task<int> LoadAsync();
}
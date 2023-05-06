using System.Reflection;
using System.Text;

namespace SmallSafe.Secure.Dictionary;

public class WordDictionary : IWordDictionary
{
    private const string BaseResourcePath = "SmallSafe.Secure.Dictionary.";

    private static readonly string[] _dictionaryFiles = new[] {
        BaseResourcePath + "data.adj",
        BaseResourcePath + "data.adv",
        BaseResourcePath + "data.noun",
        BaseResourcePath + "data.verb"
    };

    private List<string>? _words;

    public ICollection<string> Words => (ICollection<string>?) _words ?? Array.Empty<string>();

    public async Task<int> LoadAsync()
    {
        List<string> dictWords = new(150000);
        var assem = typeof(WordDictionary).GetTypeInfo().Assembly;
        StringBuilder word = new();
        foreach (var dict in _dictionaryFiles)
        {
            using StreamReader dictStream = new(assem.GetManifestResourceStream(dict) ?? throw new InvalidOperationException($"Cannot load word dictionary resource {dict}"));

            string? line;
            while ((line = await dictStream.ReadLineAsync()) != null)
            {
                // lots of magic numbers...
                // the dictionary line has 16 characters of preamble, followed
                // by a space (index 16), then the word followed by a space.
                if (line.Length < 18 || line[0] == ' ' || line[16] != ' ')
                    continue;

                word.Clear();
                foreach (var c in line.Skip(17))
                {
                    if (c == ' ')
                        break;

                    word.Append(c == '_' ? ' ' : c);
                }
                dictWords.Add(word.ToString());
            }
        }
        _words = dictWords;
        return _words.Count;
    }
}
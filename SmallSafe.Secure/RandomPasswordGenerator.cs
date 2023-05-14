using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using SmallSafe.Secure.Dictionary;

namespace SmallSafe.Secure;

public class RandomPasswordGenerator : IRandomPasswordGenerator
{
    private static readonly RandomNumberGenerator _rng = RandomNumberGenerator.Create();
    private readonly IWordDictionary _dictionary;
    private readonly HashSet<string> _endPunctuation = new() { ",", ".", "!", "?", "$", "*", ":", ";" };
    private readonly HashSet<string> _middlePunctuation = new() { " ", ",", "-", "/", "'", "\"", "$", "%", "@", "(", ")", "&", "*", ":", ";" };
    private readonly HashSet<string> _allNumbers = Enumerable.Range(0, 10).Select(i => i.ToString(CultureInfo.InvariantCulture)).ToHashSet();

    public RandomPasswordGenerator(IWordDictionary dictionary) => _dictionary = dictionary;

    public bool AllowNumbers { get; set; } = true;
    public bool AllowPunctuation { get; set; } = true;
    public int MinimumLength { get; set; } = 12;
    public int MaximumLength { get; set; } = 0;

    public string Generate()
    {
        StringBuilder password = new();
        if (AllowNumbers)
        {
            password.Append(WordFrom(_allNumbers));

            if (AllowPunctuation)
                password.Append(WordFrom(_middlePunctuation));
        }

        var addNumber = true;
        var wordsAdded = 0;
        var minPasswordLength = Math.Max(5, MinimumLength);
        if (MaximumLength > 0 && minPasswordLength > MaximumLength)
            minPasswordLength = MaximumLength;

        var maxPasswordLength = MaximumLength <= 0 ? int.MaxValue : Math.Max(MaximumLength, minPasswordLength);
        if (AllowPunctuation)
            maxPasswordLength--;

        do
        {
            if (!addNumber)
            {
                if (AllowPunctuation)
                    password.Append(wordsAdded % 2 == 0 ? WordFrom(_middlePunctuation) : " ");
                else if (AllowNumbers && wordsAdded % 2 == 0)
                    password.Append(WordFrom(_allNumbers));
            }

            if (password.Length >= maxPasswordLength)
                break;

            string? wordToAdd;
            while (
                (wordToAdd = WordFrom(_dictionary.Words, maxWordLength: maxPasswordLength - password.Length)) == null ||
                (!AllowNumbers && wordToAdd.Any(char.IsDigit)) ||
                (!AllowPunctuation && wordToAdd.Any(c => !char.IsLetterOrDigit(c)))) { }
            password.Append(wordToAdd);

            if (password.Length >= maxPasswordLength)
                break;

            addNumber = false;
            wordsAdded++;
        } while (password.Length < minPasswordLength);

        if (AllowPunctuation && password.Length < maxPasswordLength + 1)
            password.Append(WordFrom(_endPunctuation));

        return password.ToString();
    }
    private string? WordFrom(ICollection<string> words, bool checkFirstCharacter = true, string? excluding = null, int? maxWordLength = null)
    {
        IEnumerable<string> eligibleWords = words;
        if (maxWordLength.HasValue && maxWordLength > 0)
            eligibleWords = words.Where(w => w.Length <= maxWordLength).ToList();

        if (!eligibleWords.Any())
            return null;

        string word;
        do
        {
            word = eligibleWords.ElementAt(NextRandom(eligibleWords.Count()));
            if (word.Length == 0)
                continue;

            if (AllowPunctuation)
            {
                word = word.Replace('_', ' ');
            }
            else
            {
                StringBuilder wordSb = new(word);
                var idx = 0;
                while (idx < wordSb.Length)
                {
                    if (!char.IsLetterOrDigit(wordSb[idx]))
                        wordSb.Remove(idx, 1);
                    else
                        idx++;
                }
                word = wordSb.ToString();
            }

            if (char.IsLetter(word[0]))
                word = char.ToUpper(word[0]) + word.Substring(1);
        } while (word.Length == 0 && word != excluding);

        return word;
    }

    private static int NextRandom(int setSize)
    {
        var randomBuffer = new byte[4];
        _rng.GetBytes(randomBuffer);
        var val = BitConverter.ToInt32(randomBuffer, 0) & 0x7fffffff;
        return val % setSize;
    }
}
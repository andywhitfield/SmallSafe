using SmallSafe.Secure.Dictionary;

namespace SmallSafe.Secure.Test;

[TestClass]
public class RandomPasswordGeneratorTest
{
    [TestMethod]
    public async Task GeneratePasswordsWithExpectedLengths()
    {
        WordDictionary dict = new();
        await dict.LoadAsync();
        RandomPasswordGenerator randomPasswordGenerator = new(dict);

        randomPasswordGenerator.MaximumLength = 0;
        randomPasswordGenerator.MinimumLength = 12;
        foreach (var password in GeneratePasswords(randomPasswordGenerator))
            Assert.IsGreaterThanOrEqualTo(12, password.Length);


        randomPasswordGenerator.MinimumLength = 24;
        foreach (var password in GeneratePasswords(randomPasswordGenerator))
            Assert.IsGreaterThanOrEqualTo(24, password.Length);


        randomPasswordGenerator.MinimumLength = 10;
        randomPasswordGenerator.MaximumLength = 15;
        foreach (var password in GeneratePasswords(randomPasswordGenerator))
            Assert.IsTrue(password.Length >= 10 && password.Length <= 15);


        randomPasswordGenerator.MinimumLength = 20;
        randomPasswordGenerator.MaximumLength = 30;
        foreach (var password in GeneratePasswords(randomPasswordGenerator))
            Assert.IsTrue(password.Length >= 20 && password.Length <= 30);
    }

    [TestMethod]
    public async Task GeneratePasswordsWithAllowedValues()
    {
        WordDictionary dict = new();
        await dict.LoadAsync();
        RandomPasswordGenerator randomPasswordGenerator = new(dict);

        randomPasswordGenerator.AllowNumbers = false;
        foreach (var password in GeneratePasswords(randomPasswordGenerator))
            Assert.IsFalse(password.Any(c => char.IsDigit(c)), $"Password contains number! ${password}");

        randomPasswordGenerator.AllowNumbers = true;
        randomPasswordGenerator.AllowPunctuation = false;
        foreach (var password in GeneratePasswords(randomPasswordGenerator))
            Assert.IsTrue(password.All(c => char.IsLetterOrDigit(c)), $"Password contains punctunation! ${password}");
    }

    private IEnumerable<string> GeneratePasswords(RandomPasswordGenerator generator) =>
        Enumerable.Range(1, 100).Select(_ => generator.Generate());
}
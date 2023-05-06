namespace SmallSafe.Secure;

public interface IRandomPasswordGenerator
{
    bool AllowNumbers { get; set; }
    bool AllowPunctuation { get; set; }
    int MinimumLength { get; set; }
    int MaximumLength { get; set; }

    string Generate();
}
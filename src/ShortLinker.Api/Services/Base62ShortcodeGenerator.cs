namespace ShortLinker.Api.Services;

public class Base62ShortcodeGenerator : IShortcodeGenerator
{
    private const string Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
    private readonly Random _random = new();

    public string Generate()
    {
        return new string(Enumerable.Repeat(Alphabet, 6)
            .Select(s => s[_random.Next(s.Length)]).ToArray());
    }
}
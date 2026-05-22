using System.Text.RegularExpressions;

namespace PokeChat.NLP;

public static class Tokenizer
{
    private static readonly Regex TokenRegex = new(@"\b[\w'-+\$%&]+\b|[.,!?;:()\""]", RegexOptions.Compiled);

    public static List<string> Tokenize(string input)
    {
        var tokens = new List<string>();
        var matches = TokenRegex.Matches(input.ToLowerInvariant());
        foreach (Match match in matches)
        {
            tokens.Add(match.Value);
        }
        return tokens;
    }
}

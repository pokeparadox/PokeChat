using System.Text.RegularExpressions;

namespace PokeChat.NLP;

public class Tokeniser : ITokeniser
{
    private static readonly Regex TokenRegex = new(@"\b[\w'\-+$%&]+\b|[.,!?;:()\""]", RegexOptions.Compiled);
    private readonly ContractionExpander? _expander;

    public Tokeniser(ContractionExpander? expander = null)
    {
        _expander = expander;
    }

    public List<string> Tokenise(string input)
    {
        var expanded = _expander != null ? _expander.Expand(input) : input;
        var tokens = new List<string>();
        var matches = TokenRegex.Matches(expanded.ToLowerInvariant());
        foreach (Match match in matches)
        {
            tokens.Add(match.Value);
        }
        return tokens;
    }
}

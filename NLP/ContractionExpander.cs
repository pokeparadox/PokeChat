using System.Text.RegularExpressions;

namespace PokeChat.NLP;

public class ContractionExpander
{
    private readonly Dictionary<string, string> _expansions;

    public ContractionExpander(IEnumerable<KeyValuePair<string, string>> expansions)
    {
        _expansions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in expansions)
        {
            _expansions[kvp.Key] = kvp.Value;
        }
    }

    public string Expand(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        var result = input;
        foreach (var (contraction, expansion) in _expansions)
        {
            var pattern = @"\b" + Regex.Escape(contraction) + @"\b";
            result = Regex.Replace(result, pattern, expansion, RegexOptions.IgnoreCase);
        }

        return result;
    }
}

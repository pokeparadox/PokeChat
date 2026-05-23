namespace PokeChat.NLP;

public class SpellChecker
{
    private HashSet<string> _dictionary;
    private Dictionary<string, string> _misspellings;

    public SpellChecker()
    {
        _dictionary = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        _misspellings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    public void Initialize(HashSet<string> dictionary, Dictionary<string, string> misspellings)
    {
        _dictionary = dictionary;
        _misspellings = misspellings;
    }

    public void AddToDictionary(string word)
    {
        _dictionary.Add(word.ToLowerInvariant());
    }

    public List<string> AutoCorrect(List<string> tokens)
    {
        var corrected = new List<string>(tokens.Count);

        foreach (var token in tokens)
        {
            if (IsPunctuation(token))
            {
                corrected.Add(token);
                continue;
            }

            if (_misspellings.TryGetValue(token, out var correction))
            {
                corrected.Add(correction);
            }
            else
            {
                corrected.Add(token);
            }
        }

        return corrected;
    }

    public List<string> GetUnknownWords(List<string> tokens)
    {
        var unknown = new List<string>();

        foreach (var token in tokens)
        {
            if (IsPunctuation(token))
                continue;

            if (token.Length > 0 && char.IsDigit(token[0]))
                continue;

            if (!_dictionary.Contains(token))
            {
                unknown.Add(token);
            }
        }

        return unknown;
    }

    public bool HasSuggestions(string word)
    {
        return SuggestCorrections(word, 2).Count > 0;
    }

    public List<string> SuggestCorrections(string word, int maxDistance = 2)
    {
        var suggestions = new List<(string Word, int Distance)>();

        foreach (var dictWord in _dictionary)
        {
            var distance = LevenshteinDistance(word.ToLowerInvariant(), dictWord.ToLowerInvariant());
            if (distance <= maxDistance && distance > 0)
            {
                suggestions.Add((dictWord, distance));
            }
        }

        return suggestions
            .OrderBy(s => s.Distance)
            .ThenBy(s => s.Word)
            .Select(s => s.Word)
            .Take(5)
            .ToList();
    }

    private static int LevenshteinDistance(string a, string b)
    {
        var lenA = a.Length;
        var lenB = b.Length;

        var matrix = new int[lenA + 1, lenB + 1];

        for (int i = 0; i <= lenA; i++)
            matrix[i, 0] = i;

        for (int j = 0; j <= lenB; j++)
            matrix[0, j] = j;

        for (int i = 1; i <= lenA; i++)
        {
            for (int j = 1; j <= lenB; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;

                matrix[i, j] = Math.Min(
                    Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
                    matrix[i - 1, j - 1] + cost);
            }
        }

        return matrix[lenA, lenB];
    }

    private static bool IsPunctuation(string token)
    {
        return token is "." or "," or "!" or "?" or ";" or ":" or "(" or ")" or "\"" or "'";
    }
}

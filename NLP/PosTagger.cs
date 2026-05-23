using PokeChat.Data.Entities;

namespace PokeChat.NLP;

public enum PosTag
{
    Noun,
    ProperNoun,
    Verb,
    Adjective,
    Pronoun,
    Determiner,
    Preposition,
    Conjunction,
    Adverb,
    Punctuation,
    Unknown
}

public static class PosTagger
{
    private static Dictionary<string, PosTag>? _wordTagMap;
    public static void Reset()
    {
        _wordTagMap = null;
    }

    public static void Initialize(IEnumerable<PosDictionaryEntry> entries)
    {
        _wordTagMap = new Dictionary<string, PosTag>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in entries)
        {
            var tag = entry.WordType.ToLowerInvariant() switch
            {
                "pronoun" => PosTag.Pronoun,
                "determiner" => PosTag.Determiner,
                "preposition" => PosTag.Preposition,
                "conjunction" => PosTag.Conjunction,
                "verb" => PosTag.Verb,
                "adjective" => PosTag.Adjective,
                "noun" => PosTag.Noun,
                _ => PosTag.Unknown
            };

            _wordTagMap.TryAdd(entry.Word, tag);
        }
    }

    public static Dictionary<string, PosTag> Tag(List<string> tokens)
    {
        var tags = new Dictionary<string, PosTag>();

        for (int i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];
            var tag = GetTag(token, i, tokens);
            tags[token] = tag;
        }

        return tags;
    }

    private static PosTag GetTag(string token, int index, List<string> tokens)
    {
        if (IsPunctuation(token))
            return PosTag.Punctuation;

        if (_wordTagMap != null && _wordTagMap.TryGetValue(token, out var knownTag))
        {
            return knownTag;
        }

        if (token.EndsWith("ing") || token.EndsWith("ed") || token.EndsWith("en"))
        {
            return PosTag.Verb;
        }

        if (token.EndsWith("s") && token.Length > 1)
        {
            var singular = token.Substring(0, token.Length - 1);
            if (_wordTagMap != null && _wordTagMap.TryGetValue(singular, out var singularTag) && singularTag == PosTag.Verb)
            {
                return PosTag.Verb;
            }
        }

        if (token.Length > 0 && char.IsUpper(tokens[index][0]) && index > 0 &&
            tokens[index - 1] != "." && tokens[index - 1] != "!" && tokens[index - 1] != "?")
        {
            return PosTag.ProperNoun;
        }

        if (token.Length > 0 && char.IsUpper(tokens[index][0]))
            return PosTag.ProperNoun;

        return PosTag.Unknown;
    }

    private static bool IsPunctuation(string token)
    {
        return token is "." or "," or "!" or "?" or ";" or ":" or "(" or ")" or "\"" or "'";
    }
}

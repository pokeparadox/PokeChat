using PokeChat.Data.Entities;

namespace PokeChat.NLP;

public enum PosTag
{
    Noun,
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

public class PosTagger : IPosTagger
{
    private readonly Dictionary<string, PosTag> _wordTagMap;

    public PosTagger(IEnumerable<PosDictionaryEntry> entries)
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

    public List<PosTag> Tag(List<string> tokens)
    {
        var tags = new List<PosTag>(tokens.Count);

        for (int i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];
            var tag = GetTag(token, i, tokens);
            tags.Add(tag);
        }

        return tags;
    }

    private PosTag GetTag(string token, int index, List<string> tokens)
    {
        if (PunctuationHelper.IsPunctuation(token))
            return PosTag.Punctuation;

        if (_wordTagMap.TryGetValue(token, out var knownTag))
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
            if (_wordTagMap.TryGetValue(singular, out var singularTag) && singularTag == PosTag.Verb)
            {
                return PosTag.Verb;
            }
        }

        var pluralSingular = Pluraliser.ToSingular(token);
        if (pluralSingular != null && _wordTagMap.TryGetValue(pluralSingular, out var pluralTag) && pluralTag == PosTag.Noun)
        {
            return PosTag.Noun;
        }

        return PosTag.Unknown;
    }
}
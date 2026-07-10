using PokeChat.Knowledge;

namespace PokeChat.Stories;

public class RhymeMatcher
{
    private readonly KnowledgeStore _knowledgeStore;

    private static readonly HashSet<char> Vowels = new() { 'a', 'e', 'i', 'o', 'u' };

    public RhymeMatcher(KnowledgeStore knowledgeStore)
    {
        _knowledgeStore = knowledgeStore;
    }

    public string ExtractRhymeKey(string word)
    {
        var lower = word.Trim().ToLowerInvariant();
        if (lower.Length < 2) return lower;

        var lastVowelIdx = -1;
        for (var i = lower.Length - 1; i >= 0; i--)
        {
            if (Vowels.Contains(lower[i]))
            {
                if (lower[i] == 'e' && i == lower.Length - 1 && lower.Length > 2)
                {
                    for (var j = i - 1; j >= 0; j--)
                    {
                        if (Vowels.Contains(lower[j]))
                        {
                            lastVowelIdx = j;
                            break;
                        }
                    }
                    if (lastVowelIdx >= 0) break;
                }
                lastVowelIdx = i;
                break;
            }
            if (lower[i] == 'y' && i > 0)
            {
                lastVowelIdx = i;
                break;
            }
        }

        if (lastVowelIdx < 0) return lower;

        return lower[lastVowelIdx..];
    }

    public string? FindRhymeWord(string word, string wordType, int? syllableCount = null)
    {
        var rhymeKey = ExtractRhymeKey(word);
        var candidates = _knowledgeStore.GetRhymeGroupWords(rhymeKey, wordType);

        if (candidates.Count == 0)
            return null;

        var filtered = candidates
            .Where(c => !string.Equals(c, word, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (filtered.Count == 0)
            return null;

        if (syllableCount.HasValue)
        {
            var matchingSyllables = filtered
                .Where(c => SyllableCounter.Count(c) == syllableCount.Value)
                .ToList();
            if (matchingSyllables.Count > 0)
                filtered = matchingSyllables;
        }

        return filtered[Random.Shared.Next(filtered.Count)];
    }
}

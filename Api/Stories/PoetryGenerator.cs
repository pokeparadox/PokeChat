using System.Text.RegularExpressions;
using PokeChat.Knowledge;

namespace PokeChat.Stories;

public class PoetryGenerator
{
    private readonly KnowledgeStore _knowledgeStore;
    private readonly RhymeMatcher _rhymeMatcher;

    private static readonly Regex SlotPattern = new(@"\{(\w+?)(?:_(\d+))?\}(\w+)?", RegexOptions.Compiled);
    private static readonly HashSet<char> Vowels = new() { 'a', 'e', 'i', 'o', 'u' };

    public PoetryGenerator(KnowledgeStore knowledgeStore, RhymeMatcher? rhymeMatcher = null)
    {
        _knowledgeStore = knowledgeStore;
        _rhymeMatcher = rhymeMatcher ?? new RhymeMatcher(knowledgeStore);
    }

    public string? GenerateHaiku(string? userName = null, int? userId = null)
    {
        var templates = _knowledgeStore.GetPoemTemplates("haiku");
        if (templates.Count == 0) return null;

        var template = templates[Random.Shared.Next(templates.Count)].Template;
        return ResolvePoemTemplate(template, userName, userId);
    }

    public string? GenerateLimerick(string? userName = null, int? userId = null)
    {
        var templates = _knowledgeStore.GetPoemTemplates("limerick");
        if (templates.Count == 0) return null;

        var template = templates[Random.Shared.Next(templates.Count)].Template;
        return ResolvePoemTemplate(template, userName, userId);
    }

    private string ResolvePoemTemplate(string template, string? userName, int? userId)
    {
        var resolvedLines = new List<string>();
        var usedRhymeA = new List<string>();
        var usedRhymeB = new List<string>();

        foreach (var line in template.Split('\n'))
        {
            var resolvedLine = SlotPattern.Replace(line.Trim(), match =>
            {
                var fullSlot = match.Groups[1].Value;
                var numStr = match.Groups[2].Value;
                var num = string.IsNullOrEmpty(numStr) ? 0 : int.Parse(numStr);
                var suffix = match.Groups[3].Value;

                var resolved = ResolveSlot(fullSlot, num, userName, userId, usedRhymeA, usedRhymeB);
                return ApplySuffix(resolved, suffix);
            });

            resolvedLines.Add(resolvedLine);
        }

        return string.Join("\n", resolvedLines);
    }

    private string ResolveSlot(string slot, int num, string? userName, int? userId,
        List<string> usedRhymeA, List<string> usedRhymeB)
    {
        return slot switch
        {
            "noun" => PickWord("noun", num) ?? GenerationUtils.FallbackNouns[Random.Shared.Next(GenerationUtils.FallbackNouns.Length)],
            "verb" => PickWord("verb", num) ?? GenerationUtils.FallbackVerbs[Random.Shared.Next(GenerationUtils.FallbackVerbs.Length)],
            "adj" => PickWord("adjective", num) ?? GenerationUtils.FallbackAdjs[Random.Shared.Next(GenerationUtils.FallbackAdjs.Length)],
            "adv" => PickWord("adverb", num) ?? GenerationUtils.FallbackAdverbs[Random.Shared.Next(GenerationUtils.FallbackAdverbs.Length)],
            "prep" => PickWord("preposition", num) ?? "in",
            "art" => Random.Shared.Next(2) == 0 ? "a" : "the",
            "pron" => Random.Shared.Next(3) switch { 0 => "he", 1 => "she", _ => "they" },
            "det" => PickDeterminer(),
            "pronoun" => Random.Shared.Next(3) switch { 0 => "my", 1 => "your", _ => "their" },
            "user" => !string.IsNullOrEmpty(userName) ? userName : "someone",
            "place" => _knowledgeStore.GetRandomNounByCategory("place") ?? GenerationUtils.FallbackPlaces[Random.Shared.Next(GenerationUtils.FallbackPlaces.Length)],
            "number" => Random.Shared.Next(1, 101).ToString(),
            "verb_2ing" => ToGerund(PickWord("verb", 2) ?? "sing"),
            "a_rhyme" => PickRhymeWord("noun", num, usedRhymeA),
            "b_rhyme" => PickRhymeWord("noun", num, usedRhymeB),
            _ => $"{{{slot}}}"
        };
    }

    private string? PickWord(string wordType, int syllables)
    {
        if (syllables <= 0)
        {
            var word = _knowledgeStore.GetRandomWord(wordType);
            if (wordType == "verb" && word != null && GenerationUtils.ExcludedVerbs.Contains(word))
                return null;
            if (wordType == "adjective" && word != null && word.EndsWith("ed", StringComparison.OrdinalIgnoreCase))
                return null;
            return word;
        }

        var words = _knowledgeStore.GetWordsByTypeAndSyllables(wordType, syllables);
        if (wordType == "verb")
            words = words.Where(w => !GenerationUtils.ExcludedVerbs.Contains(w)).ToList();
        if (wordType == "adjective")
            words = words.Where(w => !w.EndsWith("ed", StringComparison.OrdinalIgnoreCase)).ToList();
        if (words.Count > 0)
            return words[Random.Shared.Next(words.Count)];

        return null;
    }

    private string PickRhymeWord(string wordType, int syllables, List<string> usedRhymer)
    {
        if (usedRhymer.Count > 0)
        {
            var last = usedRhymer[^1];
            var rhyme = _rhymeMatcher.FindRhymeWord(last, wordType, syllables > 0 ? syllables : null);
            if (rhyme != null && !usedRhymer.Contains(rhyme))
            {
                usedRhymer.Add(rhyme);
                return rhyme;
            }
        }

        var rhymeGroupWords = _knowledgeStore.GetAllRhymeGroupWords(wordType);
        if (rhymeGroupWords.Count > 0)
        {
            var available = rhymeGroupWords
                .Where(w => !usedRhymer.Contains(w))
                .ToList();
            if (available.Count > 0)
            {
                if (syllables > 0)
                {
                    var matching = available.Where(w => SyllableCounter.Count(w) == syllables).ToList();
                    if (matching.Count > 0)
                        available = matching;
                }
                var pick = available[Random.Shared.Next(available.Count)];
                usedRhymer.Add(pick);
                return pick;
            }
        }

        var candidates = _knowledgeStore.GetWordsByTypeAndSyllables(wordType, syllables > 0 ? syllables : 1);
        if (candidates.Count > 0)
        {
            var pick = candidates[Random.Shared.Next(candidates.Count)];
            usedRhymer.Add(pick);
            return pick;
        }

        var fallback = GenerationUtils.FallbackNouns[Random.Shared.Next(GenerationUtils.FallbackNouns.Length)];
        usedRhymer.Add(fallback);
        return fallback;
    }

    private static string PickDeterminer()
    {
        return Random.Shared.Next(3) switch
        {
            0 => "this",
            1 => "that",
            _ => "some"
        };
    }

    private static string ApplySuffix(string word, string suffix)
    {
        if (string.IsNullOrEmpty(suffix))
            return word;

        return suffix.ToLowerInvariant() switch
        {
            "ing" => ToGerund(word),
            "s" => ToThirdPerson(word),
            "ed" => ToPastTense(word),
            _ => word + suffix
        };
    }

    private static string ToThirdPerson(string word)
    {
        var lower = word.ToLowerInvariant();
        if (lower.EndsWith("s") || lower.EndsWith("sh") || lower.EndsWith("ch") ||
            lower.EndsWith("x") || lower.EndsWith("z") || lower.EndsWith("o"))
            return word + "es";
        if (lower.EndsWith("y") && lower.Length > 2 && !"aeiou".Contains(lower[lower.Length - 2]))
            return word[..^1] + "ies";
        return word + "s";
    }

    private static string ToPastTense(string word)
    {
        var lower = word.ToLowerInvariant();
        if (lower.EndsWith("e"))
            return word + "d";
        if (lower.EndsWith("y") && lower.Length > 2 && !"aeiou".Contains(lower[lower.Length - 2]))
            return word[..^1] + "ied";
        return word + "ed";
    }

    private static string ToGerund(string word)
    {
        var lower = word.ToLowerInvariant();
        if (lower.EndsWith("e") && lower.Length > 2 && !lower.EndsWith("ee"))
            return word[..^1] + "ing";
        return word + "ing";
    }
}

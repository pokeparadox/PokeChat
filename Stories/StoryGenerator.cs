using System.Text.RegularExpressions;
using PokeChat.Knowledge;
using PokeChat.NLP;

namespace PokeChat.Stories;

public class StoryGenerator
{
    private readonly KnowledgeStore _knowledgeStore;
    private static readonly Regex SlotPattern = new(@"\{(\w+)\}(ing)?", RegexOptions.Compiled);
    private static readonly string[] FallbackNames = { "Finn", "Luna", "Asher", "Nova", "Riley", "Sam", "Jordan", "Avery" };
    private static readonly string[] FallbackNouns = { "treasure", "quest", "door", "key", "garden", "river", "star", "cave" };
    private static readonly string[] FallbackVerbs = { "explore", "discover", "sing", "dance", "run", "fly", "shine", "dream" };
    private static readonly string[] FallbackAdjs = { "mysterious", "brave", "golden", "ancient", "magical", "dark" };
    private static readonly string[] FallbackAdverbs = { "quickly", "silently", "boldly", "gently", "suddenly" };
    private static readonly string[] FallbackPlaces = { "forest", "mountain", "ocean", "village", "castle", "valley" };

    public StoryGenerator(KnowledgeStore knowledgeStore)
    {
        _knowledgeStore = knowledgeStore;
    }

    public string GenerateStory(string? userName = null, int? userId = null)
    {
        var templates = _knowledgeStore.GetStoryTemplates();
        if (templates.Count == 0)
            return string.Empty;

        var template = templates[Random.Shared.Next(templates.Count)].Template;
        var usedSlots = new HashSet<string>();

        var result = SlotPattern.Replace(template, match =>
        {
            var slot = match.Groups[1].Value.ToLowerInvariant();
            var hasIng = match.Groups[2].Success;
            var resolved = ResolveSlot(slot, userName, userId, usedSlots);
            if (hasIng)
                resolved = ToGerund(resolved);
            return resolved;
        });

        return result;
    }

    private static string ToGerund(string word)
    {
        var lower = word.ToLowerInvariant();
        if (lower.EndsWith("e") && lower.Length > 2 && !lower.EndsWith("ee"))
            return word[..^1] + "ing";
        return word + "ing";
    }

    private string ResolveSlot(string slot, string? userName, int? userId, HashSet<string> usedSlots)
    {
        if (usedSlots.Contains(slot))
            return ResolveFreshSlot(slot, userName, userId);

        usedSlots.Add(slot);
        return ResolveFreshSlot(slot, userName, userId);
    }

    private string ResolveFreshSlot(string slot, string? userName, int? userId)
    {
        return slot switch
        {
            "noun" => _knowledgeStore.GetRandomWord("noun") ?? FallbackNouns[Random.Shared.Next(FallbackNouns.Length)],
            "noun_plural" => PluraliseNoun(_knowledgeStore.GetRandomWord("noun") ?? FallbackNouns[Random.Shared.Next(FallbackNouns.Length)]),
            "verb" => _knowledgeStore.GetRandomWord("verb") ?? FallbackVerbs[Random.Shared.Next(FallbackVerbs.Length)],
            "adj" => _knowledgeStore.GetRandomWord("adjective") ?? FallbackAdjs[Random.Shared.Next(FallbackAdjs.Length)],
            "adverb" => _knowledgeStore.GetRandomWord("adverb") ?? FallbackAdverbs[Random.Shared.Next(FallbackAdverbs.Length)],
            "place" => _knowledgeStore.GetRandomNounByCategory("place") ?? FallbackPlaces[Random.Shared.Next(FallbackPlaces.Length)],
            "character" => GetRandomCharacter(),
            "user" => !string.IsNullOrEmpty(userName) ? userName : "someone",
            "user_like" => ResolveUserLike(userId),
            "number" => Random.Shared.Next(1, 1001).ToString(),
            "a_noun" => AddArticle(_knowledgeStore.GetRandomWord("noun") ?? FallbackNouns[Random.Shared.Next(FallbackNouns.Length)]),
            "a_adj" => AddArticle(_knowledgeStore.GetRandomWord("adjective") ?? FallbackAdjs[Random.Shared.Next(FallbackAdjs.Length)]),
            _ => $"{{{slot}}}"
        };
    }

    private string GetRandomCharacter()
    {
        var name = _knowledgeStore.GetRandomName();
        return name ?? FallbackNames[Random.Shared.Next(FallbackNames.Length)];
    }

    private string ResolveUserLike(int? userId)
    {
        if (userId.HasValue)
        {
            var fact = _knowledgeStore.GetRandomUserFact(userId.Value);
            if (fact != null && !string.IsNullOrEmpty(fact.Object))
                return fact.Object;
        }

        return _knowledgeStore.GetRandomWord("noun") ?? FallbackNouns[Random.Shared.Next(FallbackNouns.Length)];
    }

    private static string PluraliseNoun(string noun)
    {
        var singular = Pluraliser.ToSingular(noun);
        var baseWord = singular ?? noun;
        return Pluralise(baseWord);
    }

    private static string Pluralise(string word)
    {
        var lower = word.ToLowerInvariant();
        if (lower.EndsWith("s") || lower.EndsWith("sh") || lower.EndsWith("ch") ||
            lower.EndsWith("x") || lower.EndsWith("z") || lower.EndsWith("o"))
            return word + "es";
        if (lower.EndsWith("y") && lower.Length > 2 && !"aeiou".Contains(lower[lower.Length - 2]))
            return word[..^1] + "ies";
        if (lower.EndsWith("f"))
            return word[..^1] + "ves";
        if (lower.EndsWith("fe"))
            return word[..^2] + "ves";
        return word + "s";
    }

    private static string AddArticle(string noun)
    {
        var lower = noun.ToLowerInvariant();
        var isVowel = lower.Length > 0 && "aeiou".Contains(lower[0]);
        return isVowel ? "an " + noun : "a " + noun;
    }
}

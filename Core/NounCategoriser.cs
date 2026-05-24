using PokeChat.Knowledge;

namespace PokeChat.Core;

public class NounCategoriser : INounCategoriser
{
    private readonly KnowledgeStore _knowledgeStore;

    private static readonly HashSet<string> CommonNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "alice", "bob", "charlie", "david", "emma", "frank", "grace",
        "henry", "isabella", "jack", "kate", "liam", "mary", "noah",
        "olivia", "paul", "quinn", "rachel", "sam", "tina", "uma",
        "victor", "wendy", "xavier", "yvonne", "zach"
    };

    private static readonly string[] PlaceSuffixes =
        ["ville", "town", "burg", "shire", "land", "city", "ton", "bury"];

    public NounCategoriser(KnowledgeStore knowledgeStore)
    {
        _knowledgeStore = knowledgeStore;
    }

    public string CategoriseNoun(string noun)
    {
        var lower = noun.ToLowerInvariant();

        var existing = _knowledgeStore.CategoriseNoun(lower);
        if (existing != null)
            return existing;

        var category = InferCategory(lower);

        _knowledgeStore.AddNounCategory(lower, category);

        return category;
    }

    private static string InferCategory(string noun)
    {
        if (CommonNames.Contains(noun))
            return "person";

        foreach (var suffix in PlaceSuffixes)
        {
            if (noun.EndsWith(suffix))
                return "place";
        }

        return "thing";
    }
}

namespace PokeChat.NLP;

public static class Pluraliser
{
    public static string? ToSingular(string word)
    {
        return GetCandidates(word).FirstOrDefault();
    }

    public static IEnumerable<string> GetCandidates(string word)
    {
        var lower = word.ToLowerInvariant();

        if (IrregularPlurals.TryGetValue(lower, out var singular))
        {
            yield return singular;
            yield break;
        }

        if (lower.EndsWith("ies") && lower.Length > 4)
        {
            yield return lower[..^3] + "y";
            yield return lower[..^3] + "ie";
        }

        if (lower.EndsWith("ves") && lower.Length > 4)
        {
            yield return lower[..^3] + "f";
            yield return lower[..^3] + "fe";
        }

        if (lower.EndsWith("es") && lower.Length > 3)
        {
            var stem = lower[..^2];
            if (stem.EndsWith("s") || stem.EndsWith("sh") ||
                stem.EndsWith("ch") || stem.EndsWith("x") ||
                stem.EndsWith("z") || stem.EndsWith("o"))
                yield return stem;
        }

        if (lower.EndsWith("s") && lower.Length > 2)
        {
            var candidate = lower[..^1];
            if (candidate.Length >= 2)
                yield return candidate;
        }
    }

    private static readonly Dictionary<string, string> IrregularPlurals = new()
    {
        { "children", "child" },
        { "men", "man" },
        { "women", "woman" },
        { "people", "person" },
        { "teeth", "tooth" },
        { "feet", "foot" },
        { "mice", "mouse" },
        { "geese", "goose" },
        { "oxen", "ox" },
        { "sheep", "sheep" },
        { "deer", "deer" },
        { "fish", "fish" },
        { "species", "species" },
        { "series", "series" },
    };
}

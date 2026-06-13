using System.Collections.Generic;

namespace PokeChat.Stories;

public static class GenerationUtils
{
    public static readonly HashSet<string> ExcludedVerbs = new(StringComparer.OrdinalIgnoreCase)
    {
        "can", "could", "may", "might", "must", "shall", "should", "will", "would",
        "be", "am", "is", "are", "was", "were", "been", "being",
        "have", "has", "had", "do", "does", "did", "doing", "done",
        "get", "got", "gotten", "make", "made", "let", "put"
    };

    public static readonly string[] FallbackNames = { "Finn", "Luna", "Asher", "Nova", "Riley", "Sam", "Jordan", "Avery" };
    public static readonly string[] FallbackNouns = { "treasure", "quest", "door", "key", "garden", "river", "star", "cave" };
    public static readonly string[] FallbackVerbs = { "explore", "discover", "sing", "dance", "run", "fly", "shine", "dream" };
    public static readonly string[] FallbackAdjs = { "mysterious", "brave", "golden", "ancient", "magical", "dark" };
    public static readonly string[] FallbackAdverbs = { "quickly", "silently", "boldly", "gently", "suddenly" };
    public static readonly string[] FallbackPlaces = { "forest", "mountain", "ocean", "village", "castle", "valley" };
}

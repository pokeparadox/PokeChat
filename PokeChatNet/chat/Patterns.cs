namespace PokeChatNet
{
    public static class Patterns
    {
        public static readonly Pattern Action = PatternQueries.SelectOrInsert("Action");
        public static readonly Pattern Describe = PatternQueries.SelectOrInsert("Describe");
    }
}


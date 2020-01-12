namespace PokeChatNet
{
    public static class PhraseTypes
    {
        public static readonly PhraseType Doing = PhraseTypeQueries.SelectOrInsert("Doing");
        public static readonly PhraseType Greeting = PhraseTypeQueries.SelectOrInsert("Greeting");
        public static readonly PhraseType Question = PhraseTypeQueries.SelectOrInsert("Question");
        public static readonly PhraseType Description = PhraseTypeQueries.SelectOrInsert("Description");
    }
}


namespace PokeChatNet
{
    public static class WordTypes
    {
        public static readonly WordType Unknown = WordTypeQueries.SelectOrInsert("Unknown");
        public static readonly WordType Greeting = WordTypeQueries.SelectOrInsert("Greeting");
        public static readonly WordType Question = WordTypeQueries.SelectOrInsert("Question");
        public static readonly WordType Pronoun = WordTypeQueries.SelectOrInsert("Pronoun");
        public static readonly WordType Noun = WordTypeQueries.SelectOrInsert("Noun");
        public static readonly WordType Determiner = WordTypeQueries.SelectOrInsert("Determiner");
        public static readonly WordType Verb = WordTypeQueries.SelectOrInsert("Verb");
        public static readonly WordType VerbI = WordTypeQueries.SelectOrInsert("VerbI");
        public static readonly WordType VerbYou = WordTypeQueries.SelectOrInsert("VerbYou");
        public static readonly WordType VerbHe = WordTypeQueries.SelectOrInsert("VerbHe");
        public static readonly WordType VerbShe = WordTypeQueries.SelectOrInsert("VerbShe");
        public static readonly WordType VerbIt = WordTypeQueries.SelectOrInsert("VerbIt");
        public static readonly WordType VerbWe = WordTypeQueries.SelectOrInsert("VerbWe");
        public static readonly WordType VerbThey = WordTypeQueries.SelectOrInsert("VerbThey");
        public static readonly WordType Adjective = WordTypeQueries.SelectOrInsert("Adjective");
        public static readonly WordType PresentParticiple = WordTypeQueries.SelectOrInsert("PresentParticiple");
    }
}


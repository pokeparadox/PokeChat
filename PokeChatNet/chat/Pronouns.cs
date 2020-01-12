namespace PokeChatNet
{
    public static class Pronouns
    {
        public static readonly Word I = WordQueries.SelectOrInsert("I", WordTypes.Pronoun);
        public static readonly Word You = WordQueries.SelectOrInsert("you", WordTypes.Pronoun);
        public static readonly Word He = WordQueries.SelectOrInsert("he", WordTypes.Pronoun);
        public static readonly Word She = WordQueries.SelectOrInsert("she", WordTypes.Pronoun);
        public static readonly Word It = WordQueries.SelectOrInsert("it", WordTypes.Pronoun);
        public static readonly Word We = WordQueries.SelectOrInsert("we", WordTypes.Pronoun);
        public static readonly Word They = WordQueries.SelectOrInsert("they", WordTypes.Pronoun);
    }
}


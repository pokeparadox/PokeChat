using System.Collections.Generic;

namespace PokeChatNet
{
    public static class PatternQueries
    {
        const string Table = "Pattern";
        public static bool Exists(string name)
        {
            return Queries.Exists(Table, name);
        }

        public static Pattern Select(string name)
        {
            return Queries.SelectRow<Pattern>(Table, name);
        }

        public static Pattern Select(int id)
        {
            return Queries.SelectRow<Pattern>(Table, id);
        }

        public static int Insert(string name)
        {
            return Queries.Insert(Table, name);
        }

        public static Pattern SelectOrInsert(string name)
        {
            return Queries.SelectOrInsert<Pattern>(Table, name);
        }
    }
}


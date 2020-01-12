using System.Collections.Generic;

namespace PokeChatNet
{
    public static class BadSpellingQueries
    {
        const string Table = "BadSpelling";
        public static bool Exists(string spelling)
        {
            return Queries.Exists(Table, spelling);
        }
       
        public static bool Exists(int id)
        {
            return Queries.Exists(Table, id);
        }

        public static List<BadSpelling> Select()
        {
            return Queries.SelectTable<BadSpelling>(Table);
        }

        public static BadSpelling Select(string name)
        {
            return Queries.SelectRow<BadSpelling>(Table, name);
        }

        public static BadSpelling Select(int id)
        {
            return Queries.SelectRow<BadSpelling>(Table, id);
        }

        public static int Insert(string badSpelling, int wordId)
        {
            var f = new QueryFilter(Columns.Name, badSpelling);
            f.Add(Columns.WordId, wordId);
            return Queries.Insert(Table, f);
        }
    }
}


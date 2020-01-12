using System.Collections.Generic;

namespace PokeChatNet
{
    public static class PhraseTypeQueries
    {
        const string _table = "PhraseType";
        public static bool Exists(string name)
        {
            return Queries.Exists(_table, name);
        }

        public static bool Exists(int id)
        {
            return Queries.Exists(_table, id);
        }

        public static List<PhraseType> Select()
        {
            return Queries.SelectTable<PhraseType>(_table);
        }

        public static PhraseType Select(string name)
        {
            return Queries.SelectRow<PhraseType>(_table, name);
        }

        public static PhraseType Select(int id)
        {
            return Queries.SelectRow<PhraseType>(_table, id);
        }

        public static int Insert(string name)
        {
            return Queries.Insert(_table,name);
        }

        public static PhraseType SelectOrInsert(string name)
        {
            return Queries.SelectOrInsert<PhraseType>(_table, name);
        }
    }
}


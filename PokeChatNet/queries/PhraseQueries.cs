using System.Collections.Generic;

namespace PokeChatNet
{
    public static class PhraseQueries
    {
        const string _table = "Phrase";
        public static bool Exists(string name)
        {
            return Queries.Exists(_table, name);
        }

        public static bool Exists(int  id)
        {
            return Queries.Exists(_table, id);
        }

        public static List<Phrase> Select()
        {
            return Queries.SelectTable<Phrase>(_table);
        }

        public static Phrase Select(string phrase)
        {
            return Queries.SelectRow<Phrase>(_table, phrase);
        }

        public static Phrase Select(int id)
        {
            return Queries.SelectRow<Phrase>(_table, id);
        }

        public static int Insert(string name)
        {
            return Queries.Insert(_table, name);
        }
    }
}


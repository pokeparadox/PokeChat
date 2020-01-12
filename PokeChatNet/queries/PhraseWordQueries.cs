using System.Collections.Generic;

namespace PokeChatNet
{
    public static class PhraseWordQueries
    {
        const string _table = "PhraseWord";
        public static bool Exists(string name)
        {
            return Queries.Exists(_table, name);
        }

        public static bool Exists(int id)
        {
            return Queries.Exists(_table, id);
        }

        public static List<PhraseWord> Select()
        {
            return Queries.SelectTable<PhraseWord>(_table);
        }

        public static PhraseWord Select(int row)
        {
            return Queries.SelectRow<PhraseWord>(_table,row);
        }

        public static List<PhraseWord> SelectByPhraseId(int phraseId)
        {
            var f = new QueryFilter(Columns.PhraseId, phraseId);
            return Queries.Select(_table, f).ToPhraseWords();
        }

        public static int Insert(int phraseId, int wordId)
        {
            var f = new QueryFilter(Columns.PhraseId, phraseId);
            f.Add(Columns.WordId, wordId);
            return Queries.Insert(_table, f);
        }
    }

    public static class PhraseWordExtensions
    {
        public static List<PhraseWord> ToPhraseWords(this QueryResult r)
        {
            return r.ToDataList<PhraseWord>();
        }
    }
}


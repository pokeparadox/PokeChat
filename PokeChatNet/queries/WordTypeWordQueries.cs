using System.Collections.Generic;

namespace PokeChatNet
{
    public static class WordTypeWordQueries
    {
        const string Table = "WordTypeWord";

        public static bool Exists(int id)
        {
            return Queries.Exists(Table, id);
        }

        public static bool Exists(int wordTypeId, int wordId)
        {
            var f = new QueryFilter(Columns.WordTypeId, wordTypeId);
            f.Add(Columns.WordId, wordId);
            return Queries.Exists(Table, f);
        }

        public static bool Exists(WordType wt, Word word)
        {
            return Exists(wt.Id, word.Id);
        }

        public static bool Exists(string wordType, string word)
        {
            var wt = WordTypeQueries.Select(wordType);
            var w = WordQueries.Select(word);
            if (wt != null && w != null)
            {
                return Exists(wt,w);
            }

            return false;
        }

        public static List<WordTypeWord> Select()
        {
            return Queries.SelectTable<WordTypeWord>(Table);
        }

        public static WordTypeWord Select(int id)
        {
            return Queries.SelectRow<WordTypeWord>(Table, id);
        }

        public static List<WordTypeWord> SelectByWordType(int wordTypeId)
        {
            var f = new QueryFilter(Columns.WordTypeId, wordTypeId);
            return Queries.Select(Table, f).ToDataList<WordTypeWord>();
        }

        public static WordTypeWord Select(int wordTypeId, int wordId)
        {
            var f = new QueryFilter(Columns.WordTypeId, wordTypeId);
            f.Add(Columns.WordId, wordId);
            return Queries.Select(Table, f).ToData<WordTypeWord>();
        }

        public static WordTypeWord Select(WordType wt, Word w)
        {
            if (wt != null && w != null)
            {
                return Select(wt.Id, w.Id);
            }

            return null;
        }

        public static WordTypeWord Select(string wordType, string word)
        {
            var wt = WordTypeQueries.Select(wordType);
            var w = WordQueries.Select(word);
            return Select(wt,w);
        }

        public static int Insert(int wordTypeId, int wordId)
        {
            var f = new QueryFilter(Columns.WordTypeId, wordTypeId);
            f.Add(Columns.WordId, wordId);
            return Queries.Insert(Table, f);
        }
    }
}


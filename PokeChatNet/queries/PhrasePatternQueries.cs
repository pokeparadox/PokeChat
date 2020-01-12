using System.Collections.Generic;
using System.Linq;

namespace PokeChatNet
{
    /*
        public int Position { get; set;}
        public int WordTypeId { get; set;}
        public int PhraseTypeId { get; set;}
        public int PatternId { get; set;}
     */

    public static class PhrasePatternQueries
    {
        const string Table = "PhrasePattern";
        public static bool Exists(int patternId, int phraseTypeId, int wordTypeId)
        {
            return Queries.Exists(Table, PatternIdPhraseTypeIdWordTypeId(patternId,phraseTypeId, wordTypeId));
        }

        public static bool Exists(int id)
        {
            return Queries.Exists(Table, id);
        }

        public static bool Exists(int patternId, int position)
        {
            return Queries.Exists(Table, PatternIdPosition(patternId, position));
        }

        public static List<PhrasePattern> Select()
        {
            return Queries.SelectTable<PhrasePattern>(Table);
        }

        public static List<PhrasePattern> SelectByPattern(int patternId)
        {
            return Queries.Select(Table, PatternId(patternId)).ToDataList<PhrasePattern>().OrderBy(x => x.Position).ToList();
        }

        public static PhrasePattern Select(int id)
        {
            return Queries.SelectRow<PhrasePattern>(Table, id);
        }

        public static List<PhrasePattern> SelectByPhraseType(int phraseTypeId)
        {
            var f = new QueryFilter(Columns.PhraseTypeId, phraseTypeId);
            return Queries.Select(Table,f).ToDataList<PhrasePattern>().OrderBy(x => x.Position).ToList();
        }

        public static List<PhrasePattern> SelectOrInsert(Pattern pattern, PhraseType phraseType, List<WordType> wordTypes)
        {
            return SelectOrInsert(pattern.Id, phraseType.Id, wordTypes.ConvertAll(x => x.Id));
        }

        public static List<PhrasePattern> SelectOrInsert(int patternId, int phraseTypeId, List<int> wordTypeIds)
        {
            List<PhrasePattern> phrasePattern = new List<PhrasePattern>();
            foreach (int i in wordTypeIds)
            {
                phrasePattern.Add(SelectOrInsert(patternId, phraseTypeId, i));
            }
            return phrasePattern.OrderBy(x => x.Position).ToList();
        }

        static PhrasePattern Select(int patternId, int phraseTypeId, int wordTypeId)
        {
            var f = PatternIdPhraseTypeIdWordTypeId(patternId, phraseTypeId, wordTypeId);
            return Queries.Select(Table,f).ToData<PhrasePattern>();
        }

        static int Insert(int patternId, int phraseTypeId, int wordTypeId)
        {
            var f = PatternIdPhraseTypeIdWordTypeId(patternId, phraseTypeId, wordTypeId);
            f.Add(Columns.Position, Count(patternId));
            return Queries.Insert(Table, f);
        }

        static int Count(int patternId)
        {
            var rows = SelectByPattern(patternId);
            if (rows != null)
            {
                return rows.Count;
            }

            return 0;
        }

        static PhrasePattern SelectOrInsert(int patternId, int phraseTypeId, int wordTypeId)
        {
            if (Exists(patternId, phraseTypeId, wordTypeId))
            {
                return Select(patternId, phraseTypeId, wordTypeId);
            }

            int i = Insert(patternId, phraseTypeId, wordTypeId);
            return Select(i);
        }
       
        /// Converters
        static QueryFilter PatternId(int patternId)
        {
            return new QueryFilter(Columns.PatternId,patternId);
        }

        static QueryFilter PatternIdPosition(int patternId, int position)
        {
            var f = PatternId(patternId);
            f.Add(Columns.Position,position);
            return f;
        }

        static QueryFilter PatternIdPhraseTypeId(int patternId, int phraseTypeId)
        {
            var f = PatternId(patternId);
            f.Add(Columns.PhraseTypeId, phraseTypeId);
            return f;
        }
        static QueryFilter PatternIdPhraseTypeIdWordTypeId(int patternId, int phraseTypeId, int wordTypeId)
        {
            var f = PatternIdPhraseTypeId(patternId,phraseTypeId);
            f.Add(Columns.WordTypeId, wordTypeId);
            return f;
        }
    }
}


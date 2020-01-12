using System.Collections.Generic;
using System.Linq;

namespace PokeChatNet
{
	public static class WordQueries
	{
		const string _table = "Word";
		public static List<Word> Select()
		{
			return Queries.SelectTable<Word>(_table);
		}

        public static Word Select(int id)
        {
            return Queries.SelectRow<Word>(_table,id);
        }

        public static Word Select(string name)
        {
            return Queries.SelectRow<Word>(_table,name);
        }

        public static List<Word> SelectByWordType(string wordType)
        {
            var wt = WordTypeQueries.Select(wordType);
            if (wt != null)
            {
                var wtw = WordTypeWordQueries.SelectByWordType(wt.Id);
                if (wtw != null)
                {
                    var f = new QueryFilter();
                    f.Add(Columns.Id, wtw.ConvertAll(x => x.WordId));
                    return Queries.Select(_table, f).ToDataList<Word>();
                }
            }

            return null;
        }

        public static Word Select(string word, string wordType)
        {
            var wtw = WordTypeWordQueries.Select(wordType, word);
            if (wtw != null)
            {
                return Select(wtw.WordId);
            }

            return null;
        }

        public static List<Word> Select(List<Synonym> synonyms)
        {
            var ids = synonyms.ConvertAll(x => x.AltWordId);
            if (ids.Any())
            {
                var f = new QueryFilter(_table, ids);
                return Queries.Select(_table, f).ToWords();
            }

            return null;
        }

        public static List<Word> Select(List<PhraseWord> phraseWords)
        {
            var ids = phraseWords.ConvertAll(x => x.WordId);
            if (ids.Any())
            {
                var f = new QueryFilter(_table, ids);
                return Queries.Select(_table, f).ToWords();
            }

            return null;
        }

        public static bool Exists(string name)
        {
            return Queries.Exists(_table, name);
        }

        public static int Insert(string name, float sayWeight, float seeWeight)
        {
            var f = new QueryFilter();
            f.Add(Columns.Name, name);
            f.Add(Columns.SayWeighting, sayWeight);
            f.Add(Columns.SeeWeighting, seeWeight);
            return SqliteAccess.Db.InsertValueByFilter(_table, f);
        }

        public static Word SelectOrInsert(string name)
        {
            if (Exists(name))
            {
                return Select(name);
            }
            int id = Insert(name, 0.0f, 0.0f);
            return Select(id);
        }

        public static Word SelectOrInsert(string name, WordType wordType)
        {
            return SelectOrInsert(name, wordType.Id);  
        }

        public static Word SelectOrInsert(string name, int wordTypeId)
        {
            var wt = WordTypeQueries.Select(wordTypeId);
            if (wt != null)
            {
                if (WordTypeWordQueries.Exists(wt.Name, name))
                {
                    var wtw = WordTypeWordQueries.Select(wt.Name, name);
                    if (wtw != null)
                    {
                        return Select(wtw.WordId);
                    }
                }
                var w = SelectOrInsert(name);

                if (w.Id > 0)
                {
                    int id = WordTypeWordQueries.Insert(wt.Id, w.Id);
                    var wtWord = WordTypeWordQueries.Select(id);
                    if (wtWord != null)
                    {
                        return Select(wtWord.WordId);
                    }
                }
            }

            return SelectOrInsert(name);
        }
	}

	public static class WordExtensions
	{
		public static List<Word> ToWords(this QueryResult result)
		{
            return result.ToDataList<Word>();
		}
	}
}


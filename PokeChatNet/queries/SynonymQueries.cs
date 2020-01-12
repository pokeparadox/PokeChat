using System.Collections.Generic;

namespace PokeChatNet
{
    public static class SynonymQueries
    {
        const string Table= "Synonym";
        public static List<Synonym> Select()
        {
            return Queries.SelectTable<Synonym>(Table);
        }

        public static Synonym Select(string name)
        {
            return Queries.SelectRow<Synonym>(Table, name);
        }

        public static Synonym Select(int id)
        {
            return Queries.SelectRow<Synonym>(Table, id);
        }

        public static List<Synonym> SelectFromWord(string word)
        {
            var w = WordQueries.Select(word);
            if (w != null)
            {
                var f = new QueryFilter(Columns.WordId, w.Id);
                return SqliteAccess.Db.SelectByFilter(Table, f).ToSynonyms();
            }

            return null;
        }
    }
    
    public static class SynonymnExtensions
    {
        public static List<Synonym> ToSynonyms(this QueryResult result)
        {
            return result.ToDataList<Synonym>();
        }
    }
}


using System.Collections.Generic;

namespace PokeChatNet
{
    public static class WordTypeQueries
    {
        const string _table = "WordType";
        public static bool Exists(string name)
        {
            return Queries.Exists(_table, name);
        }

        public static List<WordType> SelectWord()
        {
            return Queries.SelectTable<WordType>(_table);
        }

        public static WordType SelectOrInsert(string name)
        {
            return Queries.SelectOrInsert<WordType>(_table, name);
        }

        public static WordType Select(string name)
        {
            return Queries.SelectRow<WordType>(_table, name);
        }

        public static WordType Select(int id)
        {
            return Queries.SelectRow<WordType>(_table, id);
        }

        public static int Insert(string name)
        {
            return Queries.Insert(_table, name);
        }
    }

   /* public static class WordTypeExtensions
    {
        public static WordType ToWordType(this QueryResult result)
        {
            return result.ToFirstData<WordType>();
        }

        public static List<WordType> ToWordTypes(this QueryResult result)
        {
            return result.ToDataList<WordType>();
        }
    }*/
}

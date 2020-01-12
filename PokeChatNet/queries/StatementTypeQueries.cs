using System.Collections.Generic;

namespace PokeChatNet
{
    public static class StatementTypeQueries
    {
        const string _table = "StatementType";
        public static bool Exists(string name)
        {
            return Queries.Exists(_table, name);
        }

        public static bool Exists(int id)
        {
            return Queries.Exists(_table, id);
        }

        public static List<StatementType> Select()
        {
            return Queries.SelectTable<StatementType>(_table);
        }

        public static StatementType Select(string name)
        {
            return Queries.SelectRow<StatementType>(_table, name);
        }

        public static StatementType Select(int id)
        {
            return Queries.SelectRow<StatementType>(_table, id);
        }

        public static int Insert(string name)
        {
            return Queries.Insert(_table, name);
        }

        public static StatementType SelectOrInsert(string name)
        {
            return Queries.SelectOrInsert<StatementType>(_table, name);
        }
    }

    public static class StatementTypeExtensions
    {
        /*public static StatementType ToStatementType(this QueryResult result)
        {
            return result.ToFirstData<StatementType>();
        }

        public static List<StatementType> ToStatementTypes(this QueryResult result)
        {
            return result.ToDataList<StatementType>();
        }*/
    }
}

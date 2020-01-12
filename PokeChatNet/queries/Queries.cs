using System;
using System.Collections.Generic;
namespace PokeChatNet
{
    public static class Queries
    {
        public static List<T> SelectTable<T>(string table) where T : DataIndex, new()
        {
            return SqliteAccess.Db.SelectTable(table).ToDataList<T>();
        }

        public static T SelectRow<T>(string table, int id) where T : DataIndex, new()
        {
            return SqliteAccess.Db.SelectByFilter(table, new QueryFilter(Columns.Id, id)).ToData<T>();
        }

        public static T SelectRow<T>(string table, string name) where T : DataIndex, new()
        {
            return SqliteAccess.Db.SelectByFilter(table, new QueryFilter(Columns.Name, name)).ToData<T>();
        }

        public static QueryResult Select(string table, QueryFilter filter)
        {
            return SqliteAccess.Db.SelectByFilter(table, filter);
        }

        public static bool Exists(string table, string name)
        {
            return Exists(table, new QueryFilter(Columns.Name, name));
        }

        public static bool Exists(string table, int id)
        {
            return Exists(table, new QueryFilter(Columns.Id, id));
        }

        public static bool Exists(string table, QueryFilter filter)
        {
            return SqliteAccess.Db.ExistsQuery(table, filter);
        }

        public static int Insert(string table, string name)
        {
            return Insert(table, new QueryFilter(Columns.Name, name));
        }

        public static int Insert(string table, QueryFilter filter)
        {
            return SqliteAccess.Db.InsertValueByFilter(table, filter);
        }

        public static T SelectOrInsert<T>(string table, string name) where T : DataIndex, new()
        {
            if (Exists(table, name))
            {
                return SelectRow<T>(table,name);
            }
            int id = Insert(table, name);

            return SelectRow<T>(table,id);
        }
    }

    public static class GeneralQueryExtensions
    {
        public static List<T> ToDataList<T>(this QueryResult result) where T : DataIndex, new()
        {
            var output = new List<T>();
            for (int i = 0; i < result.Count; ++i) 
            {
                var r = result.SelectRow(i);
                var w = new T();
                w.SetData(r);
                output.Add (w);
            }
            return output;
        }

        public static T ToData<T>(this QueryResult result) where T : DataIndex, new()
        {
            if (result.Any())
            {
                var output = new T();
                output.SetData(result.SelectRow(0));
                return output;
            }

            return default(T);
        }
    }
}


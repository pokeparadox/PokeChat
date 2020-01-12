using System;
using Mono.Data.Sqlite;

namespace PokeChatNet
{
	public class SqliteDriver
	{
		public SqliteDriver()
		{
			_fileName = string.Empty;
		}

		public SqliteDriver(string fileName)
		{
			_fileName = fileName;
		}

		public string FileName
		{
			set{_fileName = value;}
		}
       
        public QueryResult SelectByFilter(string table, QueryFilter filter)
        {
            return SelectQuery(Space(_select, _all, _from, table, _where, filter.ToString()));
        }

		public QueryResult SelectTable(string table)
		{
			return SelectQuery(Space(_select,_all,_from,table));
		}

		public QueryResult SelectQuery(string query)
		{
            using (var con = Connect)
            {
                con.Open ();
                var cmd = con.CreateCommand ();
                cmd.CommandText = query;
                var reader = cmd.ExecuteReader ();
                var result = new QueryResult (reader);
                con.Close ();

                return result;
            }
        }

        public bool ExistsQuery(string table, QueryFilter filter)
        {
            using (var con = Connect)
            {
                con.Open ();
                var cmd = con.CreateCommand ();
                cmd.CommandText = Space(_select,"1",_from, table, _where, filter.ToString());
                var result = cmd.ExecuteScalar();
                con.Close ();

                return result != null && result.ToString() != "0";
            }
        }

        public int InsertValueByFilter(string table, QueryFilter filter)
        {
            return InsertUpdateQuery(Space(_insert, _into, table, Columns(filter.Columns()),Values(filter.Values())));
        }

        public int InsertUpdateQuery(string query)
        {
            using (var con = Connect)
            {
                con.Open ();
                var cmd = con.CreateCommand();
                cmd.CommandText = query;
                cmd.ExecuteNonQuery();
                int returnId = LastUpdatedRowId(con);
                con.Close ();
                return returnId;
            }
        }

        private int LastUpdatedRowId(SqliteConnection con)
        {                             
            var cmd = con.CreateCommand();
            cmd.CommandText = Space(_select, "last_insert_rowid()");
            long id = (long)cmd.ExecuteScalar();               
            return (int)id;
        }

		private SqliteConnection Connect
		{
			get{ return new SqliteConnection("Data Source=" + _fileName);}
		}
		private string _fileName;

		private string Space(params string[] strings)
		{
			return string.Join (_space, strings); 
		}

        private string Csv(params string[] values)
        {
            return string.Join (",", values); 
        }

        private string Columns(params string [] columns)
        {
            return Space(" (",Csv(columns),") ");
        }
        private string Values(params string [] values)
        {
            return Space(" VALUES(",Csv(values),") ");
        }

		private const string _space = " ";
		private const string _all = "*";
		private const string _select = "SELECT";
		private const string _insert = "INSERT";
        private const string _into = "INTO";
		private const string _from = "FROM";
		private const string _where = "WHERE";
	}
}


using System.Collections.Generic;
using Mono.Data.Sqlite;

namespace PokeChatNet
{
	public class QueryResult
	{
        public QueryResult()
        {

        }

		public QueryResult ( SqliteDataReader reader)		
		{
			if (reader.HasRows) 
			{
				_rows = new List<Dictionary<string, string>> ();
				while (reader.Read()) 
				{
					var dict = new Dictionary<string,string> ();
					for (int i = 0; i < reader.FieldCount; ++ i) 
					{
						string key = reader.GetName (i);
						dict [key] = reader.GetValue(i).ToString();
					}

					_rows.Add (dict);
				}
			}
		}

		public string SelectValue(int row, string column)
		{
			var r = SelectRow (row);
			if (r != null) 
			{
				if (r.ContainsKey (column)) 
				{
					return r [column];
				}
			}

			return string.Empty;
		}

		public Dictionary<string, string> SelectRow(int row)
		{
			if (Count > 0 && Count > row) 
			{
				return _rows [row];
			}

			return null;
		}

		public List<string> ColumnNames 
		{
			get
			{
				if (Any ()) 
				{
					var row = SelectRow (0);
					return new List<string>(row.Keys); 
				}
				return null;
			}
		}
		 
		public bool Any() 
		{
			return (Count > 0 && _rows.Count > 0);
		}
		
		public bool Empty { get { return !Any(); } }
		public int Count {get{return _rows != null ? _rows.Count: 0;}}
		private List<Dictionary<string, string>> _rows;
	}
}


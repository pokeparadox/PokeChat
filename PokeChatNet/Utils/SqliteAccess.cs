using System;

namespace PokeChatNet
{

	public class SqliteAccess
	{
		public static SqliteDriver Db
		{
			get
			{
				return Singleton<SqliteDriver>.Instance;
			}
		}
	}
}


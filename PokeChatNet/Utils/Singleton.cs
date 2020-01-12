using System;

namespace PokeChatNet
{
	public class Singleton<T> where T: class, new()
	{
		public static T Instance {get{return _instance.Value;}}

		private Singleton ()
		{
		}

		private static readonly Lazy<T> _instance = new Lazy<T> (() => new T ());
	}
}

using System;

namespace PokeChatNet
{
	class MainClass
	{
		public static void Main (string[] args)
		{
			//
			//var word = WordQueries.SelectWords();
			//Console.WriteLine ("Hello World!");
            var chat = new PokeChat();
            chat.Chat();
		}
	}
}

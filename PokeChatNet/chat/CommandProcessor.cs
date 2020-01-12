using System;

namespace PokeChatNet
{
    public static class CommandProcessor
    {
        public static bool Running = true;

        public static bool Process(string text)
        {
            return text.StartsWith("@", StringComparison.Ordinal) && CheckKeyWords(text);
        }

        static bool CheckKeyWords(string text)
        {
            bool ranCommand = false;
            if (ShouldQuit(text))
            {
                Running = false;
                ranCommand = true;
            }

            return ranCommand;
        }
        static bool ShouldQuit(string text)
        {
            return string.Equals(text, @"exit") || string.Equals(text, "@quit") || string.Equals(text, "@leave");
        }
    }
}


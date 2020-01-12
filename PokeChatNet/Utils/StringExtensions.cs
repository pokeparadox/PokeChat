using System;

namespace PokeChatNet
{
    public static class StringExtensions
    {
        public static int ToInt(this string val)
        {
            return Convert.ToInt32(val);
        }

        public static float ToFloat(this string  val)
        {
            return Convert.ToSingle(val);
        }

        public static string TrimEnd(this string val, string toTrim)
        {
            if (!val.EndsWith(toTrim, StringComparison.Ordinal))
            {
                return val;
            }

            return val.Remove(val.LastIndexOf(toTrim, StringComparison.Ordinal));
        }
    }
}


namespace PokeChat.NLP;

internal static class PunctuationHelper
{
    public static bool IsPunctuation(string token)
    {
        return token is "." or "," or "!" or "?" or ";" or ":" or "(" or ")" or "\"" or "'";
    }
}

using System.Text;

namespace PokeChat.NLP;

public static class SentenceSplitter
{
    private static readonly char[] SentenceEndings = { '.', '!', '?' };

    public static List<string> Split(string input)
    {
        var sentences = new List<string>();
        var current = new StringBuilder();

        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];
            current.Append(c);

            if (SentenceEndings.Contains(c))
            {
                bool isEndOfSentence = true;

                if (c == '.' && i + 1 < input.Length)
                {
                    char next = input[i + 1];
                    if (char.IsLower(next))
                    {
                        isEndOfSentence = false;
                    }

                    string commonAbbr = current.ToString().Trim().ToLowerInvariant();
                    if (commonAbbr is "mr" or "mrs" or "ms" or "dr" or "prof" or "st" or "jr" or "sr" or "vs" or "etc" or "inc" or "ltd" or "co" or "corp" or "dept" or "est" or "approx" or "avg" or "approx.")
                    {
                        isEndOfSentence = false;
                    }
                }

                if (isEndOfSentence)
                {
                    var sentence = current.ToString().Trim();
                    if (!string.IsNullOrEmpty(sentence))
                    {
                        sentences.Add(sentence);
                    }
                    current.Clear();
                }
            }
        }

        var remaining = current.ToString().Trim();
        if (!string.IsNullOrEmpty(remaining))
        {
            sentences.Add(remaining);
        }

        return sentences;
    }
}

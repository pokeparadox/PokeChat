using System.Text;

namespace PokeChat.NLP;

public class SentenceSplitter : ISentenceSplitter
{
    private static readonly char[] SentenceEndings = { '.', '!', '?' };

    public List<string> Split(string input)
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
                    if (char.IsLower(next) || char.IsDigit(next))
                    {
                        isEndOfSentence = false;
                    }

                    if (i > 0 && char.IsDigit(input[i - 1]))
                    {
                        isEndOfSentence = false;
                    }

                    string commonAbbr = current.ToString().Trim().TrimEnd('.').ToLowerInvariant();
                    if (commonAbbr is "mr" or "mrs" or "ms" or "dr" or "prof" or "st" or "jr" or "sr" or "vs" or "etc" or "inc" or "ltd" or "co" or "corp" or "dept" or "est" or "approx" or "avg")
                    {
                        isEndOfSentence = false;
                    }
                }

                if (isEndOfSentence)
                {
                    while (i + 1 < input.Length && SentenceEndings.Contains(input[i + 1]))
                    {
                        i++;
                        current.Append(input[i]);
                    }

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

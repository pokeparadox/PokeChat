namespace PokeChat.NLP;

public record SvoTriple(string Subject, string Verb, string @Object);

public static class SvoExtractor
{
    public static List<SvoTriple> Extract(List<string> tokens, Dictionary<string, PosTag> tags)
    {
        var triples = new List<SvoTriple>();
        var verbIndices = new List<int>();

        for (int i = 0; i < tokens.Count; i++)
        {
            if (tags.TryGetValue(tokens[i], out var tag) && tag == PosTag.Verb)
            {
                verbIndices.Add(i);
            }
        }

        foreach (var verbIdx in verbIndices)
        {
            var subject = ExtractSubject(tokens, tags, verbIdx);
            var obj = ExtractObject(tokens, tags, verbIdx);
            var verb = tokens[verbIdx];

            if (!string.IsNullOrEmpty(subject) && !string.IsNullOrEmpty(obj))
            {
                triples.Add(new SvoTriple(subject, verb, obj));
            }
        }

        return triples;
    }

    private static string ExtractSubject(List<string> tokens, Dictionary<string, PosTag> tags, int verbIdx)
    {
        var subjectTokens = new List<string>();

        for (int i = verbIdx - 1; i >= 0; i--)
        {
            var token = tokens[i];
            if (!tags.TryGetValue(token, out var tag))
                break;

            if (tag == PosTag.Punctuation)
                break;

            if (tag == PosTag.Verb)
                break;

            subjectTokens.Insert(0, token);
        }

        return string.Join(" ", subjectTokens);
    }

    private static string ExtractObject(List<string> tokens, Dictionary<string, PosTag> tags, int verbIdx)
    {
        var objectTokens = new List<string>();

        for (int i = verbIdx + 1; i < tokens.Count; i++)
        {
            var token = tokens[i];
            if (!tags.TryGetValue(token, out var tag))
                continue;

            if (tag == PosTag.Punctuation)
                break;

            if (tag == PosTag.Verb && objectTokens.Count > 0)
                break;

            objectTokens.Add(token);
        }

        return string.Join(" ", objectTokens);
    }
}

namespace PokeChat.NLP;

public record SvoTriple(string Subject, string Verb, string @Object);

public class SvoExtractor : ISvoExtractor
{
    public List<SvoTriple> Extract(List<string> tokens, List<PosTag> tags)
    {
        var triples = new List<SvoTriple>();
        var verbIndices = new List<int>();

        for (int i = 0; i < tags.Count; i++)
        {
            if (tags[i] == PosTag.Verb)
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

    private static string ExtractSubject(List<string> tokens, List<PosTag> tags, int verbIdx)
    {
        var subjectTokens = new List<string>();

        for (int i = verbIdx - 1; i >= 0; i--)
        {
            var tag = tags[i];

            if (tag == PosTag.Punctuation)
                break;

            if (tag == PosTag.Verb)
                break;

            subjectTokens.Insert(0, tokens[i]);
        }

        return string.Join(" ", subjectTokens);
    }

    private static string ExtractObject(List<string> tokens, List<PosTag> tags, int verbIdx)
    {
        var objectTokens = new List<string>();

        for (int i = verbIdx + 1; i < tags.Count; i++)
        {
            var tag = tags[i];

            if (tag == PosTag.Punctuation)
                break;

            if (tag == PosTag.Verb && objectTokens.Count > 0)
                break;

            objectTokens.Add(tokens[i]);
        }

        return string.Join(" ", objectTokens);
    }
}

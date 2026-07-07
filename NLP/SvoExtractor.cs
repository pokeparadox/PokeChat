using System.Text.RegularExpressions;

namespace PokeChat.NLP;

public record SvoTriple(string Subject, string Verb, string @Object);

public class SvoExtractor : ISvoExtractor
{
    private static readonly HashSet<string> SubjectStopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "yes", "no", "yeah", "nope", "nah", "oh", "ah", "well", "so",
        "the", "a", "an"
    };

    private static readonly HashSet<string> ObjectStopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "a", "an"
    };

    private static readonly HashSet<string> ClauseMarkers = new(StringComparer.OrdinalIgnoreCase)
    {
        "and", "or", "but", "so", "because", "although", "however",
        "therefore", "meanwhile", "nevertheless", "nonetheless", "furthermore"
    };

    private static readonly Regex GarbageObjectPattern = new(
        @"^(the|a|an|this|that|these|those)\s+(the|a|an|this|that|these|those|and|or)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

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

            if (string.IsNullOrEmpty(subject) || string.IsNullOrEmpty(obj))
                continue;

            var subjLower = subject.ToLowerInvariant();
            if (subjLower is "a" or "an" or "the" or "what" or "when" or "where" or "why"
                or "who" or "whom" or "whose" or "which" or "how")
                continue;

            if (GarbageObjectPattern.IsMatch(obj))
                continue;

            var objLower = obj.ToLowerInvariant();
            objLower = StripClauseTrailing(objLower);
            if (objLower.Contains(" and the ") || objLower.Contains(" or the ") ||
                objLower.Contains(" and a ") || objLower.Contains(" or a "))
                continue;

            if (subjLower.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length > 5)
                continue;
            if (objLower.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length > 5)
                continue;

            triples.Add(new SvoTriple(subject, verb, obj));
        }

        return triples;
    }

    private static string StripClauseTrailing(string obj)
    {
        foreach (var marker in ClauseMarkers)
        {
            var markerIdx = obj.LastIndexOf(" " + marker, StringComparison.OrdinalIgnoreCase);
            if (markerIdx > 0)
            {
                var afterMarker = obj[(markerIdx + marker.Length + 1)..].Trim();
                if (afterMarker.Length > 0 && afterMarker.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length <= 2)
                {
                    obj = obj[..markerIdx].TrimEnd();
                }
            }
        }

        var leadingMarkerIdx = obj.IndexOf(' ');
        if (leadingMarkerIdx > 0)
        {
            var firstWord = obj[..leadingMarkerIdx];
            if (ClauseMarkers.Contains(firstWord))
            {
                obj = obj[(leadingMarkerIdx + 1)..].Trim();
            }
        }

        return obj;
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

        while (subjectTokens.Count > 0 && SubjectStopWords.Contains(subjectTokens[0]))
            subjectTokens.RemoveAt(0);

        while (subjectTokens.Count > 0 && (SubjectStopWords.Contains(subjectTokens[^1]) || subjectTokens[^1] is "of" or "for" or "in" or "on" or "at" or "by" or "with"))
            subjectTokens.RemoveAt(subjectTokens.Count - 1);

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

            if (ClauseMarkers.Contains(tokens[i]) && objectTokens.Count > 0 && i + 1 < tags.Count)
            {
                var nextTag = tags[i + 1];
                if (nextTag is PosTag.Pronoun or PosTag.Verb)
                    break;
            }

            objectTokens.Add(tokens[i]);
        }

        while (objectTokens.Count > 0 && ObjectStopWords.Contains(objectTokens[0]))
            objectTokens.RemoveAt(0);

        var result = string.Join(" ", objectTokens);

        if (result.EndsWith(" and", StringComparison.Ordinal))
            result = result[..^4].TrimEnd();
        if (result.EndsWith(" or", StringComparison.Ordinal))
            result = result[..^3].TrimEnd();

        return result;
    }
}

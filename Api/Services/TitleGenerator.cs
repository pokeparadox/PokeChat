using System.Text.RegularExpressions;
using PokeChat.Api.Models;

namespace PokeChat.Api.Services;

public partial class TitleGenerator
{
    private static readonly (string[] Keywords, string Category)[] CategoryMatchers =
    [
        (["nullreference", "exception", "crash", "stack trace", "error", "bug", "fix", "broken", "fault", "issue", "failing", "defect"], "debugging"),
        (["plan", "decide", "roadmap", "strategy", "next step", "architecture", "design", "approach"], "planning"),
        (["implement", "write code", "create a", "build a", "add feature", "refactor", "optimize", "migrate"], "feature"),
        (["setup", "install", "configure", "deploy", "upgrade"], "setup"),
        (["unit test", "integration test", "e2e", "coverage", "testing"], "testing"),
        (["review", "feedback", "look at", "check my", "evaluate", "assess"], "code_review"),
        (["brainstorm", "idea", "suggestion", "what if", "imagine", "creative", "innovative"], "brainstorm"),
        (["what is", "what are", "what does", "what do", "what was", "what were", "how do", "how does", "how is", "how can", "how would", "why is", "why do", "why does", "why would", "where is", "where are", "when is", "when do", "when does", "who is", "who are", "explain", "meaning", "define"], "question"),
    ];

    private static readonly HashSet<string> WholeWordKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "implement", "plan", "review", "fix", "bug", "test", "setup", "idea", "configure",
    };

    public string GenerateTitle(List<ChatMessage> messages)
    {
        var lastUserMsg = messages
            .Where(m => string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase))
            .LastOrDefault();

        var userContent = lastUserMsg?.Content ?? "";
        if (string.IsNullOrWhiteSpace(userContent))
            return "New Conversation";

        var category = Classify(userContent);
        var subject = ExtractSubject(userContent);

        return category switch
        {
            "debugging" => FormatTitle(subject, "Debugging"),
            "planning" => FormatTitle(subject, "Planning"),
            "question" => FormatTitle(subject, "Question"),
            "code_review" => FormatTitle(subject, "Code Review"),
            "brainstorm" => FormatTitle(subject, "Brainstorm"),
            "feature" => FormatTitle(subject, "Feature"),
            "setup" => FormatTitle(subject, "Setup"),
            "testing" => FormatTitle(subject, "Testing"),
            _ => FormatTitle(subject, "Chat"),
        };
    }

    private static string Classify(string input)
    {
        var lower = input.ToLowerInvariant();
        foreach (var (keywords, category) in CategoryMatchers)
        {
            foreach (var keyword in keywords)
            {
                if (WholeWordKeywords.Contains(keyword))
                {
                    if (ContainsWholeWord(lower, keyword))
                        return category;
                }
                else if (lower.Contains(keyword))
                {
                    return category;
                }
            }
        }
        return "general_chat";
    }

    private static bool ContainsWholeWord(string text, string word)
    {
        var idx = 0;
        while ((idx = text.IndexOf(word, idx, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            var beforeOk = idx == 0 || !char.IsLetterOrDigit(text[idx - 1]);
            var afterEnd = idx + word.Length;
            var afterOk = afterEnd >= text.Length || !char.IsLetterOrDigit(text[afterEnd]);
            if (beforeOk && afterOk)
                return true;
            idx += word.Length;
        }
        return false;
    }

    private static string ExtractSubject(string input)
    {
        var match = SubjectNounPattern().Match(input);
        if (match.Success)
        {
            var subject = match.Groups[1].Value;
            if (subject.Length >= 2 && subject.Length <= 40 && !subject.All(char.IsLower))
            {
                var trimmed = StripLeadingArticles(subject);
                return Capitalise(trimmed);
            }
        }

        match = SubjectMyPattern().Match(input);
        if (match.Success)
        {
            var subject = match.Groups[1].Value;
            if (subject.Length >= 2 && subject.Length <= 40)
                return Capitalise(subject);
        }

        var words = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var significant = words
            .Select(w => w.Trim(',', '.', '!', '?', ';', ':'))
            .Where(w => w.Length >= 4 && !StopWords.Contains(w.ToLowerInvariant()))
            .ToList();

        if (significant.Count > 0)
        {
            var best = significant
                .OrderByDescending(w => w.Length)
                .ThenByDescending(w => Array.IndexOf(words, w))
                .First();
            return Capitalise(best);
        }

        if (words.Length > 0)
        {
            var last = words[^1].Trim(',', '.', '!', '?', ';', ':');
            if (last.Length >= 2)
                return Capitalise(last);
        }

        return "Conversation";
    }

    private static string StripLeadingArticles(string subject)
    {
        foreach (var article in new[] { "the ", "a ", "an " })
        {
            if (subject.StartsWith(article, StringComparison.OrdinalIgnoreCase))
                return subject[article.Length..];
        }
        return subject;
    }

    private static string FormatTitle(string subject, string category)
    {
        if (string.IsNullOrEmpty(subject) || subject == "Conversation")
            return category;

        return $"{subject} {category}";
    }

    private static string Capitalise(string word)
    {
        if (string.IsNullOrEmpty(word)) return word;
        return char.ToUpperInvariant(word[0]) + word[1..];
    }

    [GeneratedRegex(@"\b(?:in|about|with|for|on)\s+([A-Z]\w+(?:\s+[A-Z]\w+)*)", RegexOptions.IgnoreCase)]
    private static partial Regex SubjectNounPattern();

    [GeneratedRegex(@"\b(?:my|the|a|an)\s+([a-z]+(?:\s+[a-z]+){0,3})\b", RegexOptions.IgnoreCase)]
    private static partial Regex SubjectMyPattern();

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "the", "is", "are", "was", "were", "be", "been", "being",
        "have", "has", "had", "do", "does", "did", "will", "would", "could",
        "should", "may", "might", "shall", "can", "must", "need", "dare",
        "i", "you", "he", "she", "it", "we", "they", "me", "him", "her",
        "us", "them", "my", "your", "his", "its", "our", "their", "mine",
        "yours", "hers", "ours", "theirs", "this", "that", "these", "those",
        "and", "or", "but", "if", "because", "so", "than", "as", "for",
        "in", "on", "at", "by", "with", "about", "against", "between",
        "into", "through", "during", "before", "after", "above", "below",
        "to", "from", "up", "down", "of", "off", "over", "under", "out",
        "what", "which", "who", "whom", "when", "where", "why", "how",
        "all", "each", "every", "both", "few", "more", "most", "some",
        "any", "no", "not", "only", "own", "same", "so", "than", "too",
        "very", "just", "about", "also", "here", "there", "then", "well",
        "get", "got", "make", "made", "want", "like", "know", "think",
        "tell", "say", "said", "see", "let", "go", "come", "take", "use",
    };
}

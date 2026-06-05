using PokeChat.Core;

namespace PokeChat.Knowledge;

public class TopicEntry
{
    public string Subject { get; init; } = string.Empty;
    public string Verb { get; init; } = string.Empty;
    public string Object { get; init; } = string.Empty;
    public string? Category { get; init; }
    public PredicateType PredicateType { get; init; }
    public int TurnNumber { get; set; }
    public int MentionCount { get; set; }
}

public class ContextTracker
{
    private readonly Dictionary<string, string?> _context = new();
    private string? _lastSubject;
    private string? _lastObject;
    private readonly List<TopicEntry> _topicStack = new();
    private int _turnCounter;

    public string? LastSubject => _lastSubject;
    public string? LastObject => _lastObject;
    public IReadOnlyList<TopicEntry> TopicStack => _topicStack.AsReadOnly();

    public void SetContext(string key, string? value)
    {
        if (value == null)
            _context.Remove(key);
        else
            _context[key] = value;
    }

    public string? GetContext(string key)
    {
        return _context.TryGetValue(key, out var value) ? value : null;
    }

    public void UpdateLastSubject(string subject)
    {
        _lastSubject = subject;
    }

    public void UpdateLastObject(string obj)
    {
        _lastObject = obj;
    }

    public string ResolvePronoun(string pronoun)
    {
        return pronoun.ToLowerInvariant() switch
        {
            "it" or "this" or "that" => _lastObject ?? _lastSubject ?? string.Empty,
            "he" or "his" => _lastSubject ?? string.Empty,
            "she" => _lastSubject ?? string.Empty,
            "they" or "their" => _lastSubject ?? string.Empty,
            "him" or "her" or "them" => _lastObject ?? _lastSubject ?? string.Empty,
            _ => pronoun
        };
    }

    public void PushTopic(string subject, string verb, string obj, string? category, PredicateType predicateType)
    {
        _turnCounter++;

        var existing = _topicStack.FirstOrDefault(t =>
            string.Equals(t.Subject, subject, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(t.Object, obj, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(t.Verb, verb, StringComparison.OrdinalIgnoreCase));

        if (existing != null)
        {
            existing.MentionCount++;
            existing.TurnNumber = _turnCounter;
            return;
        }

        _topicStack.Add(new TopicEntry
        {
            Subject = subject,
            Verb = verb,
            Object = obj,
            Category = category,
            PredicateType = predicateType,
            TurnNumber = _turnCounter,
            MentionCount = 1
        });

        if (_topicStack.Count > 5)
            _topicStack.RemoveAt(0);
    }

    public List<TopicEntry> GetRecentTopics(int count)
    {
        return _topicStack
            .OrderByDescending(t => t.TurnNumber)
            .Take(count)
            .ToList();
    }

    public TopicEntry? GetTopicBySubject(string subject)
    {
        return _topicStack
            .OrderByDescending(t => t.TurnNumber)
            .FirstOrDefault(t =>
                string.Equals(t.Subject, subject, StringComparison.OrdinalIgnoreCase));
    }

    public void Clear()
    {
        _context.Clear();
        _lastSubject = null;
        _lastObject = null;
        _topicStack.Clear();
        _turnCounter = 0;
    }
}

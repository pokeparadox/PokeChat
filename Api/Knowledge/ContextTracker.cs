using System.Text.Json;
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
            "they" or "their" => _lastObject ?? _lastSubject ?? string.Empty,
            "him" or "her" or "them" => _lastObject ?? _lastSubject ?? string.Empty,
            _ => pronoun
        };
    }

    public string? ResolveFilePronoun(string input)
    {
        var currentFile = GetContext(ContextKeys.CurrentFile);
        var recentFilesRaw = GetContext(ContextKeys.RecentFiles);

        var lower = input.ToLowerInvariant();

        if ((lower.Contains("that file") || lower.Contains("this file") || lower.Contains("the file")) && !string.IsNullOrEmpty(currentFile))
            return currentFile;

        if (lower.Contains("that test") || lower.Contains("this test"))
        {
            if (!string.IsNullOrEmpty(currentFile) && IsTestFile(currentFile))
                return currentFile;

            if (!string.IsNullOrEmpty(recentFilesRaw))
            {
                var recentFiles = JsonSerializer.Deserialize<List<string>>(recentFilesRaw) ?? new();
                return recentFiles.FirstOrDefault(IsTestFile);
            }
        }

        if ((lower.Contains("that error") || lower.Contains("this error")))
        {
            var lastBuildOutput = GetContext(ContextKeys.LastBuildOutput);
            if (string.IsNullOrEmpty(lastBuildOutput))
                return null;
            return "last build output";
        }

        if ((lower.Contains("that function") || lower.Contains("that method") || lower.Contains("this function") || lower.Contains("this method") ||
             lower.Contains("that class") || lower.Contains("this class")) && !string.IsNullOrEmpty(currentFile))
            return currentFile;

        // "there" as in "improve what's in there" — resolve to current file
        if (lower.Contains(" there") && !string.IsNullOrEmpty(currentFile))
            return currentFile;

        // Short "it" references — resolve to current file if input is brief and no other nouns compete
        if (lower.Contains(" it ") || lower.EndsWith(" it") || lower.StartsWith("it ") || lower == "it")
        {
            if (!string.IsNullOrEmpty(currentFile))
            {
                var wordCount = input.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
                if (wordCount <= 6)
                    return currentFile;
            }
        }

        return null;
    }

    private static bool IsTestFile(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        return name.EndsWith("Tests", StringComparison.OrdinalIgnoreCase) ||
               name.EndsWith("Test", StringComparison.OrdinalIgnoreCase);
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

    public string SerializeState()
    {
        var state = new
        {
            context = _context,
            lastSubject = _lastSubject,
            lastObject = _lastObject,
            topicStack = _topicStack,
            turnCounter = _turnCounter
        };
        return JsonSerializer.Serialize(state);
    }

    public void DeserializeState(string json)
    {
        if (string.IsNullOrEmpty(json))
            return;

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        _context.Clear();
        _lastSubject = null;
        _lastObject = null;
        _topicStack.Clear();
        _turnCounter = 0;

        if (root.TryGetProperty("context", out var contextEl) && contextEl.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in contextEl.EnumerateObject())
            {
                _context[prop.Name] = prop.Value.ValueKind == JsonValueKind.Null ? null : prop.Value.GetString();
            }
        }

        if (root.TryGetProperty("lastSubject", out var lsEl) && lsEl.ValueKind != JsonValueKind.Null)
            _lastSubject = lsEl.GetString();

        if (root.TryGetProperty("lastObject", out var loEl) && loEl.ValueKind != JsonValueKind.Null)
            _lastObject = loEl.GetString();

        if (root.TryGetProperty("turnCounter", out var tcEl) && tcEl.ValueKind == JsonValueKind.Number)
            _turnCounter = tcEl.GetInt32();

        if (root.TryGetProperty("topicStack", out var tsEl) && tsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in tsEl.EnumerateArray())
            {
                PredicateType pt;
                if (item.TryGetProperty("PredicateType", out var ptEl))
                {
                    if (ptEl.ValueKind == JsonValueKind.Number)
                        pt = (PredicateType)ptEl.GetInt32();
                    else if (ptEl.ValueKind == JsonValueKind.String && Enum.TryParse<PredicateType>(ptEl.GetString(), out var parsed))
                        pt = parsed;
                    else
                        pt = PredicateType.General;
                }
                else
                {
                    pt = PredicateType.General;
                }

                var topic = new TopicEntry
                {
                    Subject = GetString(item, "Subject") ?? string.Empty,
                    Verb = GetString(item, "Verb") ?? string.Empty,
                    Object = GetString(item, "Object") ?? string.Empty,
                    Category = GetString(item, "Category"),
                    PredicateType = pt,
                    TurnNumber = GetInt(item, "TurnNumber"),
                    MentionCount = GetInt(item, "MentionCount")
                };
                _topicStack.Add(topic);
            }
        }
    }

    private static string? GetString(JsonElement el, string name)
    {
        if (el.TryGetProperty(name, out var p) && p.ValueKind != JsonValueKind.Null)
            return p.GetString();
        return null;
    }

    private static int GetInt(JsonElement el, string name)
    {
        if (el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Number)
            return p.GetInt32();
        return 0;
    }
}

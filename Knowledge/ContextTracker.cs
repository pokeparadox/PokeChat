using System.Collections.Generic;

namespace PokeChat.Knowledge;

public class ContextTracker
{
    private readonly Dictionary<string, string> _context = new();
    private string? _lastSubject;
    private string? _lastObject;

    public string? LastSubject => _lastSubject;
    public string? LastObject => _lastObject;

    public void SetContext(string key, string value)
    {
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
            "he" or "him" or "his" => _lastSubject ?? string.Empty,
            "she" or "her" => _lastSubject ?? string.Empty,
            "they" or "them" or "their" => _lastSubject ?? string.Empty,
            _ => pronoun
        };
    }

    public void Clear()
    {
        _context.Clear();
        _lastSubject = null;
        _lastObject = null;
    }
}

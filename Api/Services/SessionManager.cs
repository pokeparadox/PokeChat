using System.Collections.Concurrent;
using PokeChat.Core;

namespace PokeChat.Api.Services;

public sealed class SessionManager : IDisposable
{
    private readonly ConcurrentDictionary<string, ChatEngine> _sessions = new(StringComparer.OrdinalIgnoreCase);
    private readonly ChatEngineFactory _factory;

    public SessionManager(ChatEngineFactory factory)
    {
        _factory = factory;
    }

    public ChatEngine GetOrCreate(string sessionId)
    {
        return _sessions.GetOrAdd(sessionId, _ => _factory.Create());
    }

    public void Remove(string sessionId)
    {
        if (_sessions.TryRemove(sessionId, out var engine))
            engine.Dispose();
    }

    public void Dispose()
    {
        foreach (var kvp in _sessions)
            kvp.Value.Dispose();
        _sessions.Clear();
    }
}

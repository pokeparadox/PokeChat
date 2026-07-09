using System.Collections.Concurrent;
using PokeChat.Core;
using PokeChat.Data;
using PokeChat.Data.Entities;

namespace PokeChat.Api.Services;

public sealed class SessionManager : IDisposable
{
    private sealed class CacheEntry
    {
        public ChatEngine Engine { get; }
        public ConversationSession DbSession { get; set; }
        public DateTime LastAccessed { get; set; }

        public CacheEntry(ChatEngine engine, ConversationSession dbSession)
        {
            Engine = engine;
            DbSession = dbSession;
            LastAccessed = DateTime.UtcNow;
        }
    }

    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ChatEngineFactory _factory;
    private readonly PokeChatDbContext _dbContext;
    private readonly int _maxSessions;
    private readonly TimeSpan _sessionTtl;

    public SessionManager(ChatEngineFactory factory, int maxSessions = 50, int sessionTtlMinutes = 60)
        : this(factory, new PokeChatDbContext(), maxSessions, sessionTtlMinutes)
    {
    }

    public SessionManager(ChatEngineFactory factory, PokeChatDbContext dbContext, int maxSessions = 50, int sessionTtlMinutes = 60)
    {
        _factory = factory;
        _dbContext = dbContext;
        _maxSessions = maxSessions;
        _sessionTtl = TimeSpan.FromMinutes(sessionTtlMinutes);
    }

    public ChatEngine GetOrCreate(string sessionId, string? userName = null)
    {
        EvictExpired();

        var entry = _cache.GetOrAdd(sessionId, id =>
        {
            var dbSession = _dbContext.ConversationSessions.FirstOrDefault(s => s.SessionGuid == id);

            if (dbSession == null)
            {
                dbSession = new ConversationSession
                {
                    SessionGuid = id,
                    StartedAt = DateTime.UtcNow.ToString("o"),
                    LastActiveAt = DateTime.UtcNow.ToString("o"),
                    TurnCount = 0
                };
                _dbContext.ConversationSessions.Add(dbSession);
                _dbContext.SaveChanges();
            }

            var engine = _factory.Create(id);

            if (dbSession.UserId.HasValue)
            {
                var user = _dbContext.Users.Find(dbSession.UserId.Value);
                if (user != null)
                {
                    engine.CurrentUserId = user.Id;
                }
            }

            if (engine.CurrentUserId == null)
            {
                engine.EstablishDefaultUser(userName ?? "Guest");
            }

            return new CacheEntry(engine, dbSession);
        });

        entry.LastAccessed = DateTime.UtcNow;

        if (_cache.Count > _maxSessions)
            EvictLru();

        return entry.Engine;
    }

    public ConversationSession? GetSessionMetadata(string sessionId)
    {
        var dbSession = _dbContext.ConversationSessions.FirstOrDefault(s => s.SessionGuid == sessionId);
        if (dbSession == null) return null;

        if (_cache.TryGetValue(sessionId, out var entry))
            entry.LastAccessed = DateTime.UtcNow;

        return dbSession;
    }

    public List<ConversationSession> ListActiveSessions()
    {
        return _dbContext.ConversationSessions
            .Where(s => s.EndedAt == null)
            .OrderByDescending(s => s.LastActiveAt ?? s.StartedAt)
            .ToList();
    }

    public void EndSession(string sessionId)
    {
        if (_cache.TryRemove(sessionId, out var entry))
        {
            entry.Engine.RecordSessionMetrics();
            entry.Engine.TryRetrainClassifier();
            entry.Engine.Save();
            entry.Engine.Dispose();
        }

        var dbSession = _dbContext.ConversationSessions.FirstOrDefault(s => s.SessionGuid == sessionId);
        if (dbSession != null)
        {
            dbSession.EndedAt = DateTime.UtcNow.ToString("o");
            dbSession.LastActiveAt = DateTime.UtcNow.ToString("o");
            _dbContext.SaveChanges();
        }
    }

    public void UpdateActivity(string sessionId)
    {
        if (_cache.TryGetValue(sessionId, out var entry))
        {
            entry.LastAccessed = DateTime.UtcNow;
            entry.DbSession.LastActiveAt = DateTime.UtcNow.ToString("o");
            entry.DbSession.TurnCount++;
            _dbContext.SaveChanges();
        }
        else
        {
            var dbSession = _dbContext.ConversationSessions.FirstOrDefault(s => s.SessionGuid == sessionId);
            if (dbSession != null)
            {
                dbSession.LastActiveAt = DateTime.UtcNow.ToString("o");
                dbSession.TurnCount++;
                _dbContext.SaveChanges();
            }
        }
    }

    public bool SessionExists(string sessionId)
    {
        return _cache.ContainsKey(sessionId) ||
               _dbContext.ConversationSessions.Any(s => s.SessionGuid == sessionId && s.EndedAt == null);
    }

    private void EvictExpired()
    {
        var cutoff = DateTime.UtcNow - _sessionTtl;
        foreach (var kvp in _cache)
        {
            if (kvp.Value.LastAccessed < cutoff)
            {
                if (_cache.TryRemove(kvp.Key, out var entry))
                {
                    entry.Engine.RecordSessionMetrics();
                    entry.Engine.Save();
                    entry.Engine.Dispose();

                    entry.DbSession.EndedAt = DateTime.UtcNow.ToString("o");
                    entry.DbSession.LastActiveAt = DateTime.UtcNow.ToString("o");
                }
            }
        }

        if (_dbContext.ChangeTracker.HasChanges())
            _dbContext.SaveChanges();
    }

    private void EvictLru()
    {
        var toEvict = _cache
            .OrderBy(kvp => kvp.Value.LastAccessed)
            .Take(_cache.Count - _maxSessions)
            .ToList();

        foreach (var kvp in toEvict)
        {
            if (_cache.TryRemove(kvp.Key, out var entry))
            {
                entry.Engine.RecordSessionMetrics();
                entry.Engine.Save();
                entry.Engine.Dispose();

                entry.DbSession.EndedAt = DateTime.UtcNow.ToString("o");
                entry.DbSession.LastActiveAt = DateTime.UtcNow.ToString("o");
            }
        }

        if (_dbContext.ChangeTracker.HasChanges())
            _dbContext.SaveChanges();
    }

    public void Dispose()
    {
        foreach (var kvp in _cache)
        {
            kvp.Value.Engine.Dispose();
        }
        _cache.Clear();
        _dbContext.Dispose();
    }
}

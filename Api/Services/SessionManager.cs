using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using PokeChat.Api.Models;
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

    public ChatEngine GetOrCreate(string sessionId, string? userName = null, List<ChatMessage>? messages = null, string? persona = null)
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

            var resolvedPersona = persona ?? dbSession.Persona ?? "chat";
            var engine = _factory.Create(id, resolvedPersona);

            if (dbSession.UserId.HasValue)
            {
                var user = _dbContext.Users.Find(dbSession.UserId.Value);
                if (user != null)
                {
                    engine.RestoreUser(user.Id, user.Name);
                }
            }

            if (engine.CurrentUserId == null && messages != null)
            {
                TryRestoreUserFromMessages(engine, messages);
            }

            if (engine.CurrentUserId == null)
            {
                engine.EstablishDefaultUser(userName ?? "Guest");
            }

            if (dbSession.Persona == null)
            {
                dbSession.Persona = resolvedPersona;
                dbSession.BotName = engine.BotName;
            }

            return new CacheEntry(engine, dbSession);
        });

        entry.LastAccessed = DateTime.UtcNow;

        SyncUserId(entry.Engine, entry.DbSession);

        if (_cache.Count > _maxSessions)
            EvictLru(entry.Engine.CurrentUserId);

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
            if (entry != null)
                SyncUserId(entry.Engine, dbSession);
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
            SyncUserId(entry.Engine, entry.DbSession);
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
                    SyncUserId(entry.Engine, entry.DbSession);
                }
            }
        }

        if (_dbContext.ChangeTracker.HasChanges())
            _dbContext.SaveChanges();
    }

    private void EvictLru(int? currentUserId = null)
    {
        var overage = _cache.Count - _maxSessions;
        if (overage <= 0) return;

        // Prefer evicting sessions belonging to the same user (likely the caller)
        List<KeyValuePair<string, CacheEntry>> toEvict;

        if (currentUserId.HasValue)
        {
            var sameUser = _cache
                .Where(kvp => kvp.Value.Engine.CurrentUserId == currentUserId.Value)
                .OrderBy(kvp => kvp.Value.LastAccessed)
                .Take(overage)
                .ToList();

            var otherUsers = _cache
                .Where(kvp => kvp.Value.Engine.CurrentUserId != currentUserId.Value)
                .OrderBy(kvp => kvp.Value.LastAccessed)
                .Take(overage - sameUser.Count)
                .ToList();

            toEvict = [.. sameUser, .. otherUsers];
        }
        else
        {
            toEvict = _cache
                .OrderBy(kvp => kvp.Value.LastAccessed)
                .Take(overage)
                .ToList();
        }

        foreach (var kvp in toEvict)
        {
            if (_cache.TryRemove(kvp.Key, out var entry))
            {
                entry.Engine.RecordSessionMetrics();
                entry.Engine.Save();
                entry.Engine.Dispose();

                entry.DbSession.EndedAt = DateTime.UtcNow.ToString("o");
                entry.DbSession.LastActiveAt = DateTime.UtcNow.ToString("o");
                SyncUserId(entry.Engine, entry.DbSession);
            }
        }

        if (_dbContext.ChangeTracker.HasChanges())
            _dbContext.SaveChanges();
    }

    private static readonly Regex NameFromUserPattern = new(
        @"\b(?:my name is|i'm|i am|call me|i'm called|i'm named)\s+([A-Z][a-z]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex NameFromBotPattern = new(
        @"(?:welcome|nice to meet you|hello|hey|hi there),?\s+([A-Z][a-z]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public bool TryRestoreUserFromMessages(ChatEngine engine, List<ChatMessage> messages)
    {
        if (engine.CurrentUserId != null && engine.CurrentUserName != "Guest")
            return false;

        string? foundName = null;

        foreach (var msg in messages)
        {
            if (string.IsNullOrEmpty(msg.Content)) continue;

            if (string.Equals(msg.Role, "user", StringComparison.OrdinalIgnoreCase))
            {
                var match = NameFromUserPattern.Match(msg.Content);
                if (match.Success)
                {
                    foundName = match.Groups[1].Value;
                    break;
                }
            }
            else if (string.Equals(msg.Role, "assistant", StringComparison.OrdinalIgnoreCase))
            {
                var match = NameFromBotPattern.Match(msg.Content);
                if (match.Success)
                {
                    foundName = match.Groups[1].Value;
                    break;
                }
            }
        }

        if (string.IsNullOrEmpty(foundName) || foundName.Length < 2 || foundName.Length > 30)
            return false;

        var user = _dbContext.Users.FirstOrDefault(u =>
            u.Name.ToLower() == foundName.ToLower());
        if (user == null)
            return false;

        engine.RestoreUser(user.Id, user.Name);
        return true;
    }

    private void SyncUserId(ChatEngine engine, ConversationSession dbSession)
    {
        if (!engine.CurrentUserId.HasValue || dbSession.UserId.HasValue) return;

        var userId = engine.CurrentUserId.Value;
        var user = _dbContext.Users.Find(userId);
        if (user == null)
        {
            user = new Data.Entities.User
            {
                Id = userId,
                Name = engine.CurrentUserName,
                FirstSeen = DateTime.UtcNow.ToString("o"),
                LastSeen = DateTime.UtcNow.ToString("o")
            };
            _dbContext.Users.Add(user);
        }
        dbSession.UserId = userId;
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

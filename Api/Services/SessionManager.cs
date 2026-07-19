using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
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
        public DateTime LastAccessed { get; set; }

        public CacheEntry(ChatEngine engine)
        {
            Engine = engine;
            LastAccessed = DateTime.UtcNow;
        }
    }

    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, int> _upstreamCallsPerSession = new(StringComparer.OrdinalIgnoreCase);
    private readonly ChatEngineFactory _factory;
    private readonly IDbContextFactory<PokeChatDbContext> _factoryDb;
    private readonly int _maxSessions;
    private readonly TimeSpan _sessionTtl;
    private readonly SessionQuotaOptions _quotas;
    private readonly bool _openCodeDetected;

    public SessionManager(ChatEngineFactory factory, IDbContextFactory<PokeChatDbContext> factoryDb, SessionQuotaOptions quotas)
    {
        _factory = factory;
        _factoryDb = factoryDb;
        _quotas = quotas;
        _maxSessions = quotas.MaxSessions;
        _sessionTtl = TimeSpan.FromMinutes(quotas.SessionTtlMinutes);
        _openCodeDetected = DetectOpenCodeEnvironment();
    }

    public bool IsOpenCodeEnvironment => _openCodeDetected;

    private static bool DetectOpenCodeEnvironment()
    {
        try
        {
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OPENCODE_API_KEY")))
                return true;
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OPENCODE_SESSION_ID")))
                return true;
            if (string.Equals(Environment.GetEnvironmentVariable("OPENCODE_ENV"), "opencode", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        catch
        {
        }
        return false;
    }

    public ChatEngine GetOrCreate(string sessionId, string? userName = null, List<ChatMessage>? messages = null, string? persona = null)
    {
        EvictExpired();

        var entry = _cache.GetOrAdd(sessionId, id =>
        {
            using var ctx = _factoryDb.CreateDbContext();

            var dbSession = ctx.ConversationSessions.FirstOrDefault(s => s.SessionGuid == id);

            if (dbSession == null)
            {
                dbSession = new ConversationSession
                {
                    SessionGuid = id,
                    StartedAt = DateTime.UtcNow.ToString("o"),
                    LastActiveAt = DateTime.UtcNow.ToString("o"),
                    TurnCount = 0
                };
                ctx.ConversationSessions.Add(dbSession);
                ctx.SaveChanges();
            }

            var resolvedPersona = persona ?? dbSession.Persona ?? (_openCodeDetected ? "coding" : "chat");
            if (_openCodeDetected && resolvedPersona == "chat" && dbSession.Persona == null)
                resolvedPersona = "coding";
            var engine = _factory.Create(id, resolvedPersona);

            if (dbSession.UserId.HasValue)
            {
                var user = ctx.Users.Find(dbSession.UserId.Value);
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
                ctx.SaveChanges();
            }

            return new CacheEntry(engine);
        });

        entry.LastAccessed = DateTime.UtcNow;

        SyncUserId(entry.Engine, sessionId);

        if (_cache.Count > _maxSessions)
            EvictLru(entry.Engine.CurrentUserId);

        return entry.Engine;
    }

    public ConversationSession? GetSessionMetadata(string sessionId)
    {
        using var ctx = _factoryDb.CreateDbContext();
        var dbSession = ctx.ConversationSessions.FirstOrDefault(s => s.SessionGuid == sessionId);
        if (dbSession == null) return null;

        if (_cache.TryGetValue(sessionId, out var entry))
            entry.LastAccessed = DateTime.UtcNow;

        return dbSession;
    }

    public List<ConversationSession> ListActiveSessions()
    {
        using var ctx = _factoryDb.CreateDbContext();
        return ctx.ConversationSessions
            .Where(s => s.EndedAt == null)
            .OrderByDescending(s => s.LastActiveAt ?? s.StartedAt)
            .ToList();
    }

    public void EndSession(string sessionId)
    {
        _upstreamCallsPerSession.TryRemove(sessionId, out _);

        if (_cache.TryRemove(sessionId, out var entry))
        {
            using (var ctx = _factoryDb.CreateDbContext())
            {
                var dbSession = ctx.ConversationSessions.FirstOrDefault(s => s.SessionGuid == sessionId);
                if (dbSession != null)
                {
                    dbSession.EndedAt = DateTime.UtcNow.ToString("o");
                    dbSession.LastActiveAt = DateTime.UtcNow.ToString("o");
                    SyncUserId(entry.Engine, sessionId, ctx);
                    ctx.SaveChanges();
                }
            }

            entry.Engine.RecordSessionMetrics();
            entry.Engine.TryRetrainClassifier();
            entry.Engine.Save();
            entry.Engine.Dispose();
        }
    }

    public void UpdateActivity(string sessionId)
    {
        if (_cache.TryGetValue(sessionId, out var entry))
        {
            entry.LastAccessed = DateTime.UtcNow;
        }

        try
        {
            using var ctx = _factoryDb.CreateDbContext();
            var dbSession = ctx.ConversationSessions.FirstOrDefault(s => s.SessionGuid == sessionId);
            if (dbSession != null)
            {
                dbSession.LastActiveAt = DateTime.UtcNow.ToString("o");
                dbSession.TurnCount++;
                if (entry != null)
                    SyncUserId(entry.Engine, sessionId, ctx);
                ctx.SaveChanges();
            }
        }
        catch (Microsoft.Data.Sqlite.SqliteException)
        {
            // DB tables may not exist yet during startup — ignore
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException)
        {
            // DB may be in recovery or read-only state — ignore
        }
    }

    public int CountSessionsForUser(int userId)
    {
        return _cache.Count(kvp => kvp.Value.Engine.CurrentUserId == userId);
    }

    public bool IsTurnQuotaExceeded(string sessionId)
    {
        using var ctx = _factoryDb.CreateDbContext();
        var dbSession = ctx.ConversationSessions.FirstOrDefault(s => s.SessionGuid == sessionId);
        if (dbSession == null) return false;
        return dbSession.TurnCount >= _quotas.MaxTurnsPerSession;
    }

    public bool TryConsumeUpstreamCall(string sessionId)
    {
        var count = _upstreamCallsPerSession.AddOrUpdate(sessionId, 1, (_, c) => c + 1);
        return count <= _quotas.MaxUpstreamCallsPerSession;
    }

    public int GetUpstreamCalls(string sessionId)
    {
        return _upstreamCallsPerSession.GetValueOrDefault(sessionId);
    }

    public bool SessionExists(string sessionId)
    {
        if (_cache.ContainsKey(sessionId)) return true;
        using var ctx = _factoryDb.CreateDbContext();
        return ctx.ConversationSessions.Any(s => s.SessionGuid == sessionId && s.EndedAt == null);
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
                    using (var ctx = _factoryDb.CreateDbContext())
                    {
                        var dbSession = ctx.ConversationSessions.FirstOrDefault(s => s.SessionGuid == kvp.Key);
                        if (dbSession != null)
                        {
                            dbSession.EndedAt = DateTime.UtcNow.ToString("o");
                            dbSession.LastActiveAt = DateTime.UtcNow.ToString("o");
                            SyncUserId(entry.Engine, kvp.Key, ctx);
                            ctx.SaveChanges();
                        }
                    }

                    entry.Engine.RecordSessionMetrics();
                    entry.Engine.Save();
                    entry.Engine.Dispose();
                }

                _upstreamCallsPerSession.TryRemove(kvp.Key, out _);
            }
        }
    }

    private void EvictLru(int? currentUserId = null)
    {
        var overage = _cache.Count - _maxSessions;
        if (overage <= 0) return;

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
                using (var ctx = _factoryDb.CreateDbContext())
                {
                    var dbSession = ctx.ConversationSessions.FirstOrDefault(s => s.SessionGuid == kvp.Key);
                    if (dbSession != null)
                    {
                        dbSession.EndedAt = DateTime.UtcNow.ToString("o");
                        dbSession.LastActiveAt = DateTime.UtcNow.ToString("o");
                        SyncUserId(entry.Engine, kvp.Key, ctx);
                        ctx.SaveChanges();
                    }
                }

                entry.Engine.RecordSessionMetrics();
                entry.Engine.Save();
                entry.Engine.Dispose();
            }

            _upstreamCallsPerSession.TryRemove(kvp.Key, out _);
        }
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

        using var ctx = _factoryDb.CreateDbContext();
        var user = ctx.Users.FirstOrDefault(u =>
            u.Name.ToLower() == foundName.ToLower());
        if (user == null)
            return false;

        engine.RestoreUser(user.Id, user.Name);
        return true;
    }

    private void SyncUserId(ChatEngine engine, string sessionId, PokeChatDbContext? sharedCtx = null)
    {
        if (!engine.CurrentUserId.HasValue) return;

        var ownsCtx = sharedCtx == null;
        var ctx = sharedCtx ?? _factoryDb.CreateDbContext();
        try
        {
            var dbSession = ctx.ConversationSessions.FirstOrDefault(s => s.SessionGuid == sessionId);
            if (dbSession == null || dbSession.UserId.HasValue) return;

            var userId = engine.CurrentUserId.Value;
            var user = ctx.Users.Find(userId);
            if (user == null)
            {
                user = new Data.Entities.User
                {
                    Id = userId,
                    Name = engine.CurrentUserName,
                    FirstSeen = DateTime.UtcNow.ToString("o"),
                    LastSeen = DateTime.UtcNow.ToString("o")
                };
                ctx.Users.Add(user);
            }
            dbSession.UserId = userId;
            ctx.SaveChanges();
        }
        finally
        {
            if (ownsCtx) ctx.Dispose();
        }
    }

    public void Dispose()
    {
        foreach (var kvp in _cache)
        {
            kvp.Value.Engine.Dispose();
        }
        _cache.Clear();
    }
}

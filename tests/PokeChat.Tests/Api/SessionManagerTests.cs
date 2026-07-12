using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PokeChat.Api.Services;
using PokeChat.Core;
using PokeChat.Data;
using PokeChat.Data.Entities;
using PokeChat.Knowledge;
using PokeChat.NLP;
using PokeChat.Responses;
using PokeChat.Tests.Helpers;
using Shouldly;

namespace PokeChat.Tests.Api;

public class SessionManagerTests
{
    private sealed class TestEngineFactory : ChatEngineFactory
    {
        public override ChatEngine Create(string? sessionId = null, string persona = "chat")
        {
            var engineDb = new FreshDbContext();
            TestDataHelper.SeedBotResponses(engineDb.Context);
            TestDataHelper.SeedPosDictionary(engineDb.Context);
            var store = new KnowledgeStore(engineDb.Context);
            var contextTracker = new ContextTracker();
            var spellChecker = new SpellChecker();
            var posEntries = store.GetPosDictionary();
            var posTagger = new PosTagger(posEntries);
            var spellDict = new HashSet<string>(posEntries.Select(e => e.Word), StringComparer.OrdinalIgnoreCase);
            var misspellings = store.GetMisspellings();
            spellChecker.Initialise(spellDict, misspellings);
            var tokeniser = new Tokeniser();
            var sentenceSplitter = new SentenceSplitter();
            var svoExtractor = new SvoExtractor();
            var nounCategoriser = new NounCategoriser(store);
            var responseEngine = new ResponseEngine(store, contextTracker, spellChecker, posTagger, tokeniser, svoExtractor);
            var engine = new ChatEngine(
                engineDb.Context, store, responseEngine, spellChecker, posTagger, tokeniser,
                sentenceSplitter, svoExtractor, contextTracker, nounCategoriser,
                new List<string> { "my name is", "i am", "i'm", "call me" },
                new HashSet<string> { "quit", "exit" },
                new HashSet<string> { "hi", "hello" },
                sessionId: sessionId ?? Guid.NewGuid().ToString(),
                persona: persona);
            return engine;
        }
    }

    [Fact]
    public void GetOrCreate_creates_new_session()
    {
        using var fresh = new FreshDbContext();
        var factory = new TestEngineFactory();
        using var manager = new SessionManager(factory, fresh.Context, new SessionQuotaOptions { MaxSessions = 10, SessionTtlMinutes = 60 });

        var sessionId = "test-session-1";
        var engine = manager.GetOrCreate(sessionId);

        engine.ShouldNotBeNull();

        var metadata = manager.GetSessionMetadata(sessionId);
        metadata.ShouldNotBeNull();
        metadata.SessionGuid.ShouldBe(sessionId);
        metadata.EndedAt.ShouldBeNull();
    }

    [Fact]
    public void GetOrCreate_returns_same_engine_for_same_session()
    {
        using var fresh = new FreshDbContext();
        var factory = new TestEngineFactory();
        using var manager = new SessionManager(factory, fresh.Context, new SessionQuotaOptions { MaxSessions = 10, SessionTtlMinutes = 60 });

        var sessionId = "test-session-2";
        var engine1 = manager.GetOrCreate(sessionId);
        var engine2 = manager.GetOrCreate(sessionId);

        engine1.ShouldBeSameAs(engine2);
    }

    [Fact]
    public void GetOrCreate_creates_separate_engines_for_different_sessions()
    {
        using var fresh = new FreshDbContext();
        var factory = new TestEngineFactory();
        using var manager = new SessionManager(factory, fresh.Context, new SessionQuotaOptions { MaxSessions = 10, SessionTtlMinutes = 60 });

        var engine1 = manager.GetOrCreate("session-alpha");
        var engine2 = manager.GetOrCreate("session-beta");

        engine1.ShouldNotBeSameAs(engine2);
    }

    [Fact]
    public void SessionExists_returns_true_for_active_session()
    {
        using var fresh = new FreshDbContext();
        var factory = new TestEngineFactory();
        using var manager = new SessionManager(factory, fresh.Context, new SessionQuotaOptions { MaxSessions = 10, SessionTtlMinutes = 60 });

        var sessionId = "test-session-exists";
        manager.GetOrCreate(sessionId);

        manager.SessionExists(sessionId).ShouldBeTrue();
    }

    [Fact]
    public void SessionExists_returns_false_for_unknown_session()
    {
        using var fresh = new FreshDbContext();
        var factory = new TestEngineFactory();
        using var manager = new SessionManager(factory, fresh.Context, new SessionQuotaOptions { MaxSessions = 10, SessionTtlMinutes = 60 });

        manager.SessionExists("nonexistent-session").ShouldBeFalse();
    }

    [Fact]
    public void EndSession_marks_session_as_ended()
    {
        using var fresh = new FreshDbContext();
        var factory = new TestEngineFactory();
        using var manager = new SessionManager(factory, fresh.Context, new SessionQuotaOptions { MaxSessions = 10, SessionTtlMinutes = 60 });

        var sessionId = "test-session-end";
        manager.GetOrCreate(sessionId);

        manager.EndSession(sessionId);

        var metadata = manager.GetSessionMetadata(sessionId);
        metadata.ShouldNotBeNull();
        metadata.EndedAt.ShouldNotBeNull();

        manager.SessionExists(sessionId).ShouldBeFalse();
    }

    [Fact]
    public void EndSession_does_nothing_for_unknown_session()
    {
        using var fresh = new FreshDbContext();
        var factory = new TestEngineFactory();
        using var manager = new SessionManager(factory, fresh.Context, new SessionQuotaOptions { MaxSessions = 10, SessionTtlMinutes = 60 });

        Should.NotThrow(() => manager.EndSession("ghost-session"));
    }

    [Fact]
    public void ListActiveSessions_returns_only_active()
    {
        using var fresh = new FreshDbContext();
        var factory = new TestEngineFactory();
        using var manager = new SessionManager(factory, fresh.Context, new SessionQuotaOptions { MaxSessions = 10, SessionTtlMinutes = 60 });

        manager.GetOrCreate("active-1");
        manager.GetOrCreate("active-2");
        manager.GetOrCreate("to-end");
        manager.EndSession("to-end");

        var active = manager.ListActiveSessions();
        active.Count.ShouldBe(2);
        active.Any(s => s.SessionGuid == "active-1").ShouldBeTrue();
        active.Any(s => s.SessionGuid == "active-2").ShouldBeTrue();
        active.Any(s => s.SessionGuid == "to-end").ShouldBeFalse();
    }

    [Fact]
    public void UpdateActivity_updates_turn_count()
    {
        using var fresh = new FreshDbContext();
        var factory = new TestEngineFactory();
        using var manager = new SessionManager(factory, fresh.Context, new SessionQuotaOptions { MaxSessions = 10, SessionTtlMinutes = 60 });

        var sessionId = "test-update-activity";
        manager.GetOrCreate(sessionId);

        manager.UpdateActivity(sessionId);
        manager.UpdateActivity(sessionId);

        var metadata = manager.GetSessionMetadata(sessionId);
        metadata.ShouldNotBeNull();
        metadata.TurnCount.ShouldBe(2);
    }

    [Fact]
    public void GetOrCreate_restores_user_identity_from_db()
    {
        using var fresh = new FreshDbContext();
        var factory = new TestEngineFactory();
        using var manager = new SessionManager(factory, fresh.Context, new SessionQuotaOptions { MaxSessions = 10, SessionTtlMinutes = 60 });

        var user = new User { Name = "Alice", FirstSeen = DateTime.UtcNow.ToString("o"), LastSeen = DateTime.UtcNow.ToString("o") };
        fresh.Context.Users.Add(user);
        fresh.Context.SaveChanges();

        var session = new ConversationSession
        {
            SessionGuid = "restored-session",
            UserId = user.Id,
            StartedAt = DateTime.UtcNow.ToString("o"),
            LastActiveAt = DateTime.UtcNow.ToString("o")
        };
        fresh.Context.ConversationSessions.Add(session);
        fresh.Context.SaveChanges();

        var engine = manager.GetOrCreate("restored-session");
        engine.ShouldNotBeNull();
        engine.CurrentUserId.ShouldBe(user.Id);
    }

    [Fact]
    public void Multiple_concurrent_sessions_dont_interfere()
    {
        using var fresh = new FreshDbContext();
        var factory = new TestEngineFactory();
        using var manager = new SessionManager(factory, fresh.Context, new SessionQuotaOptions { MaxSessions = 10, SessionTtlMinutes = 60 });

        var engineA = manager.GetOrCreate("session-A");
        var engineB = manager.GetOrCreate("session-B");

        engineA.ProcessInput("My name is Alice");
        engineB.ProcessInput("My name is Bob");

        engineA.CurrentUserName.ShouldBe("Alice");
        engineB.CurrentUserName.ShouldBe("Bob");
    }

    [Fact]
    public void Start_chat_end_session_lifecycle()
    {
        using var fresh = new FreshDbContext();
        var factory = new TestEngineFactory();
        using var manager = new SessionManager(factory, fresh.Context, new SessionQuotaOptions { MaxSessions = 10, SessionTtlMinutes = 60 });

        var sessionId = "lifecycle-test";
        var engine = manager.GetOrCreate(sessionId);
        engine.ShouldNotBeNull();

        var response = engine.ProcessInput("My name is TestUser");
        response.ShouldNotBeNullOrEmpty();
        manager.UpdateActivity(sessionId);

        response = engine.ProcessInput("I like pizza");
        response.ShouldNotBeNullOrEmpty();
        manager.UpdateActivity(sessionId);

        var metadata = manager.GetSessionMetadata(sessionId);
        metadata.ShouldNotBeNull();
        metadata.TurnCount.ShouldBe(2);

        manager.EndSession(sessionId);
        manager.SessionExists(sessionId).ShouldBeFalse();
    }

    [Fact]
    public void Cache_respects_max_sessions()
    {
        using var fresh = new FreshDbContext();
        var factory = new TestEngineFactory();
        using var manager = new SessionManager(factory, fresh.Context, new SessionQuotaOptions { MaxSessions = 3, SessionTtlMinutes = 60 });

        manager.GetOrCreate("session-1");
        manager.GetOrCreate("session-2");
        manager.GetOrCreate("session-3");
        manager.GetOrCreate("session-4");

        var active = manager.ListActiveSessions();
        active.Count.ShouldBe(3);

        manager.SessionExists("session-1").ShouldBeFalse();
    }
}

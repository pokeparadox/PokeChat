using PokeChat.Api.Services;
using PokeChat.Core;
using PokeChat.Data;
using PokeChat.Knowledge;
using PokeChat.NLP;
using PokeChat.Responses;
using PokeChat.Tests.Helpers;
using Shouldly;

namespace PokeChat.Tests.Api;

public class SessionQuotaTests
{
    private sealed class TestEngineFactory : ChatEngineFactory
    {
        public override ChatEngine Create(string? sessionId = null, string persona = "chat")
        {
            var engineDb = new FreshDbContext();
            TestDataHelper.SeedBotResponses(engineDb.Context);
            TestDataHelper.SeedPosDictionary(engineDb.Context);
            var store = new PokeChat.Knowledge.KnowledgeStore(engineDb.Context);
            var contextTracker = new ContextTracker();
            var spellChecker = new PokeChat.NLP.SpellChecker();
            var posEntries = store.GetPosDictionary();
            var posTagger = new PokeChat.NLP.PosTagger(posEntries);
            var spellDict = new HashSet<string>(posEntries.Select(e => e.Word), StringComparer.OrdinalIgnoreCase);
            var misspellings = store.GetMisspellings();
            spellChecker.Initialise(spellDict, misspellings);
            var tokeniser = new PokeChat.NLP.Tokeniser();
            var sentenceSplitter = new PokeChat.NLP.SentenceSplitter();
            var svoExtractor = new PokeChat.NLP.SvoExtractor();
            var nounCategoriser = new PokeChat.Core.NounCategoriser(store);
            var responseEngine = new PokeChat.Responses.ResponseEngine(store, contextTracker, spellChecker, posTagger, tokeniser, svoExtractor);
            return new ChatEngine(
                engineDb.Context, store, responseEngine, spellChecker, posTagger, tokeniser,
                sentenceSplitter, svoExtractor, contextTracker, nounCategoriser,
                new List<string> { "my name is", "i am", "i'm", "call me" },
                new HashSet<string> { "quit", "exit" },
                new HashSet<string> { "hi", "hello" },
                sessionId: sessionId ?? Guid.NewGuid().ToString(),
                persona: persona);
        }
    }

    [Fact]
    public void IsTurnQuotaExceeded_returns_false_when_under_limit()
    {
        using var fresh = new FreshDbContext();
        var factory = new TestEngineFactory();
        var quotas = new SessionQuotaOptions { MaxTurnsPerSession = 10, MaxSessionsPerUser = 10, MaxUpstreamCallsPerSession = 5, SessionTtlMinutes = 60 };
        using var manager = new SessionManager(factory, fresh.Context, quotas);

        manager.GetOrCreate("turn-test");
        manager.IsTurnQuotaExceeded("turn-test").ShouldBeFalse();
    }

    [Fact]
    public void IsTurnQuotaExceeded_returns_true_when_at_limit()
    {
        using var fresh = new FreshDbContext();
        var factory = new TestEngineFactory();
        var quotas = new SessionQuotaOptions { MaxTurnsPerSession = 3, MaxSessionsPerUser = 10, MaxUpstreamCallsPerSession = 5, SessionTtlMinutes = 60 };
        using var manager = new SessionManager(factory, fresh.Context, quotas);

        manager.GetOrCreate("turn-max");
        manager.UpdateActivity("turn-max");
        manager.UpdateActivity("turn-max");
        manager.UpdateActivity("turn-max");

        manager.IsTurnQuotaExceeded("turn-max").ShouldBeTrue();
    }

    [Fact]
    public void IsTurnQuotaExceeded_returns_false_for_unknown_session()
    {
        using var fresh = new FreshDbContext();
        var factory = new TestEngineFactory();
        var quotas = new SessionQuotaOptions();
        using var manager = new SessionManager(factory, fresh.Context, quotas);

        manager.IsTurnQuotaExceeded("nonexistent").ShouldBeFalse();
    }

    [Fact]
    public void TryConsumeUpstreamCall_allows_first_call()
    {
        using var fresh = new FreshDbContext();
        var factory = new TestEngineFactory();
        var quotas = new SessionQuotaOptions { MaxUpstreamCallsPerSession = 2 };
        using var manager = new SessionManager(factory, fresh.Context, quotas);

        manager.TryConsumeUpstreamCall("upstream-test").ShouldBeTrue();
    }

    [Fact]
    public void TryConsumeUpstreamCall_blocks_when_exceeded()
    {
        using var fresh = new FreshDbContext();
        var factory = new TestEngineFactory();
        var quotas = new SessionQuotaOptions { MaxUpstreamCallsPerSession = 2 };
        using var manager = new SessionManager(factory, fresh.Context, quotas);

        manager.TryConsumeUpstreamCall("upstream-exceed").ShouldBeTrue();
        manager.TryConsumeUpstreamCall("upstream-exceed").ShouldBeTrue();
        manager.TryConsumeUpstreamCall("upstream-exceed").ShouldBeFalse();
    }

    [Fact]
    public void GetUpstreamCalls_returns_zero_initially()
    {
        using var fresh = new FreshDbContext();
        var factory = new TestEngineFactory();
        var quotas = new SessionQuotaOptions();
        using var manager = new SessionManager(factory, fresh.Context, quotas);

        manager.GetUpstreamCalls("any-session").ShouldBe(0);
    }

    [Fact]
    public void GetUpstreamCalls_returns_correct_count()
    {
        using var fresh = new FreshDbContext();
        var factory = new TestEngineFactory();
        var quotas = new SessionQuotaOptions();
        using var manager = new SessionManager(factory, fresh.Context, quotas);

        manager.TryConsumeUpstreamCall("count-test");
        manager.TryConsumeUpstreamCall("count-test");
        manager.GetUpstreamCalls("count-test").ShouldBe(2);
    }

    [Fact]
    public void CountSessionsForUser_returns_zero_for_unknown_user()
    {
        using var fresh = new FreshDbContext();
        var factory = new TestEngineFactory();
        var quotas = new SessionQuotaOptions();
        using var manager = new SessionManager(factory, fresh.Context, quotas);

        manager.CountSessionsForUser(999).ShouldBe(0);
    }

    [Fact]
    public void EndSession_cleans_up_upstream_calls()
    {
        using var fresh = new FreshDbContext();
        var factory = new TestEngineFactory();
        var quotas = new SessionQuotaOptions();
        using var manager = new SessionManager(factory, fresh.Context, quotas);

        manager.TryConsumeUpstreamCall("cleanup-test");
        manager.GetUpstreamCalls("cleanup-test").ShouldBe(1);

        manager.EndSession("cleanup-test");
        manager.GetUpstreamCalls("cleanup-test").ShouldBe(0);
    }
}

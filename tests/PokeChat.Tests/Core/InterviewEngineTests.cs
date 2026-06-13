using PokeChat.Core;
using PokeChat.Knowledge;
using PokeChat.LLM;
using PokeChat.Tests.Helpers;
using PokeChat.Tests.LLM;
using Shouldly;
using Xunit;

namespace PokeChat.Tests.Core;

public class InterviewEngineTests : IDisposable
{
    private readonly FreshDbContext _db;

    public InterviewEngineTests()
    {
        _db = new FreshDbContext();
        SeedTestNouns();
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    private void SeedTestNouns()
    {
        var entries = new[]
        {
            new Data.Entities.PosDictionaryEntry { Word = "pizza", WordType = "noun", CreatedAt = "now" },
            new Data.Entities.PosDictionaryEntry { Word = "paris", WordType = "noun", CreatedAt = "now" },
            new Data.Entities.PosDictionaryEntry { Word = "garden", WordType = "noun", CreatedAt = "now" },
        };
        _db.Context.PosDictionary.AddRange(entries);
        _db.Context.SaveChanges();
    }

    [Fact]
    public void Constructor_InitializesTurnsRemaining()
    {
        var engine = CreateEngine("Hello.", maxTurns: 8);
        engine.TurnsRemaining.ShouldBe(8);
    }

    [Fact]
    public void Constructor_FactsAndRulesStartAtZero()
    {
        var engine = CreateEngine("Hello.", maxTurns: 8);
        engine.FactsLearned.ShouldBe(0);
        engine.RulesLearned.ShouldBe(0);
    }

    [Fact]
    public void GenerateQuestion_ReturnsAQuestion()
    {
        var engine = CreateEngine("Hello.", maxTurns: 8);
        var result = engine.GenerateQuestion();
        result.ShouldNotBeNull();
        result.ShouldNotBeEmpty();
    }

    [Fact]
    public void GenerateQuestion_DecrementsTurnsRemaining()
    {
        var engine = CreateEngine("Hi.", maxTurns: 3);
        engine.GenerateQuestion();
        engine.TurnsRemaining.ShouldBe(2);
    }

    [Fact]
    public void GenerateQuestion_ReturnsNull_WhenTurnsExhausted()
    {
        var engine = CreateEngine("Hi.", maxTurns: 1);
        engine.GenerateQuestion();
        var result = engine.GenerateQuestion();
        result.ShouldBeNull();
    }

    [Fact]
    public void GenerateAnswer_ReturnsLLMResponse()
    {
        var engine = CreateEngine("I love it!", maxTurns: 3);
        var question = engine.GenerateQuestion();
        question.ShouldNotBeNull();
        var answer = engine.GenerateAnswer(question);
        answer.ShouldBe("I love it!");
    }

    [Fact]
    public void GenerateAnswer_ReturnsNull_WhenLLMUnavailable()
    {
        var provider = new StubLLMProvider { Response = null };
        var config = new LLMConfig { Enabled = true };
        var orchestrator = new LLMOrchestrator(provider, config);
        var store = new KnowledgeStore(_db.Context);
        var categoriser = new NounCategoriser(store);
        var engine = new InterviewEngine(orchestrator, store, categoriser, maxTurns: 3);
        var question = engine.GenerateQuestion();
        question.ShouldNotBeNull();
        var answer = engine.GenerateAnswer(question);
        answer.ShouldBeNull();
    }

    [Fact]
    public void AddExchange_DoesNotThrow()
    {
        var engine = CreateEngine("Hello.", maxTurns: 8);
        var q = engine.GenerateQuestion();
        q.ShouldNotBeNull();
        engine.AddExchange(q, "user answer", "bot response");
        var nextQ = engine.GenerateQuestion();
        nextQ.ShouldNotBeNull();
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var engine = CreateEngine("Hi.", maxTurns: 3);
        engine.GenerateQuestion();
        engine.AddExchange("q", "a", "r");
        engine.FactsLearned = 5;
        engine.RulesLearned = 3;

        engine.Reset();

        engine.TurnsRemaining.ShouldBe(3);
        engine.FactsLearned.ShouldBe(0);
        engine.RulesLearned.ShouldBe(0);
        engine.GenerateQuestion().ShouldNotBeNull();
    }

    private InterviewEngine CreateEngine(string response, int maxTurns = 8)
    {
        var provider = new StubLLMProvider { Response = response };
        var config = new LLMConfig { Enabled = true };
        var orchestrator = new LLMOrchestrator(provider, config);
        var store = new KnowledgeStore(_db.Context);
        var categoriser = new NounCategoriser(store);
        return new InterviewEngine(orchestrator, store, categoriser, maxTurns);
    }
}

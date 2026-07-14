using PokeChat.Core;
using PokeChat.Knowledge;
using PokeChat.Tests.Shared.Helpers;
using Shouldly;
using Xunit;

namespace PokeChat.Tests.Core;

public class NonLlmInterviewEngineTests : IDisposable
{
    private readonly FreshDbContext _db;

    public NonLlmInterviewEngineTests()
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
            new Data.Entities.PosDictionaryEntry { Word = "alice", WordType = "noun", CreatedAt = "now" },
            new Data.Entities.PosDictionaryEntry { Word = "garden", WordType = "noun", CreatedAt = "now" },
            new Data.Entities.PosDictionaryEntry { Word = "python", WordType = "noun", CreatedAt = "now" },
        };
        _db.Context.PosDictionary.AddRange(entries);
        _db.Context.SaveChanges();
    }

    private NonLlmInterviewEngine CreateEngine(int maxTurns = 8)
    {
        var store = new KnowledgeStore(_db.Context);
        var categoriser = new NounCategoriser(store);
        return new NonLlmInterviewEngine(store, categoriser, maxTurns);
    }

    [Fact]
    public void Constructor_InitializesTurnsRemaining()
    {
        var engine = CreateEngine();
        engine.TurnsRemaining.ShouldBe(8);
    }

    [Fact]
    public void Constructor_FactsAndRulesStartAtZero()
    {
        var engine = CreateEngine();
        engine.FactsLearned.ShouldBe(0);
        engine.RulesLearned.ShouldBe(0);
    }

    [Fact]
    public void GenerateQuestion_ReturnsAQuestion()
    {
        var engine = CreateEngine();
        var result = engine.GenerateQuestion();
        result.ShouldNotBeNull();
        result.ShouldNotBeEmpty();
    }

    [Fact]
    public void GenerateQuestion_ReturnsQuestionAboutKnownNoun()
    {
        var engine = CreateEngine();
        var result = engine.GenerateQuestion();
        result.ShouldNotBeNull();
        var containsKnown = result.IndexOf("pizza", StringComparison.OrdinalIgnoreCase) >= 0
            || result.IndexOf("paris", StringComparison.OrdinalIgnoreCase) >= 0
            || result.IndexOf("alice", StringComparison.OrdinalIgnoreCase) >= 0
            || result.IndexOf("garden", StringComparison.OrdinalIgnoreCase) >= 0
            || result.IndexOf("python", StringComparison.OrdinalIgnoreCase) >= 0;
        containsKnown.ShouldBeTrue();
    }

    [Fact]
    public void GenerateQuestion_DecrementsTurnsRemaining()
    {
        var engine = CreateEngine(maxTurns: 3);
        engine.GenerateQuestion();
        engine.TurnsRemaining.ShouldBe(2);
    }

    [Fact]
    public void GenerateQuestion_ReturnsNull_WhenTurnsExhausted()
    {
        var engine = CreateEngine(maxTurns: 1);
        engine.GenerateQuestion();
        var result = engine.GenerateQuestion();
        result.ShouldBeNull();
    }

    [Fact]
    public void GenerateQuestion_DoesNotRepeatNouns()
    {
        var engine = CreateEngine(maxTurns: 5);
        var questions = new List<string?>();
        for (int i = 0; i < 5; i++)
            questions.Add(engine.GenerateQuestion());

        questions.ShouldAllBe(q => q != null);
        var nouns = questions.Select(q =>
        {
            var parts = (q ?? "").Split(' ');
            return parts.Length > 0 ? parts[^1].TrimEnd('.', '?', '!').ToLowerInvariant() : "";
        }).ToList();
        nouns.Distinct().Count().ShouldBeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public void GenerateAnswer_ReturnsNull()
    {
        var engine = CreateEngine();
        engine.GenerateAnswer("Tell me about pizza.").ShouldBeNull();
    }

    [Fact]
    public void AddExchange_DoesNotThrow()
    {
        var engine = CreateEngine(maxTurns: 3);
        engine.GenerateQuestion();
        engine.AddExchange("Tell me about pizza.", "I like pizza", "That's nice!");
        var next = engine.GenerateQuestion();
        next.ShouldNotBeNull();
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var engine = CreateEngine(maxTurns: 3);
        engine.GenerateQuestion();
        engine.FactsLearned = 5;
        engine.RulesLearned = 3;

        engine.Reset();

        engine.TurnsRemaining.ShouldBe(3);
        engine.FactsLearned.ShouldBe(0);
        engine.RulesLearned.ShouldBe(0);
        engine.GenerateQuestion().ShouldNotBeNull();
    }

    [Fact]
    public void GenerateQuestion_ReturnsNull_WhenNoNounsAvailable()
    {
        var db = new FreshDbContext();
        using (db)
        {
            var store = new KnowledgeStore(db.Context);
            var categoriser = new NounCategoriser(store);
            var engine = new NonLlmInterviewEngine(store, categoriser, maxTurns: 5);
            var result = engine.GenerateQuestion();
            result.ShouldBeNull();
        }
    }
}

using PokeChat.Core;
using Shouldly;
using Xunit;

namespace PokeChat.Tests.Core;

public class NonLlmInterviewEngineTests
{
    [Fact]
    public void Constructor_InitializesTurnsRemaining()
    {
        var engine = new NonLlmInterviewEngine(maxTurns: 8);
        engine.TurnsRemaining.ShouldBe(8);
    }

    [Fact]
    public void Constructor_FactsAndRulesStartAtZero()
    {
        var engine = new NonLlmInterviewEngine(maxTurns: 8);
        engine.FactsLearned.ShouldBe(0);
        engine.RulesLearned.ShouldBe(0);
    }

    [Fact]
    public void GenerateUserInput_ReturnsAQuestion()
    {
        var engine = new NonLlmInterviewEngine(maxTurns: 8);
        var result = engine.GenerateUserInput();
        result.ShouldNotBeNull();
        result.ShouldNotBeEmpty();
    }

    [Fact]
    public void GenerateUserInput_DecrementsTurnsRemaining()
    {
        var engine = new NonLlmInterviewEngine(maxTurns: 3);
        engine.GenerateUserInput();
        engine.TurnsRemaining.ShouldBe(2);
    }

    [Fact]
    public void GenerateUserInput_ReturnsNull_WhenTurnsExhausted()
    {
        var engine = new NonLlmInterviewEngine(maxTurns: 1);
        engine.GenerateUserInput();
        var result = engine.GenerateUserInput();
        result.ShouldBeNull();
    }

    [Fact]
    public void GenerateUserInput_DoesNotRepeatQuestions()
    {
        var engine = new NonLlmInterviewEngine(maxTurns: 5);
        var questions = new List<string?>();
        for (int i = 0; i < 5; i++)
            questions.Add(engine.GenerateUserInput());

        questions.ShouldAllBe(q => q != null);
        questions.Distinct().Count().ShouldBe(5);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var engine = new NonLlmInterviewEngine(maxTurns: 3);
        engine.GenerateUserInput();
        engine.FactsLearned = 5;
        engine.RulesLearned = 3;

        engine.Reset();

        engine.TurnsRemaining.ShouldBe(3);
        engine.FactsLearned.ShouldBe(0);
        engine.RulesLearned.ShouldBe(0);
        engine.GenerateUserInput().ShouldNotBeNull();
    }

    [Fact]
    public void AddExchange_DoesNotThrow()
    {
        var engine = new NonLlmInterviewEngine(maxTurns: 3);
        engine.GenerateUserInput();
        engine.AddExchange("user input", "bot response");
        var next = engine.GenerateUserInput();
        next.ShouldNotBeNull();
    }
}

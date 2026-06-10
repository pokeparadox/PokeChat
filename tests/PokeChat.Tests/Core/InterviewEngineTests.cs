using PokeChat.Core;
using PokeChat.LLM;
using PokeChat.Tests.LLM;
using Shouldly;
using Xunit;

namespace PokeChat.Tests.Core;

public class InterviewEngineTests
{
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
    public void GenerateUserInput_ReturnsLLMResponse()
    {
        var engine = CreateEngine("Hi there!", maxTurns: 8);
        var result = engine.GenerateUserInput();
        result.ShouldBe("Hi there!");
    }

    [Fact]
    public void GenerateUserInput_DecrementsTurnsRemaining()
    {
        var engine = CreateEngine("Hi.", maxTurns: 3);
        engine.GenerateUserInput();
        engine.TurnsRemaining.ShouldBe(2);
    }

    [Fact]
    public void GenerateUserInput_ReturnsNull_WhenTurnsExhausted()
    {
        var engine = CreateEngine("Hi.", maxTurns: 1);
        engine.GenerateUserInput(); // consumes the one turn
        var result = engine.GenerateUserInput();
        result.ShouldBeNull();
    }

    [Fact]
    public void GenerateUserInput_ReturnsNull_WhenLLMUnavailable()
    {
        // LLM returning null means unavailable
        var provider = new StubLLMProvider { Response = null };
        var config = new LLMConfig { Enabled = true };
        var orchestrator = new LLMOrchestrator(provider, config);
        var engine = new InterviewEngine(orchestrator, maxTurns: 3);
        var result = engine.GenerateUserInput();
        result.ShouldBeNull();
    }

    [Fact]
    public void GenerateUserInput_DoesNotDecrementOnNullResponse()
    {
        var provider = new StubLLMProvider { Response = null };
        var config = new LLMConfig { Enabled = true };
        var orchestrator = new LLMOrchestrator(provider, config);
        var engine = new InterviewEngine(orchestrator, maxTurns: 3);
        engine.GenerateUserInput();
        engine.TurnsRemaining.ShouldBe(3); // unchanged
    }

    [Fact]
    public void AddExchange_StoresExchange()
    {
        var engine = CreateEngine("Hello.", maxTurns: 8);
        // First call returns the intro prompt LLM response
        var firstResponse = engine.GenerateUserInput();
        // After add exchange, the second call should include conversation history
        engine.AddExchange("user input", "bot response");
        var secondResponse = engine.GenerateUserInput();
        // Just verify it doesn't crash and returns something
        secondResponse.ShouldNotBeNull();
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var engine = CreateEngine("Hi.", maxTurns: 3);
        engine.GenerateUserInput(); // decrements to 2
        engine.AddExchange("a", "b");
        engine.FactsLearned = 5;
        engine.RulesLearned = 3;

        engine.Reset();

        engine.TurnsRemaining.ShouldBe(3);
        engine.FactsLearned.ShouldBe(0);
        engine.RulesLearned.ShouldBe(0);
        // After reset, should generate response again (not null from exhaustion)
        engine.GenerateUserInput().ShouldNotBeNull();
    }

    private static InterviewEngine CreateEngine(string response, int maxTurns = 8)
    {
        var provider = new StubLLMProvider { Response = response };
        var config = new LLMConfig { Enabled = true };
        var orchestrator = new LLMOrchestrator(provider, config);
        return new InterviewEngine(orchestrator, maxTurns);
    }
}

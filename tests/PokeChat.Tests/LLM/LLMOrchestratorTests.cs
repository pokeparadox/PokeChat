using PokeChat.LLM;
using Shouldly;

namespace PokeChat.Tests.LLM;

public class LLMOrchestratorTests
{
    [Fact]
    public void NotConfigured_WhenConfigDisabled_IsNotAvailable()
    {
        var config = new LLMConfig { Enabled = false };
        var provider = new StubLLMProvider();
        var orchestrator = new LLMOrchestrator(provider, config);

        orchestrator.IsAvailable.ShouldBeFalse();
        orchestrator.UserDeclined.ShouldBeFalse();
        orchestrator.IsAccepted.ShouldBeFalse();
    }

    [Fact]
    public void Configured_ReturnsResponse()
    {
        var config = new LLMConfig { Enabled = true };
        var provider = new StubLLMProvider { Response = "Hello from AI!" };
        var orchestrator = new LLMOrchestrator(provider, config);

        orchestrator.IsAvailable.ShouldBeTrue();
        var result = orchestrator.GenerateResponse("test input");
        result.ShouldBe("Hello from AI!");
    }

    [Fact]
    public void UserDeclines_NotAskedAgain()
    {
        var config = new LLMConfig { Enabled = true };
        var provider = new StubLLMProvider();
        var orchestrator = new LLMOrchestrator(provider, config);

        orchestrator.MarkDeclined();
        orchestrator.UserDeclined.ShouldBeTrue();
        orchestrator.GenerateResponse("test input").ShouldBeNull();
    }

    [Fact]
    public void ProviderError_ReturnsNull()
    {
        var config = new LLMConfig { Enabled = true };
        var provider = new StubLLMProvider { Response = null };
        var orchestrator = new LLMOrchestrator(provider, config);

        var result = orchestrator.GenerateResponse("test input");
        result.ShouldBeNull();
    }

    [Fact]
    public void MaxCallsPerSession_Respected()
    {
        var config = new LLMConfig { Enabled = true, MaxCallsPerSession = 2 };
        var provider = new StubLLMProvider { Response = "OK" };
        var orchestrator = new LLMOrchestrator(provider, config);

        orchestrator.GenerateResponse("first").ShouldBe("OK");
        orchestrator.CallsThisSession.ShouldBe(1);
        orchestrator.GenerateResponse("second").ShouldBe("OK");
        orchestrator.CallsThisSession.ShouldBe(2);
        orchestrator.GenerateResponse("third").ShouldBeNull();
        orchestrator.CallsThisSession.ShouldBe(2);
    }

    [Fact]
    public void AcceptedThenDeclined_DeclineWins()
    {
        var config = new LLMConfig { Enabled = true };
        var provider = new StubLLMProvider { Response = "response" };
        var orchestrator = new LLMOrchestrator(provider, config);

        orchestrator.MarkAccepted();
        orchestrator.IsAccepted.ShouldBeTrue();
        orchestrator.MarkDeclined();
        orchestrator.UserDeclined.ShouldBeTrue();
        orchestrator.GenerateResponse("test").ShouldBeNull();
    }

    [Fact]
    public void MarkAccepted_AllowsLLMUse()
    {
        var config = new LLMConfig { Enabled = true };
        var provider = new StubLLMProvider { Response = "accepted response" };
        var orchestrator = new LLMOrchestrator(provider, config);

        orchestrator.MarkAccepted();
        orchestrator.GenerateResponse("hello").ShouldBe("accepted response");
    }
}

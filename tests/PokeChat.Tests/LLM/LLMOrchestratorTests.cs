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

    [Fact]
    public void AlwaysOn_IsAvailable_WithoutAccept()
    {
        var config = new LLMConfig { Enabled = true, AlwaysOn = true };
        var provider = new StubLLMProvider { Response = "always on response" };
        var orchestrator = new LLMOrchestrator(provider, config);

        orchestrator.IsAccepted.ShouldBeFalse();
        orchestrator.IsAvailable.ShouldBeTrue();
        var result = orchestrator.GenerateResponse("hello");
        result.ShouldBe("always on response");
    }

    [Fact]
    public void AlwaysOn_IgnoresMaxCalls()
    {
        var config = new LLMConfig { Enabled = true, AlwaysOn = true, MaxCallsPerSession = 1 };
        var provider = new StubLLMProvider { Response = "ok" };
        var orchestrator = new LLMOrchestrator(provider, config);

        orchestrator.GenerateResponse("first").ShouldBe("ok");
        orchestrator.CallsThisSession.ShouldBe(1);
        orchestrator.GenerateResponse("second").ShouldBe("ok");
        orchestrator.CallsThisSession.ShouldBe(2);
    }

    [Fact]
    public void AlwaysOn_RespectsUserDeclined()
    {
        var config = new LLMConfig { Enabled = true, AlwaysOn = true };
        var provider = new StubLLMProvider { Response = "should not see this" };
        var orchestrator = new LLMOrchestrator(provider, config);

        orchestrator.MarkDeclined();
        orchestrator.GenerateResponse("hello").ShouldBeNull();
    }

    [Fact]
    public void GenerateGameStorySummary_ReturnsSummary()
    {
        var config = new LLMConfig { Enabled = true };
        var provider = new StubLLMProvider { Response = "A cat and a dog became friends." };
        var orchestrator = new LLMOrchestrator(provider, config);

        var result = orchestrator.GenerateGameStorySummary("cat dog friend");
        result.ShouldBe("A cat and a dog became friends.");
    }

    [Fact]
    public void GenerateGameStorySummary_Unavailable_ReturnsNull()
    {
        var config = new LLMConfig { Enabled = true };
        var provider = new StubLLMProvider { Response = null };
        var orchestrator = new LLMOrchestrator(provider, config);

        var result = orchestrator.GenerateGameStorySummary("cat dog");
        result.ShouldBeNull();
    }

    [Fact]
    public void GenerateGameStorySummary_Declined_ReturnsNull()
    {
        var config = new LLMConfig { Enabled = true };
        var provider = new StubLLMProvider();
        var orchestrator = new LLMOrchestrator(provider, config);

        orchestrator.MarkDeclined();
        var result = orchestrator.GenerateGameStorySummary("cat dog");
        result.ShouldBeNull();
    }

    [Fact]
    public void MaxCallsPerSession_Respected_ForGenerateWordForGame()
    {
        var config = new LLMConfig { Enabled = true, MaxCallsPerSession = 2 };
        var provider = new StubLLMProvider { Response = "elephant" };
        var orchestrator = new LLMOrchestrator(provider, config);

        orchestrator.GenerateWordForGame("once upon").ShouldNotBeNull();
        orchestrator.GenerateWordForGame("a time").ShouldNotBeNull();
        orchestrator.GenerateWordForGame("there was").ShouldBeNull();
        orchestrator.CallsThisSession.ShouldBe(2);
    }

    [Fact]
    public void MaxCallsPerSession_Respected_ForGenerateGameStorySummary()
    {
        var config = new LLMConfig { Enabled = true, MaxCallsPerSession = 1 };
        var provider = new StubLLMProvider { Response = "A story." };
        var orchestrator = new LLMOrchestrator(provider, config);

        orchestrator.GenerateGameStorySummary("cat dog").ShouldBe("A story.");
        orchestrator.GenerateGameStorySummary("cat dog").ShouldBeNull();
        orchestrator.CallsThisSession.ShouldBe(1);
    }

    [Fact]
    public void MaxCallsPerSession_Respected_ForGenerateHomeworkCheck()
    {
        var config = new LLMConfig { Enabled = true, MaxCallsPerSession = 1 };
        var provider = new StubLLMProvider { Response = "{}" };
        var orchestrator = new LLMOrchestrator(provider, config);

        orchestrator.GenerateHomeworkCheck("review this").ShouldBe("{}");
        orchestrator.GenerateHomeworkCheck("review that").ShouldBeNull();
        orchestrator.CallsThisSession.ShouldBe(1);
    }

    [Fact]
    public void MaxCallsPerSession_Respected_ForGenerateInterviewInput()
    {
        var config = new LLMConfig { Enabled = true, MaxCallsPerSession = 1 };
        var provider = new StubLLMProvider { Response = "What is your name?" };
        var orchestrator = new LLMOrchestrator(provider, config);

        orchestrator.GenerateInterviewInput("ask something").ShouldBe("What is your name?");
        orchestrator.GenerateInterviewInput("ask again").ShouldBeNull();
        orchestrator.CallsThisSession.ShouldBe(1);
    }

    [Fact]
    public void MaxCallsPerSession_Respected_ForGenerateTrainingLabels()
    {
        var config = new LLMConfig { Enabled = true, MaxCallsPerSession = 1 };
        var provider = new StubLLMProvider { Response = "{\"labels\":[]}" };
        var orchestrator = new LLMOrchestrator(provider, config);

        orchestrator.GenerateTrainingLabels("[]").ShouldBe("{\"labels\":[]}");
        orchestrator.GenerateTrainingLabels("[]").ShouldBeNull();
        orchestrator.CallsThisSession.ShouldBe(1);
    }

    [Fact]
    public void AlwaysOn_Bypasses_MaxCalls_ForAllMethods()
    {
        var config = new LLMConfig { Enabled = true, AlwaysOn = true, MaxCallsPerSession = 1 };
        var provider = new StubLLMProvider { Response = "ok" };
        var orchestrator = new LLMOrchestrator(provider, config);

        orchestrator.GenerateResponse("first").ShouldBe("ok");
        orchestrator.GenerateResponse("second").ShouldBe("ok");
        orchestrator.GenerateWordForGame("story").ShouldNotBeNull();
        orchestrator.GenerateGameStorySummary("words").ShouldBe("ok");
        orchestrator.GenerateHomeworkCheck("prompt").ShouldBe("ok");
        orchestrator.GenerateInterviewInput("prompt").ShouldBe("ok");
        orchestrator.GenerateTrainingLabels("json").ShouldBe("ok");
    }

    [Fact]
    public void Shared_MaxCalls_Counter_Across_All_Methods()
    {
        var config = new LLMConfig { Enabled = true, MaxCallsPerSession = 3 };
        var provider = new StubLLMProvider { Response = "ok" };
        var orchestrator = new LLMOrchestrator(provider, config);

        orchestrator.GenerateResponse("1").ShouldBe("ok");
        orchestrator.GenerateWordForGame("2").ShouldNotBeNull();
        orchestrator.GenerateHomeworkCheck("3").ShouldBe("ok");
        orchestrator.GenerateResponse("4").ShouldBeNull();
        orchestrator.CallsThisSession.ShouldBe(3);
    }
}

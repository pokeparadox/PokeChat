using PokeChat.LLM;

namespace PokeChat.Tests.LLM;

public class StubLLMProvider : ILLMProvider
{
    public string? Response { get; set; } = "Test response from AI.";
    public bool ShouldThrow { get; set; }

    public string? GenerateResponse(string userInput, string? systemPrompt = null)
    {
        if (ShouldThrow) return null;
        return Response;
    }
}

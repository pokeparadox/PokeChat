namespace PokeChat.LLM;

public interface ILLMProvider
{
    string? GenerateResponse(string userInput, string? systemPrompt = null);
}

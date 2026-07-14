using PokeChat.Core;

namespace PokeChat.Api.Services;

public static class SystemPromptMapper
{
    private static readonly HashSet<string> CodingPersonaKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "coding assistant", "write code", "software engineer", "programmer",
        "developer", "coding helper", "code assistant", "code helper",
        "pair programmer", "coding copilot"
    };

    private static readonly HashSet<string> ChatPersonaKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "friendly chat", "chat companion", "conversational companion",
        "friendly companion", "chat bot", "chatbot companion"
    };

    private static readonly HashSet<string> ConciseKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "be concise", "be brief", "keep it short", "short answers",
        "brief responses", "concise responses", "terse"
    };

    private static readonly HashSet<string> DetailedKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "be detailed", "be thorough", "detailed responses", "thorough explanations",
        "long answers", "verbose", "explain in detail"
    };

    public static SystemPromptResult Parse(string? systemPrompt)
    {
        if (string.IsNullOrWhiteSpace(systemPrompt))
            return new SystemPromptResult();

        var result = new SystemPromptResult { OriginalPrompt = systemPrompt };

        foreach (var keyword in CodingPersonaKeywords)
        {
            if (systemPrompt.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                result.Persona = "coding";
                break;
            }
        }

        if (result.Persona == null)
        {
            foreach (var keyword in ChatPersonaKeywords)
            {
                if (systemPrompt.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                {
                    result.Persona = "chat";
                    break;
                }
            }
        }

        foreach (var keyword in ConciseKeywords)
        {
            if (systemPrompt.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                result.ResponseLength = "concise";
                break;
            }
        }

        if (result.ResponseLength == null)
        {
            foreach (var keyword in DetailedKeywords)
            {
                if (systemPrompt.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                {
                    result.ResponseLength = "detailed";
                    break;
                }
            }
        }

        return result;
    }

    public static void Apply(ChatEngine engine, string? systemPrompt)
    {
        var result = Parse(systemPrompt);

        if (result.Persona != null)
            engine.SwitchPersona(result.Persona);

        if (result.ResponseLength != null)
            engine.ApplySystemConfig(result.ResponseLength);
    }
}

public class SystemPromptResult
{
    public string? Persona { get; set; }
    public string? ResponseLength { get; set; }
    public string? OriginalPrompt { get; set; }
}

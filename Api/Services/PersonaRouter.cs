namespace PokeChat.Api.Services;

public static class PersonaRouter
{
    private static readonly HashSet<string> CodingClientPatterns = new(StringComparer.OrdinalIgnoreCase)
    {
        "opencode", "github-copilot", "github copilot", "copilot"
    };

    public static (string Persona, string? Warning) ResolvePersona(string? modelName, string? userAgent)
    {
        var model = modelName?.Trim().ToLowerInvariant() ?? "";

        if (model == "pokecode-v1")
            return ("coding", null);

        var isCodingClient = !string.IsNullOrEmpty(userAgent) &&
            CodingClientPatterns.Any(p => userAgent.Contains(p, StringComparison.OrdinalIgnoreCase));

        if (isCodingClient && model != "pokecode-v1")
        {
            return ("coding",
                "Note: Detected coding assistant client. Auto-switched to pokecode-v1 persona. " +
                "For best results, set model to \"pokecode-v1\" in your client configuration.");
        }

        return ("chat", null);
    }
}

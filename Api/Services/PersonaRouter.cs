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

        if (IsOpenCodeEnvironment())
            return ("coding", null);

        var isCodingClient = !string.IsNullOrEmpty(userAgent) &&
            CodingClientPatterns.Any(p => userAgent.Contains(p, StringComparison.OrdinalIgnoreCase));

        if (isCodingClient)
        {
            return ("coding",
                "Note: Detected coding assistant client. Auto-switched to pokecode-v1 persona. " +
                "For best results, set model to \"pokecode-v1\" in your client configuration.");
        }

        return ("chat", null);
    }

    private static bool IsOpenCodeEnvironment()
    {
        try
        {
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OPENCODE_API_KEY")))
                return true;
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OPENCODE_SESSION_ID")))
                return true;
            if (string.Equals(Environment.GetEnvironmentVariable("OPENCODE_ENV"), "opencode", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        catch
        {
        }
        return false;
    }
}

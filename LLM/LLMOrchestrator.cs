using System.Text.Json;

namespace PokeChat.LLM;

public class LLMConfig
{
    public bool Enabled { get; set; }
    public bool AlwaysOn { get; set; }
    public string Endpoint { get; set; } = "http://localhost:11434";
    public string Model { get; set; } = "llama3.2";
    public int TimeoutMs { get; set; } = 30000;
    public int MaxCallsPerSession { get; set; }
    public bool SummariseToolResults { get; set; } = true;
    public List<string> EnhancedCategories { get; set; } = new();
    public string SystemPrompt { get; set; } = string.Empty;
}

public class LLMOrchestrator : IDisposable
{
    private readonly ILLMProvider? _provider;
    public LLMConfig Config { get; } = new();
    public bool IsAvailable => _provider != null && Config.Enabled;
    public bool IsAccepted { get; private set; }
    public bool UserDeclined { get; private set; }
    public int CallsThisSession { get; private set; }

    public LLMOrchestrator(string configPath = "tools/llm.json")
    {
        Config = LoadConfig(configPath);
        if (Config != null && Config.Enabled && !string.IsNullOrEmpty(Config.Endpoint))
            _provider = new OllamaProvider(Config.Endpoint, Config.Model, Config.TimeoutMs);
    }

    internal LLMOrchestrator(ILLMProvider provider, LLMConfig config)
    {
        _provider = provider;
        Config = config;
    }

    public void MarkAccepted()
    {
        IsAccepted = true;
    }

    public void MarkDeclined()
    {
        UserDeclined = true;
    }

    public string? GenerateResponse(string input)
    {
        if (_provider == null || UserDeclined) return null;
        if (!Config.AlwaysOn && Config.MaxCallsPerSession > 0 && CallsThisSession >= Config.MaxCallsPerSession)
            return null;

        CallsThisSession++;
        return _provider.GenerateResponse(input, Config.SystemPrompt);
    }

    public string? GenerateWordForGame(string storySoFar)
    {
        if (_provider == null || UserDeclined) return null;

        var systemPrompt = "You are playing a word game. Add EXACTLY ONE word that continues the story in a " +
            "funny or interesting way, following proper grammar. Return ONLY that word, no punctuation or explanation.";
        var result = _provider.GenerateResponse($"The story so far is: '{storySoFar}'", systemPrompt);
        if (string.IsNullOrEmpty(result)) return null;

        var firstWord = result.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
        return firstWord.ToLowerInvariant();
    }

    public void Dispose()
    {
        if (_provider is IDisposable disposable)
            disposable.Dispose();
    }

    private static LLMConfig LoadConfig(string configPath)
    {
        if (!Path.IsPathRooted(configPath))
        {
            var root = Data.ProjectPathHelper.FindProjectRoot(AppContext.BaseDirectory);
            if (root != null)
                configPath = Path.Combine(root, configPath);
        }

        if (!File.Exists(configPath)) return new LLMConfig();

        try
        {
            var json = File.ReadAllText(configPath);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<LLMConfig>(json, options) ?? new LLMConfig();
        }
        catch
        {
            return new LLMConfig();
        }
    }
}

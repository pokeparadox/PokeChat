using System.Text.Json;

namespace PokeChat.Tools;

public class ToolConfig
{
    public bool Enabled { get; set; }
    public int TimeoutMs { get; set; } = 10000;
    public string? ApiKey { get; set; }
    public string? Endpoint { get; set; }
}

public class ToolRegistry
{
    private readonly Dictionary<string, ITool> _tools = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ToolConfig> _configs;

    public ToolRegistry(string configPath = "Tools/tools.json")
    {
        _configs = LoadConfig(configPath);
        RegisterBuiltIn();
    }

    internal ToolRegistry(Dictionary<string, ToolConfig> configs)
    {
        _configs = configs ?? new Dictionary<string, ToolConfig>();
        RegisterBuiltIn();
    }

    private static Dictionary<string, ToolConfig> LoadConfig(string path)
    {
        if (!File.Exists(path))
            return new Dictionary<string, ToolConfig>();

        var json = File.ReadAllText(path);
        json = ResolveEnvVars(json);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        return JsonSerializer.Deserialize<Dictionary<string, ToolConfig>>(json, options)
               ?? new Dictionary<string, ToolConfig>();
    }

    private static string ResolveEnvVars(string json)
    {
        var envVarPattern = new System.Text.RegularExpressions.Regex(@"\$\{(\w+)\}");
        return envVarPattern.Replace(json, match =>
        {
            var varName = match.Groups[1].Value;
            return Environment.GetEnvironmentVariable(varName) ?? match.Value;
        });
    }

    private void RegisterBuiltIn()
    {
        var tools = new ITool[]
        {
            new WebSearchTool(),
            new ReadUrlTool(),
        };

        foreach (var tool in tools)
        {
            _tools[tool.Name] = tool;
        }
    }

    public ToolResult? TryExecute(string toolName, string[] args)
    {
        if (!_configs.TryGetValue(toolName, out var config) || !config.Enabled)
            return null;

        if (!_tools.TryGetValue(toolName, out var tool))
            return null;

        try
        {
            using var cts = new CancellationTokenSource(config.TimeoutMs);
            var task = Task.Run(() => tool.Execute(args), cts.Token);
            if (!task.Wait(config.TimeoutMs, cts.Token))
            {
                return new ToolResult
                {
                    Success = false,
                    Output = string.Empty,
                    ErrorMessage = "timeout"
                };
            }

            return task.Result;
        }
        catch (OperationCanceledException)
        {
            return new ToolResult
            {
                Success = false,
                Output = string.Empty,
                ErrorMessage = "timeout"
            };
        }
        catch (Exception ex)
        {
            return new ToolResult
            {
                Success = false,
                Output = string.Empty,
                ErrorMessage = ex.Message
            };
        }
    }

    public bool IsEnabled(string toolName)
    {
        return _configs.TryGetValue(toolName, out var config) && config.Enabled;
    }

    public ToolConfig? GetConfig(string toolName)
    {
        return _configs.TryGetValue(toolName, out var config) ? config : null;
    }
}

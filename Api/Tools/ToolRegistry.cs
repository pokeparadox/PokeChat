using System.Text.Json;
using PokeChat.Mcp;

namespace PokeChat.Tools;

public class ToolConfig
{
    public bool Enabled { get; set; }
    public int TimeoutMs { get; set; } = 10000;
    public string? ApiKey { get; set; }
    public string? Endpoint { get; set; }
    public List<string>? AllowedCommands { get; set; }
    public List<string>? AllowedPaths { get; set; }
}

public class ToolRegistry
{
    private readonly Dictionary<string, ITool> _tools = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ToolConfig> _configs;

    public ToolRegistry(string configPath = "Tools/tools.json", McpRegistry? mcpRegistry = null)
    {
        _configs = LoadConfig(configPath);
        RegisterBuiltIn();
        RegisterMcpTools(mcpRegistry);
    }

    internal ToolRegistry(Dictionary<string, ToolConfig> configs, McpRegistry? mcpRegistry = null)
    {
        _configs = configs ?? new Dictionary<string, ToolConfig>();
        RegisterBuiltIn();
        RegisterMcpTools(mcpRegistry);
    }

    private void RegisterMcpTools(McpRegistry? mcpRegistry)
    {
        if (mcpRegistry == null) return;

        foreach (var (name, tool) in mcpRegistry.DiscoveredTools)
        {
            _tools[name] = tool;
        }
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
        var shellTool = new ShellCommandTool();
        if (_configs.TryGetValue("shell_command", out var shellConfig) && shellConfig.AllowedCommands is { Count: > 0 })
            shellTool = new ShellCommandTool(shellConfig.AllowedCommands);

        var fileOpsTool = new FileOpsTool();
        if (_configs.TryGetValue("file_ops", out var fileConfig) && fileConfig.AllowedPaths is { Count: > 0 })
            fileOpsTool = new FileOpsTool(fileConfig.AllowedPaths);

        var tools = new ITool[]
        {
            new WebSearchTool(),
            new ReadUrlTool(),
            shellTool,
            fileOpsTool,
            new MempalaceDrawerTool(),
        };

        foreach (var tool in tools)
        {
            _tools[tool.Name] = tool;
        }
    }

    public ToolResult? TryExecute(string toolName, string[] args)
    {
        if (!_tools.TryGetValue(toolName, out var tool))
            return null;

        if (!IsEnabled(toolName))
            return null;

        var timeoutMs = _configs.TryGetValue(toolName, out var config) ? config.TimeoutMs : 10000;

        try
        {
            using var cts = new CancellationTokenSource(timeoutMs);
            var task = Task.Run(() => tool.Execute(args), cts.Token);
            if (!task.Wait(timeoutMs, cts.Token))
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
        if (_configs.TryGetValue(toolName, out var config))
            return config.Enabled;

        return _tools.ContainsKey(toolName);
    }

    public ToolConfig? GetConfig(string toolName)
    {
        return _configs.TryGetValue(toolName, out var config) ? config : null;
    }

    public ITool? GetTool(string toolName)
    {
        return _tools.TryGetValue(toolName, out var tool) ? tool : null;
    }
}

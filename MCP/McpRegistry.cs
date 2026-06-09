using System.Text.Json;
using PokeChat.Data;
using PokeChat.Responses;

namespace PokeChat.Mcp;

public class McpRegistry : IDisposable
{
    private readonly List<McpClient> _clients = new();
    private readonly Dictionary<string, McpToolAdapter> _tools = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, McpServerConfig> _serverConfigs = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _connectedServers = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public IReadOnlyDictionary<string, McpToolAdapter> DiscoveredTools => _tools;

    public McpRegistry(string configPath = "mcp.json")
    {
        configPath = ResolveConfigPath(configPath);
        var config = LoadConfig(configPath);
        if (config == null) return;

        foreach (var (name, serverConfig) in config.McpServers)
        {
            if (!serverConfig.Enabled) continue;
            _serverConfigs[name] = serverConfig;
            ConnectServer(name, serverConfig);
        }
    }

    internal McpRegistry(Dictionary<string, McpToolAdapter> tools)
    {
        foreach (var (name, tool) in tools)
        {
            _tools[name] = tool;
        }
    }

    internal Action<string> Log { get; set; } = Console.Error.WriteLine;

    public List<ResponseRuleRecord> GetToolTriggers()
    {
        var triggers = new List<ResponseRuleRecord>();

        foreach (var (serverName, serverConfig) in _serverConfigs)
        {
            if (!_connectedServers.Contains(serverName)) continue;

            var serverTools = _tools.Values
                .Where(t => t.Name.StartsWith(serverName + "_") || t.Name.Contains(serverName))
                .ToList();

            bool hasExplicitTriggers = serverConfig.ToolTriggers.Count > 0;

            if (hasExplicitTriggers)
            {
                foreach (var trigger in serverConfig.ToolTriggers)
                {
                    triggers.Add(new ResponseRuleRecord
                    {
                        Pattern = trigger.Pattern,
                        InputType = ParseInputType(trigger.InputType),
                        Responses = new List<string>(trigger.Responses),
                        RuleId = -1,
                        IsLearned = false,
                        Confidence = 8
                    });
                }
            }
            else
            {
                foreach (var tool in _tools.Values)
                {
                    var catchAll = McpAutoTriggers.GenerateCatchAll(tool.Name);
                    triggers.Add(new ResponseRuleRecord
                    {
                        Pattern = catchAll.Pattern,
                        InputType = ParseInputType(catchAll.InputType),
                        Responses = new List<string>(catchAll.Responses),
                        RuleId = -1,
                        IsLearned = false,
                        Confidence = 8
                    });
                }
            }
        }

        return triggers;
    }

    internal static InputType ParseInputType(string inputType)
    {
        return inputType.ToLowerInvariant() switch
        {
            "greeting" => InputType.Greeting,
            "question" => InputType.Question,
            "statement" => InputType.Statement,
            _ => InputType.Unknown
        };
    }

    public HashSet<string> GetTriggerKeywords()
    {
        var keywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (serverName, serverConfig) in _serverConfigs)
        {
            if (!_connectedServers.Contains(serverName)) continue;

            if (serverConfig.ToolTriggers.Count > 0)
            {
                foreach (var trigger in serverConfig.ToolTriggers)
                {
                    foreach (var response in trigger.Responses)
                    {
                        var match = System.Text.RegularExpressions.Regex.Match(response, @"\{tool:(\w+)(?::[^}]+)?\}");
                        if (match.Success)
                        {
                            var name = match.Groups[1].Value;
                            foreach (var segment in name.Split('_'))
                            {
                                if (segment.Length > 0)
                                    keywords.Add(segment);
                            }
                        }
                    }
                }
            }
            else
            {
                foreach (var tool in _tools.Values)
                {
                    foreach (var segment in tool.Name.Split('_'))
                    {
                        if (segment.Length > 0)
                            keywords.Add(segment);
                    }
                }
            }
        }

        return keywords;
    }

    private static string ResolveConfigPath(string configPath)
    {
        if (Path.IsPathRooted(configPath)) return configPath;

        var root = ProjectPathHelper.FindProjectRoot(AppContext.BaseDirectory);
        if (root != null)
            return Path.Combine(root, configPath);

        return configPath;
    }

    private void ConnectServer(string name, McpServerConfig serverConfig)
    {
        try
        {
            Log?.Invoke($"MCP: connecting to server '{name}' with command '{serverConfig.Command}'");
            var client = new McpClient(serverConfig.Command, serverConfig.Args);
            if (!client.Connect())
            {
                Log?.Invoke($"MCP: server '{name}' failed to connect");
                return;
            }

            Log?.Invoke($"MCP: server '{name}' connected, discovering tools...");
            var discovered = client.DiscoverTools();
            if (discovered.Count == 0)
            {
                Log?.Invoke($"MCP: server '{name}' returned no tools");
                return;
            }

            _clients.Add(client);
            _connectedServers.Add(name);

            foreach (var tool in discovered)
            {
                Log?.Invoke($"MCP: discovered tool '{tool.Name}' from '{name}'");
                _tools[tool.Name] = tool;
            }

            Log?.Invoke($"MCP: server '{name}' ready with {discovered.Count} tools");
        }
        catch (Exception ex)
        {
            Log?.Invoke($"MCP: server '{name}' error: {ex.GetType().Name}: {ex.Message}");
        }
    }

    public string GetStatus()
    {
        if (_tools.Count == 0) return "No MCP tools connected";
        var groups = _tools.Values
            .GroupBy(t => t.Name.Contains('_') ? t.Name[..t.Name.IndexOf('_')] : "other")
            .Select(g => $"{g.Key}: {g.Count()} tools");
        return $"MCP: {_tools.Count} tools ({string.Join(", ", groups)})";
    }

    public int TriggerCount
    {
        get
        {
            int count = 0;
            foreach (var (serverName, serverConfig) in _serverConfigs)
            {
                if (!_connectedServers.Contains(serverName)) continue;
                if (serverConfig.ToolTriggers.Count > 0)
                    count += serverConfig.ToolTriggers.Count;
                else
                    count += _tools.Values.Count(t => t.Name.StartsWith(serverName + "_") || t.Name.Contains(serverName));
            }
            return count;
        }
    }

    private static McpConfig? LoadConfig(string path)
    {
        if (!File.Exists(path))
            return null;

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<McpConfig>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var client in _clients)
        {
            client.Dispose();
        }
        _clients.Clear();
        _tools.Clear();
    }
}

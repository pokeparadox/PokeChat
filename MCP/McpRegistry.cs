using System.Text.Json;
using PokeChat.Data;

namespace PokeChat.Mcp;

public class McpRegistry : IDisposable
{
    private readonly List<McpClient> _clients = new();
    private readonly Dictionary<string, McpToolAdapter> _tools = new(StringComparer.OrdinalIgnoreCase);
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

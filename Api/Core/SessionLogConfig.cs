using System.Text.Json;
using PokeChat.Data;

namespace PokeChat.Core;

public class SessionLogConfig
{
    public bool Enabled { get; set; } = true;
    public string Mode { get; set; } = "basic";
    public int MaxLogFiles { get; set; } = 50;
    public string Directory { get; set; } = "logs";

    public bool IsVerbose => string.Equals(Mode, "verbose", StringComparison.OrdinalIgnoreCase);

    public static SessionLogConfig Load()
    {
        var root = ProjectPathHelper.FindProjectRoot(AppContext.BaseDirectory);
        if (root == null) return new SessionLogConfig();

        var configPath = Path.Combine(root, "config.json");
        if (!File.Exists(configPath)) return new SessionLogConfig();

        try
        {
            var json = File.ReadAllText(configPath);
            var config = JsonSerializer.Deserialize<ConfigFile>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return config?.Logging ?? new SessionLogConfig();
        }
        catch
        {
            return new SessionLogConfig();
        }
    }

    private class ConfigFile
    {
        public SessionLogConfig? Logging { get; set; }
    }
}

using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using PokeChat.Data;

namespace PokeChat.Api.Services;

public class WeatherApiOptions
{
    public string? ApiKey { get; set; }
    public string BaseUrl { get; set; } = "https://api.openweathermap.org/data/2.5";
    public int CacheMinutes { get; set; } = 30;
    public int TimeoutMs { get; set; } = 5000;
    public bool Enabled => !string.IsNullOrWhiteSpace(ApiKey);

    public static WeatherApiOptions Load()
    {
        var options = new WeatherApiOptions();

        var envKey = Environment.GetEnvironmentVariable("WEATHER_API_KEY");
        if (!string.IsNullOrWhiteSpace(envKey))
            options.ApiKey = envKey;

        var envBase = Environment.GetEnvironmentVariable("WEATHER_API_BASE_URL");
        if (!string.IsNullOrWhiteSpace(envBase))
            options.BaseUrl = envBase;

        var envCache = Environment.GetEnvironmentVariable("WEATHER_CACHE_MINUTES");
        if (int.TryParse(envCache, out var cacheMin))
            options.CacheMinutes = cacheMin;

        // 2) try to read dotnet user-secrets if still missing
        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            try
            {
                var root = ProjectPathHelper.FindProjectRoot(AppContext.BaseDirectory);
                if (!string.IsNullOrEmpty(root))
                {
                    var csproj = Path.Combine(root, "PokeChat.Api.csproj");
                    if (File.Exists(csproj))
                    {
                        var cs = File.ReadAllText(csproj);
                        var m = Regex.Match(cs, @"<UserSecretsId>(.*?)</UserSecretsId>", RegexOptions.IgnoreCase);
                        if (m.Success)
                        {
                            var id = m.Groups[1].Value.Trim();
                            string secretsBase;
                            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                            {

                                secretsBase =
                                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                                        "Microsoft", "UserSecrets");
                            }
                            else
                            {

                                secretsBase =
                                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) ?? "",
                                        ".microsoft", "usersecrets");
                            }
                            var secretsFile = Path.Combine(secretsBase, id, "secrets.json");  
                            using var doc = JsonDocument.Parse(File.ReadAllText(secretsFile));
                            if (doc.RootElement.TryGetProperty("Weather:ApiKey", out var p1) &&
                                p1.ValueKind == JsonValueKind.String)
                                options.ApiKey = p1.GetString();
                            else if (doc.RootElement.TryGetProperty("Weather", out var p2) &&
                                     p2.ValueKind == JsonValueKind.Object && p2.TryGetProperty("ApiKey", out var p3))
                                options.ApiKey = p3.GetString();
                        }
                    }
                }
            }
            catch
            {
                // swallow — best-effort only
            }
        }
        
        return options;
    }
}
    

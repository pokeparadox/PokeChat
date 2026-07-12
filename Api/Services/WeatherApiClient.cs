using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;

namespace PokeChat.Api.Services;

public class WeatherApiClient
{
    private readonly WeatherApiOptions _options;
    private readonly ConcurrentDictionary<string, (WeatherResponse Data, DateTime FetchedAt)> _cache = new(StringComparer.OrdinalIgnoreCase);

    public WeatherApiClient(WeatherApiOptions options)
    {
        _options = options;
    }

    public bool IsEnabled => _options.Enabled;

    public async Task<WeatherResponse?> GetWeatherAsync(string city)
    {
        if (!_options.Enabled)
            return null;

        if (_cache.TryGetValue(city, out var cached) &&
            (DateTime.UtcNow - cached.FetchedAt).TotalMinutes < _options.CacheMinutes)
        {
            return cached.Data;
        }

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(_options.TimeoutMs));
            using var http = new HttpClient();

            var url = $"{_options.BaseUrl}/weather?q={Uri.EscapeDataString(city)}&appid={_options.ApiKey}&units=metric&lang=en";
            var response = await http.GetAsync(url, cts.Token);

            if (!response.IsSuccessStatusCode)
                return null;

            var weather = await response.Content.ReadFromJsonAsync<WeatherResponse>(
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, cts.Token);

            if (weather != null)
            {
                var cacheKey = weather.Name ?? city;
                _cache[cacheKey] = (weather, DateTime.UtcNow);
            }

            return weather;
        }
        catch
        {
            return null;
        }
    }

    public static string ExtractCity(string input)
    {
        var lower = input.ToLowerInvariant().Trim();

        var patterns = new[]
        {
            "weather in ", "weather for ", "weather at ",
            "forecast for ", "forecast in ", "forecast at ",
            "temperature in ", "temperature for ", "temperature at ",
            "how's the weather in ", "how is the weather in ",
            "what's the weather in ", "what is the weather in ",
            "is it raining in ", "is it cold in ", "is it hot in ",
        };

        foreach (var pattern in patterns)
        {
            var idx = lower.IndexOf(pattern, StringComparison.Ordinal);
            if (idx >= 0)
            {
                var after = input[(idx + pattern.Length)..].Trim();
                var words = after.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (words.Length > 0)
                {
                    var cityWords = new List<string>();
                    foreach (var word in words)
                    {
                        var clean = word.TrimEnd('.', '!', '?', ',');
                        if (string.IsNullOrWhiteSpace(clean)) break;
                        if (clean.Length <= 2 && cityWords.Count > 0) break;
                        cityWords.Add(clean);
                    }
                    if (cityWords.Count > 0)
                        return string.Join(' ', cityWords);
                }
            }
        }

        return string.Empty;
    }
}

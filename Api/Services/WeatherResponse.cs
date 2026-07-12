using System.Text.Json.Serialization;

namespace PokeChat.Api.Services;

public class WeatherResponse
{
    [JsonPropertyName("main")]
    public WeatherMain? Main { get; set; }

    [JsonPropertyName("weather")]
    public List<WeatherCondition>? Weather { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("wind")]
    public WeatherWind? Wind { get; set; }

    public double TempCelsius => Main?.Temp ?? 0;
    public string Description => Weather?.FirstOrDefault()?.Description ?? "unknown";
    public string MainCondition => Weather?.FirstOrDefault()?.Main ?? "unknown";
}

public class WeatherMain
{
    [JsonPropertyName("temp")]
    public double Temp { get; set; }

    [JsonPropertyName("feels_like")]
    public double FeelsLike { get; set; }

    [JsonPropertyName("humidity")]
    public int Humidity { get; set; }

    [JsonPropertyName("pressure")]
    public int Pressure { get; set; }
}

public class WeatherCondition
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("main")]
    public string? Main { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("icon")]
    public string? Icon { get; set; }
}

public class WeatherWind
{
    [JsonPropertyName("speed")]
    public double Speed { get; set; }

    [JsonPropertyName("deg")]
    public int Deg { get; set; }
}

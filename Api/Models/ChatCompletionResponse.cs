using System.Text.Json.Serialization;

namespace PokeChat.Api.Models;

public class ChatCompletionResponse
{
    public string Id { get; set; } = $"chatcmpl-{Guid.NewGuid().ToString("N")[..12]}";
    public string Object { get; set; } = "chat.completion";
    public long Created { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    public string Model { get; set; } = "pokechat-v1";
    public List<ChatCompletionChoice> Choices { get; set; } = [];
    public Usage Usage { get; set; } = new();

    [JsonPropertyName("system_fingerprint")]
    public string? SystemFingerprint { get; set; }

    [JsonPropertyName("x_route_info")]
    public RouteInfo? RouteInfo { get; set; }
}

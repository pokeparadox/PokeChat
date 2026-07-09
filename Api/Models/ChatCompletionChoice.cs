using System.Text.Json.Serialization;

namespace PokeChat.Api.Models;

public class ChatCompletionChoice
{
    public int Index { get; set; }
    public ChatMessage Message { get; set; } = new() { Role = "assistant" };

    [JsonPropertyName("finish_reason")]
    public string FinishReason { get; set; } = "stop";

    public object? Logprobs { get; set; }
}

using System.Text.Json.Serialization;

namespace PokeChat.Api.Models;

public class ChatCompletionChunk
{
    public string Id { get; set; } = $"chatcmpl-{Guid.NewGuid().ToString("N")[..12]}";
    public string Object { get; set; } = "chat.completion.chunk";
    public long Created { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    public string Model { get; set; } = "pokechat-v1";
    public string? SystemFingerprint { get; set; }
    public List<ChunkChoice> Choices { get; set; } = [];
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Usage? Usage { get; set; }
}

public class ChunkChoice
{
    public int Index { get; set; }
    public Delta Delta { get; set; } = new();
    public string? FinishReason { get; set; }
    public object? Logprobs { get; set; }
}

public class Delta
{
    public string? Role { get; set; }
    public string? Content { get; set; }

    [JsonPropertyName("tool_calls")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<StreamingToolCall>? ToolCalls { get; set; }
}

public class StreamingToolCall
{
    public int Index { get; set; }
    public string? Id { get; set; }
    public string Type { get; set; } = "function";
    public FunctionCall Function { get; set; } = new();
}

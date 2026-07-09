using System.Text.Json.Serialization;

namespace PokeChat.Api.Models;

public class ChatMessage
{
    public string Role { get; set; } = "user";

    public string? Content { get; set; }

    public string? Name { get; set; }

    [JsonPropertyName("tool_call_id")]
    public string? ToolCallId { get; set; }

    [JsonPropertyName("tool_calls")]
    public List<ToolCall>? ToolCalls { get; set; }

    public string? Refusal { get; set; }
}

public class ToolCall
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "function";
    public FunctionCall Function { get; set; } = new();
}

public class FunctionCall
{
    public string Name { get; set; } = "";
    public string? Arguments { get; set; }
}

using System.Text.Json.Serialization;

namespace PokeChat.Api.Models;

public class ChatCompletionRequest
{
    public string Model { get; set; } = "pokechat-v1";
    public List<ChatMessage> Messages { get; set; } = [];

    public bool Stream { get; set; } = false;
    public double Temperature { get; set; } = 0.7;

    [JsonPropertyName("top_p")]
    public double? TopP { get; set; }

    public int? N { get; set; }

    [JsonPropertyName("max_tokens")]
    public int? MaxTokens { get; set; }

    [JsonPropertyName("max_completion_tokens")]
    public int? MaxCompletionTokens { get; set; }

    public object? Stop { get; set; }

    [JsonPropertyName("frequency_penalty")]
    public double? FrequencyPenalty { get; set; }

    [JsonPropertyName("presence_penalty")]
    public double? PresencePenalty { get; set; }

    [JsonPropertyName("logit_bias")]
    public Dictionary<string, int>? LogitBias { get; set; }

    public bool? Logprobs { get; set; }

    [JsonPropertyName("top_logprobs")]
    public int? TopLogprobs { get; set; }

    [JsonPropertyName("response_format")]
    public ResponseFormat? ResponseFormat { get; set; }

    public int? Seed { get; set; }

    public List<ToolDefinition>? Tools { get; set; }

    [JsonPropertyName("tool_choice")]
    public object? ToolChoice { get; set; }

    [JsonPropertyName("parallel_tool_calls")]
    public bool? ParallelToolCalls { get; set; }

    public string? User { get; set; }

    public Dictionary<string, string>? Metadata { get; set; }

    public bool? Store { get; set; }

    [JsonPropertyName("reasoning_effort")]
    public string? ReasoningEffort { get; set; }

    [JsonPropertyName("session_id")]
    public string? SessionId { get; set; }
}

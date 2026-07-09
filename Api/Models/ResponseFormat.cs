using System.Text.Json.Serialization;

namespace PokeChat.Api.Models;

public class ResponseFormat
{
    public string Type { get; set; } = "text";

    [JsonPropertyName("json_schema")]
    public JsonSchema? JsonSchema { get; set; }
}

public class JsonSchema
{
    public string Name { get; set; } = "";
    public object? Schema { get; set; }
    public bool? Strict { get; set; }
    public string? Description { get; set; }
}

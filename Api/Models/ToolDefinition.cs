using System.Text.Json.Serialization;

namespace PokeChat.Api.Models;

public class ToolDefinition
{
    public string Type { get; set; } = "function";
    public FunctionDefinition Function { get; set; } = new();
}

public class FunctionDefinition
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public object? Parameters { get; set; }
    public bool? Strict { get; set; }
}

public class ToolChoice
{
    public string Type { get; set; } = "function";
    public ToolChoiceFunction Function { get; set; } = new();
}

public class ToolChoiceFunction
{
    public string Name { get; set; } = "";
}

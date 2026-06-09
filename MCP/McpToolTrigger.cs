namespace PokeChat.Mcp;

public class McpToolTrigger
{
    public string Pattern { get; set; } = string.Empty;
    public string InputType { get; set; } = "Statement";
    public List<string> Responses { get; set; } = new();
}

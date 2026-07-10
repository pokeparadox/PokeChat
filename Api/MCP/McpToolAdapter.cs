using PokeChat.Tools;

namespace PokeChat.Mcp;

public class McpToolAdapter : ITool
{
    private readonly McpClient _client;

    public string Name { get; }
    public string Description { get; }

    public McpToolAdapter(McpClient client, string name, string description)
    {
        _client = client;
        Name = name;
        Description = description;
    }

    public ToolResult Execute(string[] args)
    {
        return _client.ExecuteTool(Name, args);
    }
}

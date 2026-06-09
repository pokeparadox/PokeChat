using System.Text.Json;

namespace PokeChat.Mcp;

public class JsonRpcRequest
{
    public string Jsonrpc { get; set; } = "2.0";
    public int Id { get; set; }
    public string Method { get; set; } = "";
    public object? Params { get; set; }
}

public class JsonRpcResponse
{
    public string Jsonrpc { get; set; } = "2.0";
    public int Id { get; set; }
    public JsonElement? Result { get; set; }
    public JsonRpcError? Error { get; set; }
}

public class JsonRpcError
{
    public int Code { get; set; }
    public string Message { get; set; } = "";
}

public class McpConfig
{
    public Dictionary<string, McpServerConfig> McpServers { get; set; } = new();
}

public class McpServerConfig
{
    public string Transport { get; set; } = "stdio";
    public string Command { get; set; } = "";
    public string[] Args { get; set; } = Array.Empty<string>();
    public bool Enabled { get; set; } = true;
    public List<McpToolTrigger> ToolTriggers { get; set; } = new();
}

public class McpToolSchema
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public JsonElement? InputSchema { get; set; }
}

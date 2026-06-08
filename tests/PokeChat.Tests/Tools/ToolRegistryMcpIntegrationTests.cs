using PokeChat.Mcp;
using PokeChat.Tools;
using Shouldly;

namespace PokeChat.Tests.Tools;

public class ToolRegistryMcpIntegrationTests
{
    [Fact]
    public void ToolRegistry_WithMcpRegistry_MergesTools()
    {
        var client = new McpClient("echo", []);
        var adapter = new McpToolAdapter(client, "mcp_tool", "MCP-powered tool");
        var mcpTools = new Dictionary<string, McpToolAdapter>
        {
            ["mcp_tool"] = adapter
        };
        var mcpRegistry = new McpRegistry(mcpTools);

        var configs = new Dictionary<string, ToolConfig>
        {
            ["web_search"] = new() { Enabled = true }
        };

        var registry = new ToolRegistry(configs, mcpRegistry);
        registry.IsEnabled("web_search").ShouldBeTrue();
        registry.IsEnabled("mcp_tool").ShouldBeTrue();
    }

    [Fact]
    public void ToolRegistry_McpToolCanExecute()
    {
        var client = new McpClient("echo", []);
        var adapter = new McpToolAdapter(client, "mcp_tool", "MCP-powered tool");
        var mcpTools = new Dictionary<string, McpToolAdapter>
        {
            ["mcp_tool"] = adapter
        };
        var mcpRegistry = new McpRegistry(mcpTools);

        var configs = new Dictionary<string, ToolConfig>
        {
            ["mcp_tool"] = new() { Enabled = true }
        };

        var registry = new ToolRegistry(configs, mcpRegistry);

        var result = registry.TryExecute("mcp_tool", ["test"]);
        result.ShouldNotBeNull();
        result.Success.ShouldBeFalse();
    }

    [Fact]
    public void ToolRegistry_WithoutMcpRegistry_WorksAsBefore()
    {
        var configs = new Dictionary<string, ToolConfig>
        {
            ["web_search"] = new() { Enabled = false }
        };

        var registry = new ToolRegistry(configs);
        registry.IsEnabled("web_search").ShouldBeFalse();
    }
}

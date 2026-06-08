using PokeChat.Mcp;
using Shouldly;

namespace PokeChat.Tests.Tools;

public class McpRegistryTests
{
    [Fact]
    public void McpRegistry_NoConfigFile_ReturnsEmptyTools()
    {
        var registry = new McpRegistry("/nonexistent/path/mcp.json");
        registry.DiscoveredTools.ShouldBeEmpty();
    }

    [Fact]
    public void McpRegistry_EmptyConfig_ReturnsEmptyTools()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "{}");
            var registry = new McpRegistry(path);
            registry.DiscoveredTools.ShouldBeEmpty();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void McpRegistry_InvalidJson_ReturnsEmptyTools()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "not valid json");
            var registry = new McpRegistry(path);
            registry.DiscoveredTools.ShouldBeEmpty();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void McpRegistry_PreBuiltTools_ReturnsThem()
    {
        var toolName = "test_tool";
        var client = new McpClient("echo", []);
        var adapter = new McpToolAdapter(client, toolName, "A test tool");
        var tools = new Dictionary<string, McpToolAdapter>
        {
            [toolName] = adapter
        };
        var registry = new McpRegistry(tools);

        registry.DiscoveredTools.ContainsKey(toolName).ShouldBeTrue();
        registry.DiscoveredTools[toolName].Name.ShouldBe(toolName);
        registry.DiscoveredTools[toolName].Description.ShouldBe("A test tool");
    }

    [Fact]
    public void McpRegistry_DisabledServers_AreSkipped()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, @"{
                ""mcpServers"": {
                    ""test-server"": {
                        ""transport"": ""stdio"",
                        ""command"": ""nonexistent-command"",
                        ""args"": [],
                        ""enabled"": false
                    }
                }
            }");
            var registry = new McpRegistry(path);
            registry.DiscoveredTools.ShouldBeEmpty();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void McpRegistry_Dispose_DoesNotThrow()
    {
        var registry = new McpRegistry("/nonexistent");
        Should.NotThrow(() => registry.Dispose());
    }
}

using PokeChat.Mcp;
using Shouldly;

namespace PokeChat.Tests.Tools;

public class McpToolAdapterTests
{
    [Fact]
    public void McpToolAdapter_StoresNameAndDescription()
    {
        var client = new McpClient("echo", []);
        var adapter = new McpToolAdapter(client, "test_tool", "A test tool");
        adapter.Name.ShouldBe("test_tool");
        adapter.Description.ShouldBe("A test tool");
    }

    [Fact]
    public void McpToolAdapter_Execute_NoConnection_ReturnsFailure()
    {
        using var client = new McpClient("nonexistent-command-that-will-fail", []);
        var adapter = new McpToolAdapter(client, "test_tool", "A test tool");
        var result = adapter.Execute(["hello"]);
        result.Success.ShouldBeFalse();
    }

    [Fact]
    public void McpToolAdapter_Execute_EmptyArgs_ReturnsFailure()
    {
        using var client = new McpClient("echo", []);
        var adapter = new McpToolAdapter(client, "test_tool", "A test tool");
        var result = adapter.Execute([]);
        result.Success.ShouldBeFalse();
    }

    [Fact]
    public void McpToolAdapter_MultipleAdapters_ShareClient()
    {
        using var client = new McpClient("echo", []);
        var adapter1 = new McpToolAdapter(client, "tool_a", "Tool A");
        var adapter2 = new McpToolAdapter(client, "tool_b", "Tool B");

        adapter1.Name.ShouldBe("tool_a");
        adapter2.Name.ShouldBe("tool_b");
    }
}

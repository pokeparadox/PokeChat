using System.Text;
using PokeChat.Mcp;
using Shouldly;

namespace PokeChat.Tests.Tools;

public class McpIntegrationTests
{
    private static string CreateMockMcpServerScript()
    {
        var path = Path.GetTempFileName();
        var script = new StringBuilder();
        script.AppendLine("#!/bin/bash");
        script.AppendLine("while IFS= read -r line; do");
        script.AppendLine("    if echo \"$line\" | grep -q initialize; then");
        script.AppendLine("        echo '{\"jsonrpc\":\"2.0\",\"id\":1,\"result\":{\"protocolVersion\":\"2024-11-05\",\"capabilities\":{\"tools\":{}},\"serverInfo\":{\"name\":\"test-server\",\"version\":\"1.0\"}}}'");
        script.AppendLine("    elif echo \"$line\" | grep -q \"tools/list\"; then");
        script.AppendLine("        echo '{\"jsonrpc\":\"2.0\",\"id\":2,\"result\":{\"tools\":[{\"name\":\"test_tool\",\"description\":\"A test tool\",\"inputSchema\":{\"type\":\"object\",\"properties\":{\"query\":{\"type\":\"string\"}}}}]}}'");
        script.AppendLine("    elif echo \"$line\" | grep -q \"tools/call\"; then");
        script.AppendLine("        echo '{\"jsonrpc\":\"2.0\",\"id\":3,\"result\":{\"content\":[{\"type\":\"text\",\"text\":\"Test result\"}]}}'");
        script.AppendLine("    fi");
        script.AppendLine("done");
        File.WriteAllText(path, script.ToString());
        if (OperatingSystem.IsLinux())
            File.SetUnixFileMode(path,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        return path;
    }

    [Fact]
    public void McpClient_ConnectsToMockServer()
    {
        var scriptPath = CreateMockMcpServerScript();
        try
        {
            using var client = new McpClient(scriptPath, []);
            var connected = client.Connect();
            connected.ShouldBeTrue();
        }
        finally
        {
            File.Delete(scriptPath);
        }
    }

    [Fact]
    public void McpClient_DiscoverTools_ReturnsTools()
    {
        var scriptPath = CreateMockMcpServerScript();
        try
        {
            using var client = new McpClient(scriptPath, []);
            client.Connect().ShouldBeTrue();

            var tools = client.DiscoverTools();
            tools.ShouldNotBeEmpty();
            tools.ShouldContain(t => t.Name == "test_tool");
        }
        finally
        {
            File.Delete(scriptPath);
        }
    }

    [Fact]
    public void McpClient_ExecuteTool_ReturnsResult()
    {
        var scriptPath = CreateMockMcpServerScript();
        try
        {
            using var client = new McpClient(scriptPath, []);
            client.Connect().ShouldBeTrue();
            client.DiscoverTools();

            var result = client.ExecuteTool("test_tool", ["hello"]);
            result.Success.ShouldBeTrue();
            result.Output.ShouldContain("Test result");
        }
        finally
        {
            File.Delete(scriptPath);
        }
    }

    [Fact]
    public void McpClient_ExecuteTool_BeforeConnect_ReturnsFailure()
    {
        using var client = new McpClient("echo", []);
        var result = client.ExecuteTool("test_tool", ["hello"]);
        result.Success.ShouldBeFalse();
    }

    [Fact]
    public void McpRegistry_WithMockServer_ConnectsAndDiscoversTools()
    {
        var scriptPath = CreateMockMcpServerScript();
        var configPath = Path.GetTempFileName();
        try
        {
            var configJson = $@"{{
                ""mcpServers"": {{
                    ""test-server"": {{
                        ""transport"": ""stdio"",
                        ""command"": ""{scriptPath.Replace("\\", "\\\\")}"",
                        ""args"": [],
                        ""enabled"": true
                    }}
                }}
            }}";
            File.WriteAllText(configPath, configJson);

            using var registry = new McpRegistry(configPath);
            registry.DiscoveredTools.ShouldNotBeEmpty();
            registry.DiscoveredTools.ContainsKey("test_tool").ShouldBeTrue();
        }
        finally
        {
            File.Delete(scriptPath);
            File.Delete(configPath);
        }
    }

    [Fact]
    public void McpToolAdapter_FullFlow_ExecutesViaClient()
    {
        var scriptPath = CreateMockMcpServerScript();
        try
        {
            using var client = new McpClient(scriptPath, []);
            client.Connect().ShouldBeTrue();
            client.DiscoverTools();

            var adapter = new McpToolAdapter(client, "test_tool", "A test tool");
            var result = adapter.Execute(["world"]);
            result.Success.ShouldBeTrue();
            result.Output.ShouldContain("Test result");
        }
        finally
        {
            File.Delete(scriptPath);
        }
    }
}

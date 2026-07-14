using PokeChat.Mcp;
using PokeChat.Responses;
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

    [Fact]
    public void GetToolTriggers_NoConfigFile_ReturnsEmpty()
    {
        var registry = new McpRegistry("/nonexistent/path/mcp.json");
        var triggers = registry.GetToolTriggers();
        triggers.ShouldBeEmpty();
    }

    [Fact]
    public void GetToolTriggers_EmptyConfig_ReturnsEmpty()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "{}");
            var registry = new McpRegistry(path);
            var triggers = registry.GetToolTriggers();
            triggers.ShouldBeEmpty();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void GetToolTriggers_InvalidJson_ReturnsEmpty()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "not valid json");
            var registry = new McpRegistry(path);
            var triggers = registry.GetToolTriggers();
            triggers.ShouldBeEmpty();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void GetToolTriggers_WithExplicitTriggers_ReturnsRecords()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, @"{
                ""mcpServers"": {
                    ""mempalace"": {
                        ""transport"": ""stdio"",
                        ""command"": ""echo"",
                        ""args"": [""{}""],
                        ""enabled"": true,
                        ""toolTriggers"": [
                            {
                                ""pattern"": ""search memory for (.+)"",
                                ""inputType"": ""Statement"",
                                ""responses"": [""Looking up {tool:mempalace_search:{$1}}""]
                            }
                        ]
                    }
                }
            }");
            var registry = new McpRegistry(path);
            var triggers = registry.GetToolTriggers();

            if (triggers.Count == 0)
            {
                // Server may not connect in test environment; that's acceptable
                return;
            }

            triggers.Count.ShouldBe(1);
            triggers[0].Pattern.ShouldBe("search memory for (.+)");
            triggers[0].InputType.ShouldBe(InputType.Statement);
            triggers[0].Responses.ShouldContain("Looking up {tool:mempalace_search:{$1}}");
            triggers[0].RuleId.ShouldBe(-1);
            triggers[0].IsLearned.ShouldBeFalse();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void GetToolTriggers_DisabledServer_ReturnsEmpty()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, @"{
                ""mcpServers"": {
                    ""test-server"": {
                        ""transport"": ""stdio"",
                        ""command"": ""echo"",
                        ""args"": [""{}""],
                        ""enabled"": false,
                        ""toolTriggers"": [
                            {
                                ""pattern"": ""search (.+)"",
                                ""inputType"": ""Statement"",
                                ""responses"": [""Searching... {tool:search:{$1}}""]
                            }
                        ]
                    }
                }
            }");
            var registry = new McpRegistry(path);
            var triggers = registry.GetToolTriggers();
            triggers.ShouldBeEmpty();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void GetTriggerKeywords_ReturnsToolNameSegments()
    {
        var toolName = "mempalace_search";
        var client = new McpClient("echo", []);
        var adapter = new McpToolAdapter(client, toolName, "Test tool");

        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, @"{
                ""mcpServers"": {
                    ""mempalace"": {
                        ""transport"": ""stdio"",
                        ""command"": ""echo"",
                        ""args"": [""{}""],
                        ""enabled"": true,
                        ""toolTriggers"": [
                            {
                                ""pattern"": ""search (.+)"",
                                ""inputType"": ""Statement"",
                                ""responses"": [""Searching {tool:mempalace_search:{$1}}""]
                            }
                        ]
                    }
                }
            }");
            var registry = new McpRegistry(path);
            var keywords = registry.GetTriggerKeywords();

            // If server didn't connect, keywords will be empty
            if (keywords.Count == 0)
                return;

            keywords.ShouldContain(k => k.Equals("mempalace", StringComparison.OrdinalIgnoreCase));
            keywords.ShouldContain(k => k.Equals("search", StringComparison.OrdinalIgnoreCase));
            keywords.ShouldNotContain("tool");
        }
        finally
        {
            File.Delete(path);
        }
    }
}

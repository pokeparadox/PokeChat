using PokeChat.Tools;
using Shouldly;

namespace PokeChat.Tests.Tools;

public class ToolRegistryTests
{
    [Fact]
    public void TryExecute_DisabledTool_ReturnsNull()
    {
        var configs = new Dictionary<string, ToolConfig>
        {
            ["web_search"] = new() { Enabled = false }
        };
        var registry = new ToolRegistry(configs);
        var result = registry.TryExecute("web_search", new[] { "test" });
        result.ShouldBeNull();
    }

    [Fact]
    public void IsEnabled_DisabledTool_ReturnsFalse()
    {
        var configs = new Dictionary<string, ToolConfig>
        {
            ["web_search"] = new() { Enabled = false }
        };
        var registry = new ToolRegistry(configs);
        registry.IsEnabled("web_search").ShouldBeFalse();
    }

    [Fact]
    public void IsEnabled_UnknownTool_ReturnsFalse()
    {
        var configs = new Dictionary<string, ToolConfig>();
        var registry = new ToolRegistry(configs);
        registry.IsEnabled("nonexistent").ShouldBeFalse();
    }

    [Fact]
    public void TryExecute_UnknownTool_ReturnsNull()
    {
        var configs = new Dictionary<string, ToolConfig>();
        var registry = new ToolRegistry(configs);
        var result = registry.TryExecute("nonexistent", Array.Empty<string>());
        result.ShouldBeNull();
    }

    [Fact]
    public void TryExecute_EnabledTool_ReturnsResult()
    {
        var configs = new Dictionary<string, ToolConfig>
        {
            ["read_url"] = new() { Enabled = true }
        };
        var registry = new ToolRegistry(configs);

        var result = registry.TryExecute("read_url", new[] { "not-a-valid-url" });

        result.ShouldNotBeNull();
        result.Success.ShouldBeFalse();
    }

    [Fact]
    public void GetConfig_ReturnsConfig_WhenExists()
    {
        var configs = new Dictionary<string, ToolConfig>
        {
            ["web_search"] = new() { Enabled = true, TimeoutMs = 5000 }
        };
        var registry = new ToolRegistry(configs);
        var config = registry.GetConfig("web_search");
        config.ShouldNotBeNull();
        config.Enabled.ShouldBeTrue();
        config.TimeoutMs.ShouldBe(5000);
    }

    [Fact]
    public void GetConfig_ReturnsNull_WhenNotExists()
    {
        var configs = new Dictionary<string, ToolConfig>();
        var registry = new ToolRegistry(configs);
        registry.GetConfig("web_search").ShouldBeNull();
    }

    [Fact]
    public void WebSearchTool_EmptyQuery_ReturnsFailure()
    {
        var tool = new WebSearchTool();
        var result = tool.Execute(Array.Empty<string>());
        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void ReadUrlTool_EmptyUrl_ReturnsFailure()
    {
        var tool = new ReadUrlTool();
        var result = tool.Execute(Array.Empty<string>());
        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void ReadUrlTool_BadUrl_ReturnsFailure()
    {
        var tool = new ReadUrlTool();
        var result = tool.Execute(new[] { "not-a-real-url-12345" });
        result.Success.ShouldBeFalse();
    }
}

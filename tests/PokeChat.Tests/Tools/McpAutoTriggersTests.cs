using PokeChat.Mcp;
using PokeChat.Responses;
using Shouldly;

namespace PokeChat.Tests.Tools;

public class McpAutoTriggersTests
{
    [Fact]
    public void GenerateCatchAll_ProducesValidTrigger()
    {
        var trigger = McpAutoTriggers.GenerateCatchAll("mempalace_search");
        trigger.ShouldNotBeNull();
        trigger.Pattern.ShouldContain("mempalace_search");
        trigger.InputType.ShouldBe("Statement");
        trigger.Responses.Count.ShouldBe(1);
        trigger.Responses[0].ShouldContain("{tool:mempalace_search:{$4}}");
    }

    [Fact]
    public void GenerateCatchAll_InputMatches()
    {
        var trigger = McpAutoTriggers.GenerateCatchAll("web_search");
        var match = System.Text.RegularExpressions.Regex.Match("use web_search for cats", trigger.Pattern);
        match.Success.ShouldBeTrue();
        match.Groups[4].Value.ShouldBe("cats");
    }

    [Fact]
    public void GenerateCatchAll_EscapesSpecialChars()
    {
        var trigger = McpAutoTriggers.GenerateCatchAll("my_tool_123");
        var match = System.Text.RegularExpressions.Regex.Match("run my_tool_123 for test", trigger.Pattern);
        match.Success.ShouldBeTrue();
        match.Groups[4].Value.ShouldBe("test");
    }
}

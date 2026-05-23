using PokeChat.Data.Entities;
using PokeChat.Knowledge;
using PokeChat.Responses;
using PokeChat.Tests.Helpers;
using Shouldly;

namespace PokeChat.Tests.Responses;

public class ResponseRulesTests
{
    [Fact]
    public void MatchRule_Greeting_ReturnsRule()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);
        SeedRule(db.Context, "^(hi|hello)", "Greeting", ["Hello there!", "Hi!"]);
        var result = ResponseRules.MatchRule("hi", store);
        result.ShouldNotBeNull();
        result.InputType.ShouldBe(InputType.Greeting);
    }

    [Fact]
    public void MatchRule_NoMatch_ReturnsNull()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);
        SeedRule(db.Context, "^(hi|hello)", "Greeting", ["Hello!"]);
        var result = ResponseRules.MatchRule("goodbye", store);
        result.ShouldBeNull();
    }

    [Fact]
    public void MatchRule_ReturnsResponses()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);
        SeedRule(db.Context, "hello", "Greeting", ["Hi there!", "Hey!"]);
        var result = ResponseRules.MatchRule("hello", store);
        result.ShouldNotBeNull();
        result.Responses.Count.ShouldBe(2);
    }

    private static void SeedRule(PokeChat.Data.PokeChatDbContext context, string pattern, string inputType, string[] responses)
    {
        var rule = new ResponseRule
        {
            Pattern = pattern,
            InputType = inputType,
            IsActive = true,
            CreatedAt = DateTime.UtcNow.ToString("O"),
            Responses = responses.Select(r => new ResponseRuleResponse { ResponseText = r }).ToList()
        };
        context.ResponseRules.Add(rule);
        context.SaveChanges();
    }
}

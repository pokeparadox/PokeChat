using PokeChat.Data.Entities;
using PokeChat.Knowledge;
using PokeChat.NLP;
using PokeChat.Responses;
using PokeChat.Tests.Helpers;
using Shouldly;

namespace PokeChat.Tests.Responses;

public class ResponseEngineTests
{
    [Fact]
    public void GenerateResponse_Default_WhenNoRulesOrFacts()
    {
        using var db = new FreshDbContext();
        var context = new ContextTracker();
        var spellChecker = new SpellChecker();
        spellChecker.Initialize(new HashSet<string>(StringComparer.OrdinalIgnoreCase), new Dictionary<string, string>());
        var knowledgeStore = new KnowledgeStore(db.Context);
        var engine = new ResponseEngine(knowledgeStore, context, spellChecker);
        var response = engine.GenerateResponse("hello", null);
        response.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void GenerateResponse_ReturnsRuleResponse_WhenMatch()
    {
        using var db = new FreshDbContext();
        var context = new ContextTracker();
        var spellChecker = new SpellChecker();
        spellChecker.Initialize(new HashSet<string>(StringComparer.OrdinalIgnoreCase), new Dictionary<string, string>());
        var knowledgeStore = new KnowledgeStore(db.Context);

        db.Context.ResponseRules.Add(new()
        {
            Pattern = "^(hello|hi)",
            InputType = "Greeting",
            IsActive = true,
            CreatedAt = DateTime.UtcNow.ToString("O"),
            Responses = [new() { ResponseText = "Hey there!" }]
        });
        db.Context.SaveChanges();

        var engine = new ResponseEngine(knowledgeStore, context, spellChecker);
        var response = engine.GenerateResponse("hello", null);
        response.ShouldBe("Hey there!");
    }
}

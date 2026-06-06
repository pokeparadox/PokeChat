using PokeChat.Core;
using PokeChat.Knowledge;
using PokeChat.NLP;
using PokeChat.Responses;
using PokeChat.Tests.Helpers;
using PokeChat.Tools;
using Shouldly;

namespace PokeChat.Tests.Responses;

public class ResponseEngineToolTests
{
    private ResponseEngine CreateEngine(PokeChat.Data.PokeChatDbContext db, ContextTracker context, ToolRegistry? toolRegistry = null)
    {
        TestDataHelper.SeedBotResponses(db);
        TestDataHelper.SeedBotResponsesWithToolCategories(db);
        var knowledgeStore = new KnowledgeStore(db);
        var spellChecker = new SpellChecker();
        spellChecker.Initialise(new HashSet<string>(StringComparer.OrdinalIgnoreCase), new Dictionary<string, string>());
        var posTagger = new PosTagger([]);
        var tokeniser = new Tokeniser();
        var svoExtractor = new SvoExtractor();
        return new ResponseEngine(knowledgeStore, context, spellChecker, posTagger, tokeniser, svoExtractor, toolRegistry: toolRegistry);
    }

    [Fact]
    public void ProcessToolMarkers_NoMarker_ReturnsResponseUnchanged()
    {
        using var db = new FreshDbContext();
        var context = new ContextTracker();

        db.Context.ResponseRules.Add(new()
        {
            Pattern = "^(hello|hi)",
            InputType = "Greeting",
            IsActive = true,
            CreatedAt = DateTime.UtcNow.ToString("o"),
            Responses = [new() { ResponseText = "Hey there!" }]
        });
        db.Context.SaveChanges();

        var engine = CreateEngine(db.Context, context);
        var response = engine.GenerateResponse("hello", null);
        response.ShouldBe("Hey there!");
    }

    [Fact]
    public void ProcessToolMarkers_NoToolRegistry_StripsMarker()
    {
        using var db = new FreshDbContext();
        var context = new ContextTracker();

        db.Context.ResponseRules.Add(new()
        {
            Pattern = "^(hello|hi)",
            InputType = "Greeting",
            IsActive = true,
            CreatedAt = DateTime.UtcNow.ToString("o"),
            Responses = [new() { ResponseText = "Let me look. {tool:web_search} Here it is." }]
        });
        db.Context.SaveChanges();

        var engine = CreateEngine(db.Context, context);
        var response = engine.GenerateResponse("hello", null);
        response.ShouldNotContain("{tool:");
        response.ShouldNotContain("web_search");
    }

    [Fact]
    public void ProcessToolMarkers_DisabledTool_ReturnsUnavailable()
    {
        using var db = new FreshDbContext();
        var context = new ContextTracker();
        var configs = new Dictionary<string, ToolConfig>
        {
            ["web_search"] = new() { Enabled = false }
        };
        var registry = new ToolRegistry(configs);

        db.Context.ResponseRules.Add(new()
        {
            Pattern = "^(hello|hi)",
            InputType = "Greeting",
            IsActive = true,
            CreatedAt = DateTime.UtcNow.ToString("o"),
            Responses = [new() { ResponseText = "Let me look. {tool:web_search} Here it is." }]
        });
        db.Context.SaveChanges();

        var engine = CreateEngine(db.Context, context, registry);
        var response = engine.GenerateResponse("hello", null);
        response.ShouldNotContain("{tool:");
        response.ShouldNotBeEmpty();
    }

    [Fact]
    public void ProcessToolMarkers_UnknownTool_ReturnsUnavailable()
    {
        using var db = new FreshDbContext();
        var context = new ContextTracker();
        var configs = new Dictionary<string, ToolConfig>
        {
            ["nonexistent"] = new() { Enabled = true }
        };
        var registry = new ToolRegistry(configs);

        db.Context.ResponseRules.Add(new()
        {
            Pattern = "^(hello|hi)",
            InputType = "Greeting",
            IsActive = true,
            CreatedAt = DateTime.UtcNow.ToString("o"),
            Responses = [new() { ResponseText = "Let me try. {tool:nonexistent} Done." }]
        });
        db.Context.SaveChanges();

        var engine = CreateEngine(db.Context, context, registry);
        var response = engine.GenerateResponse("hello", null);
        response.ShouldNotContain("{tool:");
        response.ShouldNotBeEmpty();
    }
}

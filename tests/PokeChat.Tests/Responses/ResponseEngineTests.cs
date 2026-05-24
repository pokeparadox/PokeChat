using PokeChat.Core;
using PokeChat.Data.Entities;
using PokeChat.Knowledge;
using PokeChat.NLP;
using PokeChat.Responses;
using PokeChat.Tests.Helpers;
using Shouldly;

namespace PokeChat.Tests.Responses;

public class ResponseEngineTests
{
    private ResponseEngine CreateEngine(PokeChat.Data.PokeChatDbContext db, ContextTracker context)
    {
        TestDataHelper.SeedBotResponses(db);
        var knowledgeStore = new KnowledgeStore(db);
        var spellChecker = new SpellChecker();
        spellChecker.Initialise(new HashSet<string>(StringComparer.OrdinalIgnoreCase), new Dictionary<string, string>());
        var posTagger = new PosTagger([]);
        var tokeniser = new Tokeniser();
        var svoExtractor = new SvoExtractor();
        return new ResponseEngine(knowledgeStore, context, spellChecker, posTagger, tokeniser, svoExtractor);
    }

    [Fact]
    public void GenerateResponse_Default_WhenNoRulesOrFacts()
    {
        using var db = new FreshDbContext();
        var context = new ContextTracker();
        var engine = CreateEngine(db.Context, context);
        var response = engine.GenerateResponse("hello", null);
        response.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void GenerateResponse_ReturnsRuleResponse_WhenMatch()
    {
        using var db = new FreshDbContext();
        var context = new ContextTracker();

        db.Context.ResponseRules.Add(new()
        {
            Pattern = "^(hello|hi)",
            InputType = "Greeting",
            IsActive = true,
            CreatedAt = DateTime.UtcNow.ToString("O"),
            Responses = [new() { ResponseText = "Hey there!" }]
        });
        db.Context.SaveChanges();

        var engine = CreateEngine(db.Context, context);
        var response = engine.GenerateResponse("hello", null);
        response.ShouldBe("Hey there!");
    }

    private int SeedUser(PokeChat.Data.PokeChatDbContext db)
    {
        var user = new User { Name = "TestUser", FirstSeen = DateTime.UtcNow.ToString("O"), LastSeen = DateTime.UtcNow.ToString("O") };
        db.Users.Add(user);
        db.SaveChanges();
        return user.Id;
    }

    [Fact]
    public void GenerateResponse_ProactiveQuestion_WhenUserHasFacts()
    {
        using var db = new FreshDbContext();
        var context = new ContextTracker();

        var userId = SeedUser(db.Context);
        db.Context.Facts.Add(new FactEntity
        {
            UserId = userId,
            Subject = "TestUser",
            Verb = "like",
            Object = "pizza",
            PredicateType = "Preference",
            CreatedAt = DateTime.UtcNow.ToString("O")
        });
        db.Context.SaveChanges();

        var engine = CreateEngine(db.Context, context);
        var response = engine.GenerateResponse("hello", userId);
        response.ShouldContain("pizza");
    }

    [Fact]
    public void GenerateResponse_Default_WhenUserHasNoFacts()
    {
        using var db = new FreshDbContext();
        var context = new ContextTracker();

        var userId = SeedUser(db.Context);

        var engine = CreateEngine(db.Context, context);
        var response = engine.GenerateResponse("hello", userId);
        response.ShouldBeOneOf("Interesting! Tell me more.", "I see.");
    }

    [Fact]
    public void GenerateResponse_Default_WhenAllFactsRecentlyUsed()
    {
        using var db = new FreshDbContext();
        var context = new ContextTracker();

        var userId = SeedUser(db.Context);
        db.Context.Facts.Add(new FactEntity
        {
            UserId = userId,
            Subject = "TestUser",
            Verb = "like",
            Object = "pizza",
            PredicateType = "Preference",
            CreatedAt = DateTime.UtcNow.ToString("O")
        });
        db.Context.SaveChanges();

        context.SetContext(ContextKeys.RecentlyUsedFacts, "TestUser|like|pizza");

        var engine = CreateEngine(db.Context, context);
        var response = engine.GenerateResponse("hello", userId);
        response.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void GenerateResponse_ContextFollowUp_Fires_WhenBelowThreshold()
    {
        using var db = new FreshDbContext();
        var context = new ContextTracker();
        context.UpdateLastSubject("TestUser");

        var engine = CreateEngine(db.Context, context);
        var response = engine.GenerateResponse("hello", null);
        response.ShouldBe("Tell me more about TestUser.");
    }

    [Fact]
    public void GenerateResponse_ContextFollowUp_SkipsToProactive_WhenAtThreshold()
    {
        using var db = new FreshDbContext();
        var context = new ContextTracker();
        context.UpdateLastSubject("TestUser");
        context.SetContext(ContextKeys.ContextFollowUpCount, "2");

        var userId = SeedUser(db.Context);
        db.Context.Facts.Add(new FactEntity
        {
            UserId = userId,
            Subject = "TestUser",
            Verb = "like",
            Object = "pizza",
            PredicateType = "Preference",
            CreatedAt = DateTime.UtcNow.ToString("O")
        });
        db.Context.SaveChanges();

        var engine = CreateEngine(db.Context, context);
        var response = engine.GenerateResponse("hello", userId);
        response.ShouldContain("pizza");
    }

    [Fact]
    public void GenerateResponse_ContextFollowUp_SkipsToDefault_WhenAtThresholdAndNoFacts()
    {
        using var db = new FreshDbContext();
        var context = new ContextTracker();
        context.UpdateLastSubject("TestUser");
        context.SetContext(ContextKeys.ContextFollowUpCount, "2");

        var engine = CreateEngine(db.Context, context);
        var response = engine.GenerateResponse("hello", null);
        response.ShouldBeOneOf("Interesting! Tell me more.", "I see.");
    }

    [Fact]
    public void ConjugateVerb_LeavesBaseForm_ForFirstSecondPerson()
    {
        ResponseEngine.ConjugateVerb("like", "I").ShouldBe("like");
        ResponseEngine.ConjugateVerb("like", "you").ShouldBe("like");
        ResponseEngine.ConjugateVerb("like", "we").ShouldBe("like");
        ResponseEngine.ConjugateVerb("like", "they").ShouldBe("like");
    }

    [Fact]
    public void ConjugateVerb_AddsS_ForThirdPersonSingular()
    {
        ResponseEngine.ConjugateVerb("like", "Alice").ShouldBe("likes");
        ResponseEngine.ConjugateVerb("run", "cat").ShouldBe("runs");
        ResponseEngine.ConjugateVerb("walk", "dog").ShouldBe("walks");
    }

    [Fact]
    public void ConjugateVerb_AddsEs_ForSpecialEndings()
    {
        ResponseEngine.ConjugateVerb("pass", "Alice").ShouldBe("passes");
        ResponseEngine.ConjugateVerb("push", "Bob").ShouldBe("pushes");
        ResponseEngine.ConjugateVerb("watch", "Charlie").ShouldBe("watches");
        ResponseEngine.ConjugateVerb("mix", "Daisy").ShouldBe("mixes");
        ResponseEngine.ConjugateVerb("buzz", "bee").ShouldBe("buzzes");
        ResponseEngine.ConjugateVerb("go", "David").ShouldBe("goes");
    }

    [Fact]
    public void ConjugateVerb_ConvertsYtoIes_AfterConsonant()
    {
        ResponseEngine.ConjugateVerb("fly", "bird").ShouldBe("flies");
        ResponseEngine.ConjugateVerb("cry", "baby").ShouldBe("cries");
    }

    [Fact]
    public void ConjugateVerb_KeepsY_AfterVowel()
    {
        ResponseEngine.ConjugateVerb("play", "Alice").ShouldBe("plays");
        ResponseEngine.ConjugateVerb("enjoy", "Bob").ShouldBe("enjoys");
    }

    [Fact]
    public void ConjugateVerb_HandlesIrregulars()
    {
        ResponseEngine.ConjugateVerb("have", "Alice").ShouldBe("has");
        ResponseEngine.ConjugateVerb("do", "Bob").ShouldBe("does");
        ResponseEngine.ConjugateVerb("say", "Charlie").ShouldBe("says");
        ResponseEngine.ConjugateVerb("is", "sky").ShouldBe("is");
        ResponseEngine.ConjugateVerb("are", "sky").ShouldBe("is");
        ResponseEngine.ConjugateVerb("was", "Alice").ShouldBe("was");
        ResponseEngine.ConjugateVerb("were", "Alice").ShouldBe("were");
    }

    [Fact]
    public void ConjugateVerb_IsApplied_InExistingFactResponse()
    {
        using var db = new FreshDbContext();
        var context = new ContextTracker();
        var userId = SeedUser(db.Context);

        db.Context.Facts.Add(new FactEntity
        {
            UserId = userId,
            Subject = "pizza",
            Verb = "is",
            Object = "good",
            PredicateType = "GeneralFact",
            CreatedAt = DateTime.UtcNow.ToString("O")
        });
        db.Context.SaveChanges();

        var engine = CreateEngine(db.Context, context);
        var response = engine.GenerateResponse("pizza is good", userId);
        response.ShouldContain("is");
    }
}

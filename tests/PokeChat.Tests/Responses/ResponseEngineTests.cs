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
        SeedBotResponses(db);
        var knowledgeStore = new KnowledgeStore(db);
        var spellChecker = new SpellChecker();
        spellChecker.Initialise(new HashSet<string>(StringComparer.OrdinalIgnoreCase), new Dictionary<string, string>());
        var posTagger = new PosTagger([]);
        var tokeniser = new Tokeniser();
        var svoExtractor = new SvoExtractor();
        return new ResponseEngine(knowledgeStore, context, spellChecker, posTagger, tokeniser, svoExtractor);
    }

    private static void SeedBotResponses(PokeChat.Data.PokeChatDbContext db)
    {
        var now = DateTime.UtcNow.ToString("O");
        db.BotResponses.AddRange(
            new BotResponse { Category = "default_response", ResponseText = "Interesting! Tell me more.", CreatedAt = now },
            new BotResponse { Category = "default_response", ResponseText = "I see.", CreatedAt = now },
            new BotResponse { Category = "existing_fact", ResponseText = "I already know that {0} {1} {2}.", CreatedAt = now },
            new BotResponse { Category = "context_followup", ResponseText = "Tell me more about {0}.", CreatedAt = now },
            new BotResponse { Category = "context_followup_with_object", ResponseText = "You said {0} is related to {1}.", CreatedAt = now },
            new BotResponse { Category = "random_fact_followup", ResponseText = "Speaking of {0}, you mentioned they {1} {2}.", CreatedAt = now },
            new BotResponse { Category = "dictionary_query_found", ResponseText = "A {0} is {1}.", CreatedAt = now },
            new BotResponse { Category = "dictionary_query_not_found", ResponseText = "I don't know what {0} means.", CreatedAt = now },
            new BotResponse { Category = "thesaurus_query_found", ResponseText = "Some words related to {0} are: {1}.", CreatedAt = now },
            new BotResponse { Category = "thesaurus_query_none", ResponseText = "I don't know of any related words.", CreatedAt = now },
            new BotResponse { Category = "link_saved", ResponseText = "I've noted that {0} is related to {1}.", CreatedAt = now },
            new BotResponse { Category = "proactive_preference", ResponseText = "You like {0}? Tell me more!", CreatedAt = now },
            new BotResponse { Category = "proactive_dislike", ResponseText = "Why don't you like {0}?", CreatedAt = now },
            new BotResponse { Category = "proactive_possession", ResponseText = "Tell me more about your {0}.", CreatedAt = now },
            new BotResponse { Category = "proactive_belief", ResponseText = "How did you learn about {0}?", CreatedAt = now },
            new BotResponse { Category = "proactive_personal", ResponseText = "You said you're {0}. What's that like?", CreatedAt = now },
            new BotResponse { Category = "proactive_general_fact", ResponseText = "You mentioned {0} {1} {2}.", CreatedAt = now },
            new BotResponse { Category = "proactive_general", ResponseText = "Tell me more about {0}.", CreatedAt = now },
            new BotResponse { Category = "proactive_statement", ResponseText = "I remember that {0} {1} {2}.", CreatedAt = now }
        );
        db.SaveChanges();
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

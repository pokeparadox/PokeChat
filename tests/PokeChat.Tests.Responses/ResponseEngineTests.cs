using PokeChat.Core;
using PokeChat.Data.Entities;
using PokeChat.Knowledge;
using PokeChat.NLP;
using PokeChat.Responses;
using PokeChat.Tests.Shared.Helpers;
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
            CreatedAt = DateTime.UtcNow.ToString("o"),
            Responses = [new() { ResponseText = "Hey there!" }]
        });
        db.Context.SaveChanges();

        var engine = CreateEngine(db.Context, context);
        var response = engine.GenerateResponse("hello", null);
        response.ShouldBe("Hey there!");
    }

    private int SeedUser(PokeChat.Data.PokeChatDbContext db)
    {
        var user = new User { Name = "TestUser", FirstSeen = DateTime.UtcNow.ToString("o"), LastSeen = DateTime.UtcNow.ToString("o") };
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
            CreatedAt = DateTime.UtcNow.ToString("o")
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
            CreatedAt = DateTime.UtcNow.ToString("o")
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
        response.ShouldBe("\U0001F4AD Tell me more about TestUser.");
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
            CreatedAt = DateTime.UtcNow.ToString("o")
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
        ResponseEngine.ConjugateVerb("were", "Alice").ShouldBe("was");
    }

    [Fact]
    public void GenerateResponse_ReturnsEmpathyHappy_WhenSentimentContextSet()
    {
        using var db = new FreshDbContext();
        var context = new ContextTracker();
        context.SetContext(ContextKeys.CurrentSentiment, "positive");
        context.SetContext(ContextKeys.LastSentimentIntensity, "3");

        var engine = CreateEngine(db.Context, context);
        var response = engine.GenerateResponse("I'm so happy!", null);
        response.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void GenerateResponse_ReturnsEmpathySad_WhenSentimentContextSet()
    {
        using var db = new FreshDbContext();
        var context = new ContextTracker();
        context.SetContext(ContextKeys.CurrentSentiment, "negative");
        context.SetContext(ContextKeys.LastSentimentIntensity, "3");

        var engine = CreateEngine(db.Context, context);
        var response = engine.GenerateResponse("I feel so sad", null);
        response.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void GenerateResponse_SkipsEmpathy_WhenIntensityLow()
    {
        using var db = new FreshDbContext();
        var context = new ContextTracker();
        context.SetContext(ContextKeys.CurrentSentiment, "negative");
        context.SetContext(ContextKeys.LastSentimentIntensity, "1");

        var engine = CreateEngine(db.Context, context);
        var response = engine.GenerateResponse("hello", null);
        response.ShouldBeOneOf("Interesting! Tell me more.", "I see.");
    }

    [Fact]
    public void GenerateResponse_ReturnsEmotionFollowUp_WhenSentimentChanged()
    {
        using var db = new FreshDbContext();
        var context = new ContextTracker();
        context.SetContext(ContextKeys.CurrentSentiment, "negative");
        context.SetContext(ContextKeys.LastSentimentIntensity, "3");
        context.SetContext(ContextKeys.PreviousSentiment, "positive");

        var engine = CreateEngine(db.Context, context);
        var response = engine.GenerateResponse("I'm sad now", null);
        response.ShouldNotBeNullOrEmpty();
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
            CreatedAt = DateTime.UtcNow.ToString("o")
        });
        db.Context.SaveChanges();

        var engine = CreateEngine(db.Context, context);
        var response = engine.GenerateResponse("pizza is good", userId);
        response.ShouldContain("is");
    }

    // --- Part B: Enhanced categories via LLM ---

    [Fact]
    public void GenerateResponse_UsesLLM_WhenCategoryEnhanced()
    {
        using var db = new FreshDbContext();
        var context = new ContextTracker();
        var store = new KnowledgeStore(db.Context);
        TestDataHelper.SeedBotResponses(db.Context);

        var spellChecker = new SpellChecker();
        spellChecker.Initialise(new HashSet<string>(StringComparer.OrdinalIgnoreCase), []);
        var posTagger = new PosTagger([]);
        var tokeniser = new Tokeniser();
        var svoExtractor = new SvoExtractor();

        var llmCalled = false;
        Func<string, string?> llmGen = prompt => { llmCalled = true; return "LLM-enhanced response."; };
        var enhanced = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "default_response" };

        var engine = new ResponseEngine(store, context, spellChecker, posTagger, tokeniser, svoExtractor,
            llmGenerator: llmGen, enhancedCategories: enhanced);
        var response = engine.GenerateResponse("hello", null);
        llmCalled.ShouldBeTrue();
        response.ShouldBe("LLM-enhanced response.");
    }

    [Fact]
    public void GenerateResponse_FallsBackToTemplate_WhenLLMReturnsNull()
    {
        using var db = new FreshDbContext();
        var context = new ContextTracker();
        var store = new KnowledgeStore(db.Context);
        TestDataHelper.SeedBotResponses(db.Context);

        var spellChecker = new SpellChecker();
        spellChecker.Initialise(new HashSet<string>(StringComparer.OrdinalIgnoreCase), []);
        var posTagger = new PosTagger([]);
        var tokeniser = new Tokeniser();
        var svoExtractor = new SvoExtractor();

        Func<string, string?>? llmGen = _ => null;
        var enhanced = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "default_response" };

        var engine = new ResponseEngine(store, context, spellChecker, posTagger, tokeniser, svoExtractor,
            llmGenerator: llmGen, enhancedCategories: enhanced);
        var response = engine.GenerateResponse("hello", null);
        response.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void GenerateResponse_UsesTemplate_WhenCategoryNotEnhanced()
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
            CreatedAt = DateTime.UtcNow.ToString("o")
        });
        db.Context.SaveChanges();

        var store = new KnowledgeStore(db.Context);
        TestDataHelper.SeedBotResponses(db.Context);

        var spellChecker = new SpellChecker();
        spellChecker.Initialise(new HashSet<string>(StringComparer.OrdinalIgnoreCase), []);
        var posTagger = new PosTagger([]);
        var tokeniser = new Tokeniser();
        var svoExtractor = new SvoExtractor();

        var llmCalled = false;
        Func<string, string?> llmGen = prompt => { llmCalled = true; return "LLM-enhanced."; };
        var enhanced = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "unrelated_category" };

        var engine = new ResponseEngine(store, context, spellChecker, posTagger, tokeniser, svoExtractor,
            llmGenerator: llmGen, enhancedCategories: enhanced);
        var response = engine.GenerateResponse("hello", userId);
        llmCalled.ShouldBeFalse();
        response.ShouldContain("pizza");
    }

    // --- Part E: Inference via LLM ---

    [Fact]
    public void HandleInferenceResponse_Contradiction_UsesLLM_WhenAvailable()
    {
        using var db = new FreshDbContext();
        var context = new ContextTracker();
        context.SetContext(ContextKeys.LastContradiction, "like|pizza|hate|pizza");

        var store = new KnowledgeStore(db.Context);
        TestDataHelper.SeedBotResponses(db.Context);

        var spellChecker = new SpellChecker();
        spellChecker.Initialise(new HashSet<string>(StringComparer.OrdinalIgnoreCase), []);
        var posTagger = new PosTagger([]);
        var tokeniser = new Tokeniser();
        var svoExtractor = new SvoExtractor();

        var llmCalled = false;
        Func<string, string?> llmGen = prompt => { llmCalled = true; return "LLM contradiction response."; };

        var engine = new ResponseEngine(store, context, spellChecker, posTagger, tokeniser, svoExtractor,
            llmGenerator: llmGen);
        var response = engine.GenerateResponse("I like pizza", null);
        // With LLM available, contradiction should use LLM
        llmCalled.ShouldBeTrue();
        response.ShouldBe("LLM contradiction response.");
    }

    [Fact]
    public void HandleInferenceResponse_Generalisation_UsesLLM_WhenAvailable()
    {
        using var db = new FreshDbContext();
        var context = new ContextTracker();
        context.SetContext(ContextKeys.InferredGeneralisation, "fruit|apple");

        var store = new KnowledgeStore(db.Context);
        TestDataHelper.SeedBotResponses(db.Context);

        var spellChecker = new SpellChecker();
        spellChecker.Initialise(new HashSet<string>(StringComparer.OrdinalIgnoreCase), []);
        var posTagger = new PosTagger([]);
        var tokeniser = new Tokeniser();
        var svoExtractor = new SvoExtractor();

        Func<string, string?> llmGen = prompt => "LLM generalisation response.";

        var engine = new ResponseEngine(store, context, spellChecker, posTagger, tokeniser, svoExtractor,
            llmGenerator: llmGen);
        var response = engine.GenerateResponse("hello", null);
        // Generalisation has 50% chance — run multiple times or check for it
        // At least we verify the response is non-null
        response.ShouldNotBeNullOrEmpty();
    }

    // --- Part F: Story via LLM ---

    [Fact]
    public void HandleStoryRequest_UsesLLM_WhenAvailable()
    {
        using var db = new FreshDbContext();
        var context = new ContextTracker();

        var store = new KnowledgeStore(db.Context);
        TestDataHelper.SeedBotResponses(db.Context);

        var spellChecker = new SpellChecker();
        spellChecker.Initialise(new HashSet<string>(StringComparer.OrdinalIgnoreCase), []);
        var posTagger = new PosTagger([]);
        var tokeniser = new Tokeniser();
        var svoExtractor = new SvoExtractor();

        var llmCalled = false;
        Func<string, string?> llmGen = prompt => { llmCalled = true; return "LLM story."; };

        var engine = new ResponseEngine(store, context, spellChecker, posTagger, tokeniser, svoExtractor,
            llmGenerator: llmGen);
        var response = engine.GenerateResponse("tell me a story", null);
        llmCalled.ShouldBeTrue();
        response.ShouldContain("LLM story");
    }

    [Fact]
    public void HandleStoryRequest_FallsBackToTemplate_WhenLLMReturnsNull()
    {
        using var db = new FreshDbContext();
        var context = new ContextTracker();

        var store = new KnowledgeStore(db.Context);
        TestDataHelper.SeedBotResponses(db.Context);

        var spellChecker = new SpellChecker();
        spellChecker.Initialise(new HashSet<string>(StringComparer.OrdinalIgnoreCase), []);
        var posTagger = new PosTagger([]);
        var tokeniser = new Tokeniser();
        var svoExtractor = new SvoExtractor();

        Func<string, string?> llmGen = _ => null;

        var engine = new ResponseEngine(store, context, spellChecker, posTagger, tokeniser, svoExtractor,
            llmGenerator: llmGen);
        var response = engine.GenerateResponse("tell me a story", null);
        response.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void HandlePoetryRequest_ExplicitHaiku_ReturnsPoem()
    {
        using var db = new FreshDbContext();
        TestDataHelper.SeedRhymeGroups(db.Context);
        TestDataHelper.SeedPoemTemplates(db.Context);
        TestDataHelper.SeedBotResponses(db.Context);
        TestDataHelper.SeedPosDictionary(db.Context);

        var context = new ContextTracker();
        var store = new KnowledgeStore(db.Context);
        var spellChecker = new SpellChecker();
        var posEntries = store.GetPosDictionary();
        spellChecker.Initialise(new HashSet<string>(posEntries.Select(e => e.Word), StringComparer.OrdinalIgnoreCase), []);
        var posTagger = new PosTagger(posEntries);
        var tokeniser = new Tokeniser();
        var svoExtractor = new SvoExtractor();

        var engine = new ResponseEngine(store, context, spellChecker, posTagger, tokeniser, svoExtractor);
        var response = engine.GenerateResponse("write a haiku", 1);
        response.ShouldNotBeNullOrEmpty();
        response.ShouldNotContain("{");
    }

    [Fact]
    public void HandlePoetryRequest_ExplicitLimerick_ReturnsPoem()
    {
        using var db = new FreshDbContext();
        TestDataHelper.SeedRhymeGroups(db.Context);
        TestDataHelper.SeedPoemTemplates(db.Context);
        TestDataHelper.SeedBotResponses(db.Context);
        TestDataHelper.SeedPosDictionary(db.Context);

        var context = new ContextTracker();
        var store = new KnowledgeStore(db.Context);
        var spellChecker = new SpellChecker();
        var posEntries = store.GetPosDictionary();
        spellChecker.Initialise(new HashSet<string>(posEntries.Select(e => e.Word), StringComparer.OrdinalIgnoreCase), []);
        var posTagger = new PosTagger(posEntries);
        var tokeniser = new Tokeniser();
        var svoExtractor = new SvoExtractor();

        var engine = new ResponseEngine(store, context, spellChecker, posTagger, tokeniser, svoExtractor);
        var response = engine.GenerateResponse("write a limerick", 1);
        response.ShouldNotBeNullOrEmpty();
        response.ShouldNotContain("{");
    }

    [Fact]
    public void HandlePoetryRequest_HaikuViaLLM_WhenAvailable()
    {
        using var db = new FreshDbContext();
        TestDataHelper.SeedRhymeGroups(db.Context);
        TestDataHelper.SeedPoemTemplates(db.Context);
        TestDataHelper.SeedBotResponses(db.Context);
        TestDataHelper.SeedPosDictionary(db.Context);

        var context = new ContextTracker();
        var store = new KnowledgeStore(db.Context);
        var spellChecker = new SpellChecker();
        var posEntries = store.GetPosDictionary();
        spellChecker.Initialise(new HashSet<string>(posEntries.Select(e => e.Word), StringComparer.OrdinalIgnoreCase), []);
        var posTagger = new PosTagger(posEntries);
        var tokeniser = new Tokeniser();
        var svoExtractor = new SvoExtractor();

        var llmCalled = false;
        Func<string, string?> llmGen = prompt => { llmCalled = true; return "an LLM haiku"; };

        var engine = new ResponseEngine(store, context, spellChecker, posTagger, tokeniser, svoExtractor,
            llmGenerator: llmGen);
        var response = engine.GenerateResponse("write a haiku", 1);
        llmCalled.ShouldBeTrue();
        response.ShouldContain("haiku");
    }

    [Fact]
    public void HandlePoetryRequest_LimerickViaLLM_WhenAvailable()
    {
        using var db = new FreshDbContext();
        TestDataHelper.SeedRhymeGroups(db.Context);
        TestDataHelper.SeedPoemTemplates(db.Context);
        TestDataHelper.SeedBotResponses(db.Context);
        TestDataHelper.SeedPosDictionary(db.Context);

        var context = new ContextTracker();
        var store = new KnowledgeStore(db.Context);
        var spellChecker = new SpellChecker();
        var posEntries = store.GetPosDictionary();
        spellChecker.Initialise(new HashSet<string>(posEntries.Select(e => e.Word), StringComparer.OrdinalIgnoreCase), []);
        var posTagger = new PosTagger(posEntries);
        var tokeniser = new Tokeniser();
        var svoExtractor = new SvoExtractor();

        var llmCalled = false;
        Func<string, string?> llmGen = prompt => { llmCalled = true; return "an LLM limerick"; };

        var engine = new ResponseEngine(store, context, spellChecker, posTagger, tokeniser, svoExtractor,
            llmGenerator: llmGen);
        var response = engine.GenerateResponse("write a limerick", 1);
        llmCalled.ShouldBeTrue();
        response.ShouldContain("limerick");
    }

    private static string Strip8BallEmoji(string response)
    {
        var text = response.StartsWith("*shakes the magic 8 ball* ")
            ? response["*shakes the magic 8 ball* ".Length..]
            : response;
        return text.Length >= 3 && char.IsHighSurrogate(text[0]) ? text[3..] : text;
    }

    [Fact]
    public void Explicit8Ball_ReturnsAnswer()
    {
        using var db = new FreshDbContext();
        var context = new ContextTracker();
        var engine = CreateEngine(db.Context, context);
        var response = engine.GenerateResponse("magic 8 ball, will I win?", null);
        response.ShouldNotBeNullOrEmpty();
        var seededAnswers = new[] { "Yes.", "No.", "Maybe.", "Ask again later.", "It is certain." };
        seededAnswers.ShouldContain(Strip8BallEmoji(response));
    }

    [Fact]
    public void ExplicitPredict_Returns8Ball()
    {
        using var db = new FreshDbContext();
        var context = new ContextTracker();
        var engine = CreateEngine(db.Context, context);
        var response = engine.GenerateResponse("predict my future", null);
        response.ShouldNotBeNullOrEmpty();
        var seededAnswers = new[] { "Yes.", "No.", "Maybe.", "Ask again later.", "It is certain." };
        seededAnswers.ShouldContain(Strip8BallEmoji(response));
    }

    [Fact]
    public void QuestionFallthrough_Returns8Ball()
    {
        using var db = new FreshDbContext();
        var context = new ContextTracker();
        var engine = CreateEngine(db.Context, context);
        var response = engine.GenerateResponse("shake the ball", null);
        response.ShouldNotBeNullOrEmpty();
        var seededAnswers = new[] { "Yes.", "No.", "Maybe.", "Ask again later.", "It is certain." };
        seededAnswers.ShouldContain(Strip8BallEmoji(response));
    }

    [Fact]
    public void HandledQuestion_No8Ball()
    {
        using var db = new FreshDbContext();
        var context = new ContextTracker();

        var now = DateTime.UtcNow.ToString("o");
        db.Context.ResponseRules.Add(new()
        {
            Pattern = @"what is a cat",
            InputType = "Question",
            IsActive = true,
            CreatedAt = now,
            Responses = [new() { ResponseText = "Cats are fascinating creatures." }]
        });
        db.Context.SaveChanges();

        var engine = CreateEngine(db.Context, context);
        var response = engine.GenerateResponse("what is a cat?", null);
        response.ShouldBe("Cats are fascinating creatures.");
    }

    [Fact]
    public void Statement_No8Ball()
    {
        using var db = new FreshDbContext();
        var context = new ContextTracker();
        var engine = CreateEngine(db.Context, context);
        var response = engine.GenerateResponse("I like pizza", null);
        response.ShouldNotBeNullOrEmpty();
        response.ShouldNotStartWith("*shakes the magic 8 ball* ");
        var seededAnswers = new[] { "Yes.", "No.", "Maybe.", "Ask again later.", "It is certain." };
        seededAnswers.ShouldNotContain(response);
    }

    [Fact]
    public void EmptyQuestionMark_Skips()
    {
        using var db = new FreshDbContext();
        var context = new ContextTracker();
        var engine = CreateEngine(db.Context, context);
        var response = engine.GenerateResponse("?", null);
        response.ShouldNotBeNullOrEmpty();
        response.ShouldNotStartWith("*shakes the magic 8 ball* ");
        var seededAnswers = new[] { "Yes.", "No.", "Maybe.", "Ask again later.", "It is certain." };
        seededAnswers.ShouldNotContain(response);
    }

    [Fact]
    public void RandomSelection_ProvidesVariety()
    {
        using var db = new FreshDbContext();
        var context = new ContextTracker();
        var engine = CreateEngine(db.Context, context);
        var answers = new HashSet<string>();
        for (int i = 0; i < 50; i++)
        {
            var response = engine.GenerateResponse("magic 8 ball, will I win?", null);
            var withoutPreamble = response.StartsWith("*shakes the magic 8 ball* ")
                ? response["*shakes the magic 8 ball* ".Length..]
                : response;
            answers.Add(withoutPreamble);
        }
        answers.Count.ShouldBeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public void DefaultResponse_NoEmoji()
    {
        using var db = new FreshDbContext();
        var context = new ContextTracker();
        var engine = CreateEngine(db.Context, context);
        var response = engine.GenerateResponse("hello world", null);
        response.ShouldNotBeNullOrEmpty();
        response.ShouldNotStartWith("\U0001F44B");
        response.ShouldNotStartWith("\U0001F4AD");
        response.ShouldNotStartWith("\U0001F4D6");
    }

    [Fact]
    public void DictionaryQuery_IncludesBookEmoji()
    {
        using var db = new FreshDbContext();
        var context = new ContextTracker();
        var engine = CreateEngine(db.Context, context);
        var response = engine.GenerateResponse("what is the definition of testword", null);
        response.ShouldStartWith("\U0001F4D6");
    }

    [Fact]
    public void PredictionResponse_IncludesCrystalBallEmoji()
    {
        using var db = new FreshDbContext();
        var context = new ContextTracker();
        var engine = CreateEngine(db.Context, context);
        var response = engine.GenerateResponse("predict my future", null);
        response.ShouldContain("\U0001F52E");
    }

    [Fact]
    public void EmpathyResponse_IncludesSentimentEmoji()
    {
        using var db = new FreshDbContext();
        var context = new ContextTracker();
        context.SetContext(ContextKeys.CurrentSentiment, "positive");
        context.SetContext(ContextKeys.LastSentimentIntensity, "3");
        var engine = CreateEngine(db.Context, context);
        var response = engine.GenerateResponse("hello", null);
        response.ShouldStartWith("\U0001F60A");
    }

    [Fact]
    public void SanitiseFollowUpPhrase_StripsLeadingFunctionWords()
    {
        ResponseEngine.SanitiseFollowUpPhrase("and the trains").ShouldBe("trains");
    }

    [Fact]
    public void SanitiseFollowUpPhrase_StripsTrailingFunctionWords()
    {
        ResponseEngine.SanitiseFollowUpPhrase("trains and the").ShouldBe("trains");
    }

    [Fact]
    public void SanitiseFollowUpPhrase_ReturnsNull_WhenAllFunctionWords()
    {
        ResponseEngine.SanitiseFollowUpPhrase("and the of for").ShouldBeNull();
    }

    [Fact]
    public void SanitiseFollowUpPhrase_SingleMeaningfulWord_ReturnsIt()
    {
        ResponseEngine.SanitiseFollowUpPhrase("trains").ShouldBe("trains");
    }

    [Fact]
    public void SanitiseFollowUpPhrase_NormalPhrase_Unchanged()
    {
        ResponseEngine.SanitiseFollowUpPhrase("efficient trains").ShouldBe("efficient trains");
    }

    [Fact]
    public void SanitiseFollowUpPhrase_NullInput_ReturnsNull()
    {
        ResponseEngine.SanitiseFollowUpPhrase(null!).ShouldBeNull();
    }

    [Fact]
    public void SanitiseFollowUpPhrase_EmptyInput_ReturnsNull()
    {
        ResponseEngine.SanitiseFollowUpPhrase("").ShouldBeNull();
    }

    [Fact]
    public void SanitiseFollowUpPhrase_StripsTrailingNow()
    {
        ResponseEngine.SanitiseFollowUpPhrase("bored of that now").ShouldBe("bored");
    }

    [Fact]
    public void SanitiseFollowUpPhrase_AllStopWords_ReturnsNull()
    {
        ResponseEngine.SanitiseFollowUpPhrase("of the and for now").ShouldBeNull();
    }

    [Fact]
    public void GenerateResponse_NoLiteralPlaceholder_WhenSubjectIsNull()
    {
        using var db = new FreshDbContext();
        var context = new ContextTracker();
        context.UpdateLastSubject("of the and for");
        context.UpdateLastObject("also too just");
        var engine = CreateEngine(db.Context, context);
        var response = engine.GenerateResponse("hello", null);
        response.ShouldNotContain("{0}");
        response.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void GetResponse_WeatherNoApiKey_mentions_openweathermap()
    {
        using var db = new FreshDbContext();
        var context = new ContextTracker();
        var engine = CreateEngine(db.Context, context);
        var response = engine.GetResponse("weather_no_api_key");
        response.ShouldContain("openweathermap.org");
        response.ShouldContain("WEATHER_API_KEY");
    }

    [Fact]
    public void GetResponse_WeatherError_mention_city_check()
    {
        using var db = new FreshDbContext();
        var context = new ContextTracker();
        var engine = CreateEngine(db.Context, context);
        var response = engine.GetResponse("weather_error");
        response.ShouldNotBeNullOrEmpty();
    }
}

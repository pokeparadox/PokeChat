using PokeChat.Core;
using PokeChat.Data.Entities;
using PokeChat.Knowledge;
using PokeChat.LLM;
using PokeChat.NLP;
using PokeChat.Responses;
using PokeChat.Tests.Shared.Helpers;
using PokeChat.Tests.Shared.LLM;
using Shouldly;

namespace PokeChat.Tests.Core;

public class ChatSessionHomeworkCheckTests
{
    private static readonly string TestSessionId = "test-homework-session";

    private ChatSession CreateSession(
        string? llmResponse = null,
        bool noLLM = false,
        FreshDbContext? dbOut = null)
    {
        var db = dbOut ?? new FreshDbContext();
        TestDataHelper.SeedBotResponses(db.Context);
        TestDataHelper.SeedPosDictionary(db.Context);
        var store = new KnowledgeStore(db.Context);
        var contextTracker = new ContextTracker();
        var spellChecker = new SpellChecker();

        var posEntries = store.GetPosDictionary();
        var posTagger = new PosTagger(posEntries);

        var spellDict = new HashSet<string>(posEntries.Select(e => e.Word), StringComparer.OrdinalIgnoreCase);
        var misspellings = store.GetMisspellings();
        spellChecker.Initialise(spellDict, misspellings);

        var tokeniser = new Tokeniser();
        var sentenceSplitter = new SentenceSplitter();
        var svoExtractor = new SvoExtractor();

        var nounCategoriser = new NounCategoriser(store);
        var responseEngine = new ResponseEngine(store, contextTracker, spellChecker, posTagger, tokeniser, svoExtractor);

        if (noLLM)
        {
            return new ChatSession(
                db.Context, store, responseEngine, spellChecker, posTagger,
                tokeniser, sentenceSplitter, svoExtractor, contextTracker, nounCategoriser,
                new List<string> { "my name is", "i am", "i'm", "call me" },
                new List<string> { "quit", "exit" }.ToHashSet(StringComparer.OrdinalIgnoreCase),
                new List<string> { "hi", "hello" }.ToHashSet(StringComparer.OrdinalIgnoreCase),
                sessionId: TestSessionId);
        }

        var llmConfig = new LLMConfig { Enabled = true };
        var llmProvider = new StubLLMProvider { Response = llmResponse ?? "{}" };
        var llmOrchestrator = new LLMOrchestrator(llmProvider, llmConfig);

        return new ChatSession(
            db.Context, store, responseEngine, spellChecker, posTagger,
            tokeniser, sentenceSplitter, svoExtractor, contextTracker, nounCategoriser,
            new List<string> { "my name is", "i am", "i'm", "call me" },
            new List<string> { "quit", "exit" }.ToHashSet(StringComparer.OrdinalIgnoreCase),
            new List<string> { "hi", "hello" }.ToHashSet(StringComparer.OrdinalIgnoreCase),
            sessionId: TestSessionId,
            llmOrchestrator: llmOrchestrator);
    }

    private static void AddConversation(FreshDbContext db, string userInput, string botResponse)
    {
        db.Context.Conversations.Add(new Conversation
        {
            UserId = 1,
            UserInput = userInput,
            BotResponse = botResponse,
            Timestamp = DateTime.UtcNow.ToString("o"),
            SessionId = TestSessionId
        });
        db.Context.SaveChanges();
    }

    [Fact]
    public void NoLLM_Skips()
    {
        using var db = new FreshDbContext();
        var session = CreateSession(noLLM: true, dbOut: db);
        session.HandleNameInput("my name is Alice");

        session.RunHomeworkCheck();
    }

    [Fact]
    public void UserDeclined_Skips()
    {
        using var db = new FreshDbContext();
        var session = CreateSession(dbOut: db);
        session.HandleNameInput("my name is Alice");

        session.SetLLMOfferState("test");
        session.ProcessInput("no");

        session.RunHomeworkCheck();
    }

    [Fact]
    public void NoCurrentUser_Skips()
    {
        using var db = new FreshDbContext();
        var session = CreateSession(dbOut: db);

        session.RunHomeworkCheck();
    }

    [Fact]
    public void RemovesBadRule()
    {
        using var db = new FreshDbContext();
        var session = CreateSession(llmResponse: """
            {
              "rules_to_remove": [{"rule_id": 1, "reason": "Incorrect pattern"}],
              "definitions_to_add": [],
              "classifications_to_add": []
            }
            """, dbOut: db);
        session.HandleNameInput("my name is Alice");

        db.Context.LearnedResponseRules.Add(new LearnedResponseRule
        {
            Pattern = @"\btest\b",
            ResponseTemplate = "wrong response",
            InputType = "Statement",
            Confidence = 5,
            IsActive = true,
            CreatedAt = DateTime.UtcNow.ToString("o")
        });
        db.Context.SaveChanges();

        AddConversation(db, "test input", "test response");

        // Verify conversation exists before homework check
        db.Context.Conversations.Count(c => c.SessionId == TestSessionId).ShouldBe(1);

        session.RunHomeworkCheck();

        var rule = db.Context.LearnedResponseRules.Find(1);
        rule.ShouldNotBeNull();
        rule.IsActive.ShouldBeFalse();
    }

    [Fact]
    public void AddsDefinitions()
    {
        using var db = new FreshDbContext();
        var session = CreateSession(llmResponse: """
            {
              "rules_to_remove": [],
              "definitions_to_add": [{"word": "grok", "definition": "to understand deeply"}],
              "classifications_to_add": []
            }
            """, dbOut: db);
        session.HandleNameInput("my name is Alice");

        AddConversation(db, "what is grok", "I don't know");

        session.RunHomeworkCheck();

        var def = db.Context.WordDefinitions.FirstOrDefault(d => d.Word == "grok");
        def.ShouldNotBeNull();
        def.Definition.ShouldBe("to understand deeply");
    }

    [Fact]
    public void AddsClassifications()
    {
        using var db = new FreshDbContext();
        var session = CreateSession(llmResponse: """
            {
              "rules_to_remove": [],
              "definitions_to_add": [],
              "classifications_to_add": [{"word": "grok", "category": "verb"}]
            }
            """, dbOut: db);
        session.HandleNameInput("my name is Alice");

        // Add the word to POS dict first (as would happen during normal teaching)
        db.Context.PosDictionary.Add(new PosDictionaryEntry
        {
            Word = "grok",
            WordType = "unknown",
            CreatedAt = DateTime.UtcNow.ToString("o")
        });
        db.Context.SaveChanges();

        AddConversation(db, "grok", "what is that");

        session.RunHomeworkCheck();

        var cat = db.Context.NounCategories.FirstOrDefault(n => n.Noun == "grok");
        cat.ShouldNotBeNull();
        cat.Category.ShouldBe("verb");

        var pos = db.Context.PosDictionary.FirstOrDefault(p => p.Word == "grok");
        pos.ShouldNotBeNull();
        pos.WordType.ShouldBe("verb");
    }

    [Fact]
    public void InvalidJson_NoCrash()
    {
        using var db = new FreshDbContext();
        var session = CreateSession(llmResponse: "this is not valid json", dbOut: db);
        session.HandleNameInput("my name is Alice");

        AddConversation(db, "test", "response");

        session.RunHomeworkCheck();
    }

    [Fact]
    public void MixedActions()
    {
        using var db = new FreshDbContext();
        var session = CreateSession(llmResponse: """
            {
              "rules_to_remove": [{"rule_id": 1, "reason": "Wrong"}],
              "definitions_to_add": [{"word": "foo", "definition": "a thing"}],
              "classifications_to_add": [{"word": "bar", "category": "place"}]
            }
            """, dbOut: db);
        session.HandleNameInput("my name is Alice");

        db.Context.LearnedResponseRules.Add(new LearnedResponseRule
        {
            Pattern = @"\btest\b",
            ResponseTemplate = "bad",
            InputType = "Statement",
            Confidence = 5,
            IsActive = true,
            CreatedAt = DateTime.UtcNow.ToString("o")
        });
        db.Context.SaveChanges();

        AddConversation(db, "test", "response");

        session.RunHomeworkCheck();

        var rule = db.Context.LearnedResponseRules.Find(1);
        rule!.IsActive.ShouldBeFalse();

        var def = db.Context.WordDefinitions.FirstOrDefault(d => d.Word == "foo");
        def.ShouldNotBeNull();
        def.Definition.ShouldBe("a thing");

        var cat = db.Context.NounCategories.FirstOrDefault(n => n.Noun == "bar");
        cat.ShouldNotBeNull();
        cat.Category.ShouldBe("place");
    }

    [Fact]
    public void NoIssuesFound()
    {
        using var db = new FreshDbContext();
        var session = CreateSession(llmResponse: "{}", dbOut: db);
        session.HandleNameInput("my name is Alice");

        AddConversation(db, "hi", "hello");

        session.RunHomeworkCheck();

        db.Context.LearnedResponseRules.Count().ShouldBe(0);
        db.Context.WordDefinitions.Count().ShouldBe(0);
    }

    [Fact]
    public void EmptyLearnedRules()
    {
        using var db = new FreshDbContext();
        var session = CreateSession(llmResponse: """
            {
              "rules_to_remove": [],
              "definitions_to_add": [{"word": "baz", "definition": "something"}],
              "classifications_to_add": []
            }
            """, dbOut: db);
        session.HandleNameInput("my name is Alice");

        AddConversation(db, "baz", "what");

        session.RunHomeworkCheck();

        var def = db.Context.WordDefinitions.FirstOrDefault(d => d.Word == "baz");
        def.ShouldNotBeNull();
        def.Definition.ShouldBe("something");
    }

    [Fact]
    public void ValidatesRuleExists()
    {
        using var db = new FreshDbContext();
        var session = CreateSession(llmResponse: """
            {
              "rules_to_remove": [{"rule_id": 999, "reason": "Doesn't exist"}],
              "definitions_to_add": [],
              "classifications_to_add": []
            }
            """, dbOut: db);
        session.HandleNameInput("my name is Alice");

        AddConversation(db, "test", "response");

        session.RunHomeworkCheck();
    }

    [Fact]
    public void ValidatesCategory()
    {
        using var db = new FreshDbContext();
        var session = CreateSession(llmResponse: """
            {
              "rules_to_remove": [],
              "definitions_to_add": [],
              "classifications_to_add": [{"word": "qux", "category": "invalid_category"}]
            }
            """, dbOut: db);
        session.HandleNameInput("my name is Alice");

        AddConversation(db, "qux", "what");

        session.RunHomeworkCheck();

        db.Context.NounCategories.Count(n => n.Noun == "qux").ShouldBe(0);
    }
}

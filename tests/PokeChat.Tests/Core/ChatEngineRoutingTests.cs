using PokeChat.Core;
using PokeChat.Data;
using PokeChat.Data.Entities;
using PokeChat.Knowledge;
using PokeChat.NLP;
using PokeChat.Responses;
using PokeChat.Tests.Helpers;
using Shouldly;

namespace PokeChat.Tests.Core;

public class ChatEngineRoutingTests
{
    private ChatEngine CreateEngine(FreshDbContext db)
    {
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

        return new ChatEngine(
            db.Context, store, responseEngine, spellChecker, posTagger, tokeniser,
            sentenceSplitter, svoExtractor, contextTracker, nounCategoriser,
            new List<string> { "my name is", "i am", "i'm", "call me" },
            new HashSet<string> { "quit", "exit" },
            new HashSet<string> { "hi", "hello" });
    }

    [Fact]
    public void LastResponseCategory_is_set_after_processing_input()
    {
        using var db = new FreshDbContext();
        var engine = CreateEngine(db);
        engine.ProcessInput("My name is TestUser");
        engine.ProcessInput("What colour is the sky?");

        engine.LastResponseCategory.ShouldNotBeNull();
        engine.LastResponseIsDeadEnd.ShouldBe(false);
    }

    [Fact]
    public void State_handled_categories_are_not_dead_ends()
    {
        using var db = new FreshDbContext();
        var engine = CreateEngine(db);

        engine.ProcessInput("My name is TestUser");
        var result = engine.ProcessInput("hello");

        engine.LastResponseCategory.ShouldNotBeNull();
        engine.LastResponseIsDeadEnd.ShouldBe(false);
        result.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void Unknown_entity_query_returns_non_dead_end_category()
    {
        using var db = new FreshDbContext();
        var engine = CreateEngine(db);

        engine.ProcessInput("My name is TestUser");
        var result = engine.ProcessInput("What is a quasar?");

        engine.LastResponseCategory.ShouldNotBeNull();
        result.ShouldNotBeNullOrEmpty();
    }
}

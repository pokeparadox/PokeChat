using PokeChat.Core;
using PokeChat.Data;
using PokeChat.Knowledge;
using PokeChat.NLP;
using PokeChat.Responses;
using PokeChat.Tests.Shared.Helpers;
using Shouldly;

namespace PokeChat.Tests.Core;

public class TurnRatingTests
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
    public void Rate_PlusOne_StoresRating()
    {
        using var db = new FreshDbContext();
        var engine = CreateEngine(db);
        engine.ProcessInput("My name is TestUser");
        engine.ProcessInput("I like pizza");
        var result = engine.ProcessInput("~rate +1");

        result.ShouldBe("Glad you liked it!");
        db.Context.TurnRates.ShouldContain(t => t.Rating == 1);
    }

    [Fact]
    public void Rate_MinusOne_StoresRating()
    {
        using var db = new FreshDbContext();
        var engine = CreateEngine(db);
        engine.ProcessInput("My name is TestUser");
        engine.ProcessInput("I like pizza");
        var result = engine.ProcessInput("~rate -1");

        result.ShouldBe("Noted — I'll try to do better.");
        db.Context.TurnRates.ShouldContain(t => t.Rating == -1);
    }

    [Fact]
    public void Rate_ShorthandUp_StoresPlusOne()
    {
        using var db = new FreshDbContext();
        var engine = CreateEngine(db);
        engine.ProcessInput("My name is TestUser");
        engine.ProcessInput("I like pizza");
        var result = engine.ProcessInput("~rate up");

        result.ShouldBe("Glad you liked it!");
        db.Context.TurnRates.ShouldContain(t => t.Rating == 1);
    }

    [Fact]
    public void Rate_ShorthandDown_StoresMinusOne()
    {
        using var db = new FreshDbContext();
        var engine = CreateEngine(db);
        engine.ProcessInput("My name is TestUser");
        engine.ProcessInput("I like pizza");
        var result = engine.ProcessInput("~rate down");

        result.ShouldBe("Noted — I'll try to do better.");
        db.Context.TurnRates.ShouldContain(t => t.Rating == -1);
    }

    [Fact]
    public void Rate_NumericOne_StoresPlusOne()
    {
        using var db = new FreshDbContext();
        var engine = CreateEngine(db);
        engine.ProcessInput("My name is TestUser");
        engine.ProcessInput("I like pizza");
        var result = engine.ProcessInput("~rate 1");

        result.ShouldBe("Glad you liked it!");
        db.Context.TurnRates.ShouldContain(t => t.Rating == 1);
    }

    [Fact]
    public void Rate_NegativeNumericOne_StoresMinusOne()
    {
        using var db = new FreshDbContext();
        var engine = CreateEngine(db);
        engine.ProcessInput("My name is TestUser");
        engine.ProcessInput("I like pizza");
        var result = engine.ProcessInput("~rate -1");

        result.ShouldBe("Noted — I'll try to do better.");
        db.Context.TurnRates.ShouldContain(t => t.Rating == -1);
    }

    [Fact]
    public void Rate_NoArgument_ReturnsUsageHint()
    {
        using var db = new FreshDbContext();
        var engine = CreateEngine(db);
        engine.ProcessInput("My name is TestUser");
        engine.ProcessInput("I like pizza");
        var result = engine.ProcessInput("~rate");

        result.ShouldBe("Usage: ~rate +1 or ~rate -1");
    }

    [Fact]
    public void Rate_BeforeAnyResponse_ReturnsNothingToRate()
    {
        using var db = new FreshDbContext();
        var engine = CreateEngine(db);
        engine.ProcessInput("My name is TestUser");
        var result = engine.ProcessInput("~rate +1");

        result.ShouldBe("Nothing to rate yet — try talking to me first!");
        db.Context.TurnRates.ShouldBeEmpty();
    }

    [Fact]
    public void Rate_BeforeNameSet_ReturnsNameRequired()
    {
        using var db = new FreshDbContext();
        var engine = CreateEngine(db);
        var result = engine.ProcessInput("~rate +1");

        result.ShouldBe("Tell me your name first so I know who's rating me!");
        db.Context.TurnRates.ShouldBeEmpty();
    }

    [Fact]
    public void Rate_SameResponseTwice_ReturnsAlreadyRated()
    {
        using var db = new FreshDbContext();
        var engine = CreateEngine(db);
        engine.ProcessInput("My name is TestUser");
        engine.ProcessInput("I like pizza");
        engine.ProcessInput("~rate +1");
        var result = engine.ProcessInput("~rate +1");

        result.ShouldBe("You already rated that one!");
        db.Context.TurnRates.Count(t => t.Rating == 1).ShouldBe(1);
    }

    [Fact]
    public void Rate_InvalidInput_ReturnsUsageHint()
    {
        using var db = new FreshDbContext();
        var engine = CreateEngine(db);
        engine.ProcessInput("My name is TestUser");
        engine.ProcessInput("I like pizza");
        var result = engine.ProcessInput("~rate 5");

        result.ShouldBe("Usage: ~rate +1 or ~rate -1");
    }

    [Fact]
    public void Rate_TextInput_ReturnsUsageHint()
    {
        using var db = new FreshDbContext();
        var engine = CreateEngine(db);
        engine.ProcessInput("My name is TestUser");
        engine.ProcessInput("I like pizza");
        var result = engine.ProcessInput("~rate abc");

        result.ShouldBe("Usage: ~rate +1 or ~rate -1");
    }

    [Fact]
    public void Rate_DifferentResponses_CanRateEach()
    {
        using var db = new FreshDbContext();
        var engine = CreateEngine(db);
        engine.ProcessInput("My name is TestUser");
        engine.ProcessInput("I like pizza");
        engine.ProcessInput("~rate +1");
        engine.ProcessInput("I like cats");
        var result = engine.ProcessInput("~rate -1");

        result.ShouldBe("Noted — I'll try to do better.");
        db.Context.TurnRates.Count().ShouldBe(2);
    }

    [Fact]
    public void Rate_StoresCorrectConversationId()
    {
        using var db = new FreshDbContext();
        var engine = CreateEngine(db);
        engine.ProcessInput("My name is TestUser");
        engine.ProcessInput("I like pizza");
        engine.ProcessInput("~rate +1");

        var rating = db.Context.TurnRates.Single();
        var conversations = db.Context.Conversations.ToList();
        conversations.Count.ShouldBe(1);
        rating.ConversationId.ShouldBe(conversations[0].Id);
    }

    [Fact]
    public void Rate_StoresCorrectUserId()
    {
        using var db = new FreshDbContext();
        var engine = CreateEngine(db);
        engine.ProcessInput("My name is TestUser");
        engine.ProcessInput("I like pizza");
        engine.ProcessInput("~rate +1");

        var rating = db.Context.TurnRates.Single();
        rating.UserId.ShouldNotBeNull();
        rating.UserId.ShouldBe(engine.CurrentUserId);
    }

    [Fact]
    public void Rate_HelpTextContainsRate()
    {
        var help = ChatEngine.GetHelpText();
        help.ShouldContain("~rate");
    }

    [Fact]
    public void Feedback_PositivePhrase_StoresRating()
    {
        using var db = new FreshDbContext();
        var engine = CreateEngine(db);
        engine.ProcessInput("My name is TestUser");
        engine.ProcessInput("I like pizza");
        var result = engine.ProcessInput("That was helpful");

        result.ShouldBe("Glad you liked it!");
        db.Context.TurnRates.ShouldContain(t => t.Rating == 1);
    }

    [Fact]
    public void Feedback_Thanks_StoresRating()
    {
        using var db = new FreshDbContext();
        var engine = CreateEngine(db);
        engine.ProcessInput("My name is TestUser");
        engine.ProcessInput("I like pizza");
        var result = engine.ProcessInput("Thanks!");

        result.ShouldBe("Glad you liked it!");
        db.Context.TurnRates.ShouldContain(t => t.Rating == 1);
    }

    [Fact]
    public void Feedback_ThankYou_StoresRating()
    {
        using var db = new FreshDbContext();
        var engine = CreateEngine(db);
        engine.ProcessInput("My name is TestUser");
        engine.ProcessInput("I like pizza");
        var result = engine.ProcessInput("Thank you");

        result.ShouldBe("Glad you liked it!");
        db.Context.TurnRates.ShouldContain(t => t.Rating == 1);
    }

    [Fact]
    public void Feedback_ThatWasUseful_StoresRating()
    {
        using var db = new FreshDbContext();
        var engine = CreateEngine(db);
        engine.ProcessInput("My name is TestUser");
        engine.ProcessInput("I like pizza");
        var result = engine.ProcessInput("That was useful");

        result.ShouldBe("Glad you liked it!");
        db.Context.TurnRates.ShouldContain(t => t.Rating == 1);
    }

    [Fact]
    public void Feedback_SpotOn_StoresRating()
    {
        using var db = new FreshDbContext();
        var engine = CreateEngine(db);
        engine.ProcessInput("My name is TestUser");
        engine.ProcessInput("I like pizza");
        var result = engine.ProcessInput("Spot on!");

        result.ShouldBe("Glad you liked it!");
        db.Context.TurnRates.ShouldContain(t => t.Rating == 1);
    }

    [Fact]
    public void Feedback_NailedIt_StoresRating()
    {
        using var db = new FreshDbContext();
        var engine = CreateEngine(db);
        engine.ProcessInput("My name is TestUser");
        engine.ProcessInput("I like pizza");
        var result = engine.ProcessInput("Nailed it");

        result.ShouldBe("Glad you liked it!");
        db.Context.TurnRates.ShouldContain(t => t.Rating == 1);
    }

    [Fact]
    public void Feedback_NegativePhrase_StoresRating()
    {
        using var db = new FreshDbContext();
        var engine = CreateEngine(db);
        engine.ProcessInput("My name is TestUser");
        engine.ProcessInput("I like pizza");
        var result = engine.ProcessInput("That was useless");

        result.ShouldBe("Noted — I'll try to do better.");
        db.Context.TurnRates.ShouldContain(t => t.Rating == -1);
    }

    [Fact]
    public void Feedback_ThatDidntHelp_StoresRating()
    {
        using var db = new FreshDbContext();
        var engine = CreateEngine(db);
        engine.ProcessInput("My name is TestUser");
        engine.ProcessInput("I like pizza");
        var result = engine.ProcessInput("That didn't help me");

        result.ShouldBe("Noted — I'll try to do better.");
        db.Context.TurnRates.ShouldContain(t => t.Rating == -1);
    }

    [Fact]
    public void Feedback_MetaCommentaryAlsoRates()
    {
        using var db = new FreshDbContext();
        var engine = CreateEngine(db);
        engine.ProcessInput("My name is TestUser");
        engine.ProcessInput("I like pizza");
        var result = engine.ProcessInput("That doesn't make sense");

        result.ShouldNotBeNullOrEmpty();
        db.Context.TurnRates.ShouldContain(t => t.Rating == -1);
    }

    [Fact]
    public void Feedback_DuplicateRating_AlreadyRated()
    {
        using var db = new FreshDbContext();
        var engine = CreateEngine(db);
        engine.ProcessInput("My name is TestUser");
        engine.ProcessInput("I like pizza");
        engine.ProcessInput("Thanks!");
        var result = engine.ProcessInput("Thanks!");

        result.ShouldContain("already rated");
        db.Context.TurnRates.Count().ShouldBe(1);
    }

    [Fact]
    public void Feedback_NoConversationYet_ReturnsFalse()
    {
        using var db = new FreshDbContext();
        var engine = CreateEngine(db);
        engine.ProcessInput("My name is TestUser");

        var result = engine.ProcessInput("Thanks!");
        result.ShouldNotContain("Glad you liked it");
        db.Context.TurnRates.ShouldBeEmpty();
    }

    [Fact]
    public void Feedback_TooShort_Ignored()
    {
        using var db = new FreshDbContext();
        var engine = CreateEngine(db);
        engine.ProcessInput("My name is TestUser");
        engine.ProcessInput("I like pizza");
        var result = engine.ProcessInput("ok");

        result.ShouldNotContain("Glad you liked it");
        result.ShouldNotContain("Noted");
        db.Context.TurnRates.ShouldBeEmpty();
    }

    [Fact]
    public void Feedback_UnrelatedInput_Ignored()
    {
        using var db = new FreshDbContext();
        var engine = CreateEngine(db);
        engine.ProcessInput("My name is TestUser");
        engine.ProcessInput("I like pizza");
        var result = engine.ProcessInput("What is the weather today");

        db.Context.TurnRates.ShouldBeEmpty();
    }
}

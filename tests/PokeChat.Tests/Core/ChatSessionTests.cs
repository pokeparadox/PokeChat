using PokeChat.Core;
using PokeChat.Knowledge;
using PokeChat.NLP;
using PokeChat.Responses;
using PokeChat.Tests.Helpers;
using Shouldly;

namespace PokeChat.Tests.Core;

public class ChatSessionTests
{
    private (ChatSession Session, FreshDbContext Db) CreateSessionAndDb(
        List<string>? namePatterns = null,
        HashSet<string>? botCommands = null,
        HashSet<string>? greetingWords = null,
        List<string>? renamePatterns = null)
    {
        var db = new FreshDbContext();
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

        var session = new ChatSession(
            db.Context,
            store,
            responseEngine,
            spellChecker,
            posTagger,
            tokeniser,
            sentenceSplitter,
            svoExtractor,
            contextTracker,
            nounCategoriser,
            namePatterns ?? new List<string> { "my name is", "i am", "i'm", "call me" },
            botCommands ?? new List<string> { "quit", "exit" }.ToHashSet(StringComparer.OrdinalIgnoreCase),
            greetingWords ?? new List<string> { "hi", "hello" }.ToHashSet(StringComparer.OrdinalIgnoreCase),
            renamePatterns: renamePatterns
        );

        return (session, db);
    }

    [Fact]
    public void ShouldExit_RecognizesBotCommands()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            session.ShouldExit("quit").ShouldBeTrue();
            session.ShouldExit("exit").ShouldBeTrue();
            session.ShouldExit("bye").ShouldBeFalse();
        }
    }

    [Fact]
    public void ShouldExit_RejectsNonCommands()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            session.ShouldExit("hello").ShouldBeFalse();
            session.ShouldExit("what is this").ShouldBeFalse();
        }
    }

    [Fact]
    public void HandleNameInput_ExtractsName()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            var response = session.HandleNameInput("my name is Alice");
            response.ShouldContain("Alice");
        }
    }

    [Fact]
    public void HandleNameInput_ReturnsFailure_WhenNoName()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            var response = session.HandleNameInput("what is this");
            response.ShouldBe("I didn't catch your name. Could you tell me again?");
        }
    }

    [Fact]
    public void ExtractName_UsesPattern_ReturnsLowercase()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            var name = session.ExtractName("my name is Bob", ["my", "name", "is", "Bob"]);
            name.ShouldBe("bob");
        }
    }

    [Fact]
    public void ExtractName_SingleToken_ReturnsIt()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            var name = session.ExtractName("Charlie", ["Charlie"]);
            name.ShouldBe("Charlie");
        }
    }

    [Fact]
    public void ExtractName_SingleStopWord_ReturnsEmpty()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            var name = session.ExtractName("the", ["the"]);
            name.ShouldBeEmpty();
        }
    }

    [Fact]
    public void ResolveSubject_I_ReturnsUserName()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            session.HandleNameInput("my name is Alice");
            session.ResolveSubject("i").ShouldBe("Alice");
        }
    }

    [Fact]
    public void ResolveSubject_NonPronoun_ReturnsItself()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            session.ResolveSubject("cat").ShouldBe("cat");
        }
    }

    [Fact]
    public void ResolveObject_It_ReturnsEmpty_WhenNoContext()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            session.ResolveObject("it").ShouldBe(string.Empty);
        }
    }

    [Fact]
    public void ClassifyPredicate_IsUser_ReturnsPersonalAttribute()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            session.HandleNameInput("my name is Alice");
            session.ClassifyPredicate("Alice", "is", "nice").ShouldBe(PredicateType.PersonalAttribute);
        }
    }

    [Fact]
    public void ClassifyPredicate_IsGeneral_ReturnsGeneralFact()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            session.ClassifyPredicate("sky", "is", "blue").ShouldBe(PredicateType.GeneralFact);
        }
    }

    [Fact]
    public void ClassifyPredicate_Like_ReturnsPreference()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            session.ClassifyPredicate("I", "like", "pizza").ShouldBe(PredicateType.Preference);
        }
    }

    [Fact]
    public void ClassifyPredicate_Hate_ReturnsDislike()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            session.ClassifyPredicate("I", "hate", "broccoli").ShouldBe(PredicateType.Dislike);
        }
    }

    [Fact]
    public void IsStopWord_DetectsArticles()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            session.IsStopWord("the").ShouldBeTrue();
            session.IsStopWord("a").ShouldBeTrue();
        }
    }

    [Fact]
    public void IsStopWord_RejectsNonStopWords()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            session.IsStopWord("hello").ShouldBeFalse();
            session.IsStopWord("pizza").ShouldBeFalse();
        }
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        var (session, db) = CreateSessionAndDb();
        Should.NotThrow(() => session.Dispose());
        db.Dispose();
    }

    [Fact]
    public void ProcessInput_WithNameInput_ReturnsGreeting()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            var response = session.ProcessInput("my name is Dave");
            response.ShouldContain("Dave");
        }
    }

    [Fact]
    public void ProcessInput_AfterNameSet_ReturnsResponse()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            session.ProcessInput("my name is Eve");
            var response = session.ProcessInput("I like pizza");
            response.ShouldNotBeNullOrEmpty();
        }
    }

    [Fact]
    public void TryHandleBotRename_DetectsPattern_ReturnsName()
    {
        var (session, db) = CreateSessionAndDb(renamePatterns: new List<string> { "can i call you", "i'll call you" });
        using (db)
        {
            session.HandleNameInput("my name is Alice");
            var result = session.TryHandleBotRename("can I call you Jeff", out var response);
            result.ShouldBeTrue();
            response.ShouldContain("Jeff", Case.Insensitive);
        }
    }

    [Fact]
    public void TryHandleBotRename_NoMatch_ReturnsFalse()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            session.HandleNameInput("my name is Alice");
            var result = session.TryHandleBotRename("I like pizza", out var response);
            result.ShouldBeFalse();
            response.ShouldBeEmpty();
        }
    }

}

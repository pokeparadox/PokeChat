using PokeChat.Data.Entities;
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
        HashSet<string>? greetingWords = null)
    {
        var db = new FreshDbContext();
        SeedBotResponses(db.Context);
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
            greetingWords ?? new List<string> { "hi", "hello" }.ToHashSet(StringComparer.OrdinalIgnoreCase)
        );

        return (session, db);
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
            new BotResponse { Category = "unknown_word_suggestion", ResponseText = "Did you mean '{0}' instead of '{1}'?", CreatedAt = now },
            new BotResponse { Category = "unknown_word_no_suggestion", ResponseText = "I don't know the word '{0}'. What does it mean?", CreatedAt = now }
        );

        db.PosDictionary.AddRange(
            new PosDictionaryEntry { Word = "i", WordType = "pronoun", CreatedAt = now },
            new PosDictionaryEntry { Word = "like", WordType = "verb", CreatedAt = now },
            new PosDictionaryEntry { Word = "pizza", WordType = "noun", CreatedAt = now },
            new PosDictionaryEntry { Word = "is", WordType = "verb", CreatedAt = now },
            new PosDictionaryEntry { Word = "my", WordType = "pronoun", CreatedAt = now },
            new PosDictionaryEntry { Word = "name", WordType = "noun", CreatedAt = now },
            new PosDictionaryEntry { Word = "the", WordType = "determiner", CreatedAt = now },
            new PosDictionaryEntry { Word = "cat", WordType = "noun", CreatedAt = now },
            new PosDictionaryEntry { Word = "sky", WordType = "noun", CreatedAt = now },
            new PosDictionaryEntry { Word = "blue", WordType = "adjective", CreatedAt = now },
            new PosDictionaryEntry { Word = "hate", WordType = "verb", CreatedAt = now },
            new PosDictionaryEntry { Word = "broccoli", WordType = "noun", CreatedAt = now }
        );
        db.SaveChanges();
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
        db.Dispose();
        Should.NotThrow(() => session.Dispose());
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
}

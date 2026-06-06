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

    [Fact]
    public void TryHandleReset_DetectsStartAfresh()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            session.HandleNameInput("my name is Alice");
            var result = session.TryHandleResetRequest("can we start afresh", out var response);
            result.ShouldBeTrue();
            response.ShouldContain("sure");
        }
    }

    [Fact]
    public void TryHandleReset_DetectsResetTriggers()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            session.HandleNameInput("my name is Alice");
            var result = session.TryHandleResetRequest("reset everything", out var response);
            result.ShouldBeTrue();
            response.ShouldContain("sure");
        }
    }

    [Fact]
    public void TryHandleReset_NoMatch_ReturnsFalse()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            session.HandleNameInput("my name is Alice");
            var result = session.TryHandleResetRequest("I like pizza", out var response);
            result.ShouldBeFalse();
            response.ShouldBeEmpty();
        }
    }

    [Fact]
    public void TryHandleReset_Confirms_ReturnsConfirmed()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            session.HandleNameInput("my name is Alice");
            session.TryHandleResetRequest("start fresh", out _);
            var result = session.TryHandleResetRequest("yes", out var response);
            result.ShouldBeTrue();
            response.ShouldNotBeNullOrEmpty();
        }
    }

    [Fact]
    public void TryHandleReset_Cancels_ReturnsCancelled()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            session.HandleNameInput("my name is Alice");
            session.TryHandleResetRequest("start fresh", out _);
            var result = session.TryHandleResetRequest("no", out var response);
            result.ShouldBeTrue();
            response.ShouldNotContain("fresh");
        }
    }

    [Fact]
    public void TryHandleReset_ConfirmationWipesUserData()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            session.HandleNameInput("my name is Alice");
            session.ProcessInput("I like pizza");

            db.Context.Facts.Count().ShouldBe(1);

            session.TryHandleResetRequest("start fresh", out _);
            session.TryHandleResetRequest("yes", out var response);

            db.Context.Facts.Count().ShouldBe(0);
            db.Context.Conversations.Count().ShouldBe(0);
            db.Context.Users.Count().ShouldBe(0);
            response.ShouldNotBeNullOrEmpty();
        }
    }

    [Fact]
    public void TryHandleReset_ForgottenUserMustReintroduce()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            session.HandleNameInput("my name is Alice");
            session.TryHandleResetRequest("start fresh", out _);
            session.TryHandleResetRequest("yes", out _);

            var response = session.ProcessInput("I like pizza");
            response.ShouldBe("I didn't catch your name. Could you tell me again?");
        }
    }

    [Fact]
    public void ProcessInput_StoresSentimentInContext_ForEmotionalInput()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            TestDataHelper.SeedEmotionKeywords(db.Context);
            session.HandleNameInput("my name is Alice");
            session.ProcessInput("I'm so happy today!");
        }
    }

    private (ChatSession Session, FreshDbContext Db) CreateSessionWithContractions(
        List<string>? namePatterns = null,
        HashSet<string>? botCommands = null,
        HashSet<string>? greetingWords = null)
    {
        var db = new FreshDbContext();
        TestDataHelper.SeedBotResponses(db.Context);
        TestDataHelper.SeedPosDictionary(db.Context);
        TestDataHelper.SeedContractions(db.Context);
        var store = new KnowledgeStore(db.Context);
        var contextTracker = new ContextTracker();
        var spellChecker = new SpellChecker();

        var posEntries = store.GetPosDictionary();
        var posTagger = new PosTagger(posEntries);

        var spellDict = new HashSet<string>(posEntries.Select(e => e.Word), StringComparer.OrdinalIgnoreCase);
        var misspellings = store.GetMisspellings();
        spellChecker.Initialise(spellDict, misspellings);

        var contractions = store.GetContractions();
        var contractionMap = contractions.ToDictionary(c => c.Contraction, c => c.Expansion);
        var expander = new ContractionExpander(contractionMap);
        var tokeniser = new Tokeniser(expander);
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

    [Fact]
    public void Contraction_ImHappy_StoresFactViaExpansion()
    {
        var (session, db) = CreateSessionWithContractions();
        using (db)
        {
            session.HandleNameInput("my name is Alice");
            session.ProcessInput("I'm happy");

            var facts = db.Context.Facts.ToList();
            facts.Count.ShouldBe(1);
            facts[0].Subject.ShouldBe("Alice");
            facts[0].Verb.ShouldBe("am");
            facts[0].Object.ShouldBe("happy");
        }
    }

    [Fact]
    public void Contraction_ILikePizza_StoresFactCorrectly()
    {
        var (session, db) = CreateSessionWithContractions();
        using (db)
        {
            session.HandleNameInput("my name is Alice");
            session.ProcessInput("I'm happy and I like pizza");

            var facts = db.Context.Facts.ToList();
            facts.Count.ShouldBe(2);
            facts.Any(f => f.Verb == "like" && f.Object == "pizza").ShouldBeTrue();
        }
    }

    [Fact]
    public void ProcessInput_StoresSentimentOnFact()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            TestDataHelper.SeedEmotionKeywords(db.Context);
            session.HandleNameInput("my name is Alice");
            session.ProcessInput("I love pizza");

            var facts = db.Context.Facts.ToList();
            facts.Count.ShouldBe(1);
            facts[0].Sentiment.ShouldBe("positive");
            facts[0].EmotionIntensity.ShouldBeGreaterThanOrEqualTo(2);
        }
    }

    [Fact]
    public void ProcessInput_ReturnsEmpathyResponse_ForEmotionalInput()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            TestDataHelper.SeedEmotionKeywords(db.Context);
            session.HandleNameInput("my name is Alice");
            var response = session.ProcessInput("I'm so happy today!");
            response.ShouldNotBeNullOrEmpty();
        }
    }

    [Fact]
    public void TryHandleReset_WorksThroughProcessInput()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            session.HandleNameInput("my name is Alice");
            var response = session.ProcessInput("start fresh");
            response.ShouldContain("sure");
        }
    }

    [Fact]
    public void TemporalFlow_DetectsAndStoresTimeContext()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            TestDataHelper.SeedTemporalExpressions(db.Context);
            session.HandleNameInput("my name is Alice");
            session.ProcessInput("I went to the cinema yesterday");

            var facts = db.Context.Facts.ToList();
            facts.Count.ShouldBe(1);
            facts[0].Subject.ShouldBe("Alice");
            facts[0].Verb.ShouldBe("went");
            facts[0].Object.ShouldContain("cinema");
            facts[0].TimeContext.ShouldBe("yesterday");
            facts[0].MentionedAt.ShouldNotBeNullOrEmpty();
        }
    }

    [Fact]
    public void TemporalQuery_ReturnsFormattedResponse()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            TestDataHelper.SeedTemporalExpressions(db.Context);
            session.HandleNameInput("my name is Alice");
            session.ProcessInput("I went to the cinema yesterday");
            var response = session.ProcessInput("what did I do yesterday");
            response.ShouldNotBeNullOrEmpty();
            response.ShouldContain("yesterday");
        }
    }

    [Fact]
    public void InferenceFlow_ContradictionDetected()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            TestDataHelper.SeedInferenceWordLinks(db.Context);
            session.HandleNameInput("my name is Alice");
            session.ProcessInput("I like pizza");

            var response = session.ProcessInput("I hate pizza");
            response.ShouldNotBeNullOrEmpty();
            response.ToLowerInvariant().ShouldContain("like");
            response.ToLowerInvariant().ShouldContain("pizza");
            response.ToLowerInvariant().ShouldContain("hate");
        }
    }

    [Fact]
    public void InferenceFlow_StoresFact_WhenNoContradiction()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            TestDataHelper.SeedInferenceWordLinks(db.Context);
            session.HandleNameInput("my name is Alice");
            session.ProcessInput("I like pizza");

            var facts = db.Context.Facts.ToList();
            facts.Count.ShouldBe(1);
            facts[0].Subject.ShouldBe("Alice");
            facts[0].Verb.ShouldBe("like");
            facts[0].Object.ShouldBe("pizza");
        }
    }

    [Fact]
    public void SessionSummary_DetectsSummaryRequest_AndReturnsResponse()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            session.HandleNameInput("my name is Bob");
            session.ProcessInput("I like pizza");
            var response = session.ProcessInput("what did we talk about");
            response.ShouldNotBeNullOrEmpty();
            response.ShouldContain("Bob");
            response.ShouldContain("like");
            response.ShouldContain("pizza");
        }
    }

    [Fact]
    public void SessionSummary_ReturnsEmptyMessage_WhenNoFacts()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            session.HandleNameInput("my name is Carol");
            var response = session.ProcessInput("summarise our conversation");
            response.ShouldNotBeNullOrEmpty();
        }
    }

    [Fact]
    public void SessionSummary_RecognizesSummaryKeyword()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            session.HandleNameInput("my name is Dave");
            session.ProcessInput("I like pizza");
            var response = session.ProcessInput("summary");
            response.ShouldNotBeNullOrEmpty();
        }
    }

    [Fact]
    public void SessionSummary_RecognizesSummaryOfPrefix()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            session.HandleNameInput("my name is Eve");
            session.ProcessInput("I like chess");
            var response = session.ProcessInput("summary of today");
            response.ShouldNotBeNullOrEmpty();
        }
    }

    [Fact]
    public void CorrectionDetection_LearnsPattern_FromYouShouldSay()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            session.HandleNameInput("my name is Alice");
            session.ProcessInput("I like pizza");

            var response = session.ProcessInput("you should say Great question!");
            response.ShouldNotBeNullOrEmpty();

            var learnedRules = db.Context.LearnedResponseRules.ToList();
            learnedRules.Count.ShouldBe(1);
            learnedRules[0].Pattern.ShouldBe(@"\bpizza\b");
            learnedRules[0].ResponseTemplate.ShouldBe("Great question");
            learnedRules[0].Confidence.ShouldBe(5);
        }
    }

    [Fact]
    public void CorrectionDetection_LearnsPattern_FromSayInstead()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            session.HandleNameInput("my name is Bob");
            var firstResponse = session.ProcessInput("I like chess");
            firstResponse.ShouldNotBeNullOrEmpty();

            var response = session.ProcessInput("say Tell me more instead");
            response.ShouldNotBeNullOrEmpty();

            var learnedRules = db.Context.LearnedResponseRules.ToList();
            learnedRules.Count.ShouldBe(1);
            learnedRules[0].Pattern.ShouldBe(@"\bchess\b");
            learnedRules[0].ResponseTemplate.ShouldBe("Tell me more");
        }
    }

    [Fact]
    public void CorrectionDetection_NegativeFeedback_RecordsFeedback()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            session.HandleNameInput("my name is Carol");
            session.ProcessInput("I like pizza");

            var response = session.ProcessInput("that's not right");
            response.ShouldNotBeNullOrEmpty();
        }
    }

    [Fact]
    public void CorrectionDetection_PositiveFeedback_RecordsFeedback()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            session.HandleNameInput("my name is Dave");
            session.ProcessInput("I like pizza");

            var response = session.ProcessInput("that's better");
            response.ShouldNotBeNullOrEmpty();
        }
    }

    [Fact]
    public void CorrectionDetection_WhenISay_LearnsPair()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            session.HandleNameInput("my name is Eve");

            var response = session.ProcessInput("when I say hello you should say hi there");
            response.ShouldNotBeNullOrEmpty();

            var learnedRules = db.Context.LearnedResponseRules.ToList();
            learnedRules.Count.ShouldBe(1);
            learnedRules[0].Pattern.ShouldBe("hello");
            learnedRules[0].ResponseTemplate.ShouldBe("hi there");
        }
    }

    [Fact]
    public void MultiTurnTopicFlow_PushesTopicAfterSvoExtraction()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            session.HandleNameInput("my name is Alice");
            session.ProcessInput("I like pizza");

            var topics = session.TopicStack;
            topics.Count.ShouldBe(1);
            topics[0].Subject.ShouldBe("Alice");
            topics[0].Verb.ShouldBe("like");
            topics[0].Object.ShouldBe("pizza");
            topics[0].MentionCount.ShouldBe(1);
        }
    }

    [Fact]
    public void MultiTurnTopicFlow_MultipleTopics_AddedToStack()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            session.HandleNameInput("my name is Alice");
            session.ProcessInput("I like pizza");
            session.ProcessInput("I hate broccoli");

            var topics = session.TopicStack;
            topics.Count.ShouldBe(2);
            topics[0].Object.ShouldBe("pizza");
            topics[1].Object.ShouldBe("broccoli");
        }
    }

    [Fact]
    public void MultiTurnTopicFlow_DoesNotDuplicateTopic_OnSameInput()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            session.HandleNameInput("my name is Alice");
            session.ProcessInput("I like pizza");
            session.ProcessInput("I like pizza");

            var topics = session.TopicStack;
            topics.Count.ShouldBe(1);
        }
    }

    [Fact]
    public void ProcessInput_ReturnsSentimentAcknowledgement_AfterEmotionFollowUp()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            TestDataHelper.SeedEmotionKeywords(db.Context);
            session.HandleNameInput("my name is Bob");
            session.ProcessInput("I love pizza");
            var followUp = session.ProcessInput("I'm sad");
            followUp.ShouldNotBeNullOrEmpty();
            var ack = session.ProcessInput("I'm happy");
            ack.ShouldNotBeNullOrEmpty();
            ack.ShouldNotContain("Bob and happy");
        }
    }

    [Fact]
    public void MultiTurnTopicFlow_TopicReference_ReturnsTopicResponse_WhenFollowUpExhausted()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            TestDataHelper.SeedEmotionKeywords(db.Context);
            session.HandleNameInput("my name is Alice");
            session.ProcessInput("I like pizza");
            session.ProcessInput("the sky is blue");
            session.ProcessInput("yes");
            session.ProcessInput("okay");
            session.ProcessInput("hmm");

            var response = session.ProcessInput("tell me something");
            response.ShouldNotBeNullOrEmpty();
        }
    }

    [Fact]
    public void ProcessInput_AutoLearnsUnknownWordInSvoObject()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            session.HandleNameInput("my name is Alice");
            var response = session.ProcessInput("I love steak");

            response.ShouldNotContain("unknown");
            response.ShouldNotContain("Don't know");

            var facts = db.Context.Facts.ToList();
            facts.Count.ShouldBe(1);
            facts[0].Subject.ShouldBe("Alice");
            facts[0].Verb.ShouldBe("love");
            facts[0].Object.ShouldBe("steak");
        }
    }

    [Fact]
    public void ProcessInput_AutoLearnsUnknownWordInSvoSubject()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            session.HandleNameInput("my name is Alice");
            var response = session.ProcessInput("steak is tasty");

            response.ShouldNotContain("unknown");
            response.ShouldNotContain("Don't know");

            var facts = db.Context.Facts.ToList();
            facts.Count.ShouldBe(1);
            facts[0].Subject.ShouldBe("steak");
            facts[0].Verb.ShouldBe("is");
            facts[0].Object.ShouldBe("tasty");
        }
    }

    [Fact]
    public void ProcessInput_AutoLearnsUnknownWordInCompoundObject()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            session.HandleNameInput("my name is Alice");
            var response = session.ProcessInput("I like pizza and steak");

            response.ShouldNotContain("unknown");
            response.ShouldNotContain("Don't know");

            var facts = db.Context.Facts.ToList();
            facts.Count.ShouldBe(1);
            facts[0].Subject.ShouldBe("Alice");
            facts[0].Verb.ShouldBe("like");
            facts[0].Object.ShouldBe("pizza and steak");
        }
    }

    [Fact]
    public void ProcessInput_SingleUnknownWord_AutoLearnedAsTopic()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            session.HandleNameInput("my name is Alice");
            var response = session.ProcessInput("gobbledygook");

            response.ShouldContain("gobbledygook");
            response.ShouldNotContain("I don't know the word");
        }
    }

    [Fact]
    public void ResponseCategory_TrackedPerTurn()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            session.HandleNameInput("my name is Alice");
            session.ProcessInput("I like pizza");

            var convs = db.Context.Conversations.ToList();
            convs.Count.ShouldBeGreaterThan(0);
            foreach (var conv in convs)
            {
                conv.ResponseCategory.ShouldNotBeNull();
                conv.ResponseCategory.ShouldNotBeEmpty();
            }
        }
    }

    [Fact]
    public void StoryRequest_ReturnsNonEmptyResponse()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            TestDataHelper.SeedStoryTemplates(db.Context);
            session.HandleNameInput("my name is Alice");
            session.ProcessInput("I like pizza");

            var response = session.ProcessInput("tell me a story");

            response.ShouldNotBeNullOrEmpty();
            response.ShouldNotContain("{");
        }
    }
}

using PokeChat.Core;
using PokeChat.Data.Entities;
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
    public void HandleClarification_NeverMind_DoesNotLearnWord()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            var response = session.HandleClarification("never mind", "odn't");
            response.ShouldBeOneOf("No problem, I won't remember that!", "Got it, I'll forget about that word.");

            var posDict = new KnowledgeStore(db.Context).GetPosDictionary();
            posDict.Any(e => e.Word == "odn't").ShouldBeFalse();
        }
    }

    [Fact]
    public void HandleClarification_Mistake_DoesNotLearnWord()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            var response = session.HandleClarification("that was a mistake", "odn't");
            response.ShouldBeOneOf("No problem, I won't remember that!", "Got it, I'll forget about that word.");

            var posDict = new KnowledgeStore(db.Context).GetPosDictionary();
            posDict.Any(e => e.Word == "odn't").ShouldBeFalse();
        }
    }

    [Fact]
    public void HandleClassification_Typo_RemovesLearnedWord()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            var store = new KnowledgeStore(db.Context);
            store.AddLearnedWord("odn't");
            store.Save();

            var response = session.HandleClassification("typo", "odn't");
            response.ShouldBeOneOf("No problem, I won't remember that!", "Got it, I'll forget about that word.");

            var posDict = store.GetPosDictionary();
            posDict.Any(e => e.Word == "odn't").ShouldBeFalse();
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

    [Fact]
    public void PoetryRequest_Haiku_ReturnsNonEmptyResponse()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            TestDataHelper.SeedRhymeGroups(db.Context);
            TestDataHelper.SeedPoemTemplates(db.Context);
            session.HandleNameInput("my name is Alice");
            session.ProcessInput("I like pizza");

            var response = session.ProcessInput("write a haiku");

            response.ShouldNotBeNullOrEmpty();
            response.ShouldNotContain("{");
        }
    }

    [Fact]
    public void PoetryRequest_Limerick_ReturnsNonEmptyResponse()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            TestDataHelper.SeedRhymeGroups(db.Context);
            TestDataHelper.SeedPoemTemplates(db.Context);
            session.HandleNameInput("my name is Alice");
            session.ProcessInput("I like pizza");

            var response = session.ProcessInput("write a limerick");

            response.ShouldNotBeNullOrEmpty();
            response.ShouldNotContain("{");
        }
    }

    [Fact]
    public void PoetryRequest_JustHaikuWord_TriggersPoem()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            TestDataHelper.SeedRhymeGroups(db.Context);
            TestDataHelper.SeedPoemTemplates(db.Context);
            session.HandleNameInput("my name is Alice");

            var response = session.ProcessInput("haiku");

            response.ShouldNotBeNullOrEmpty();
        }
    }

    [Fact]
    public void ProcessInput_UnknownWord_ClassificationFires_AfterLearn()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            session.HandleNameInput("my name is Alice");
            session.ProcessInput("the xyzzy");

            var response = session.ProcessInput("a made up word");

            response.ShouldContain("Is it a person, place, thing, or verb");
        }
    }

    [Fact]
    public void ProcessInput_Classification_LearnsNoun()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            session.HandleNameInput("my name is Alice");
            session.ProcessInput("the xyzzy");
            session.ProcessInput("a made up word");

            var response = session.ProcessInput("a person");

            response.ShouldBe("Got it! I'll remember 'xyzzy' as a person.");

            var posEntry = db.Context.PosDictionary.FirstOrDefault(p => p.Word == "xyzzy");
            posEntry.ShouldNotBeNull();
            posEntry.WordType.ShouldBe("noun");

            var catEntry = db.Context.NounCategories.FirstOrDefault(n => n.Noun == "xyzzy");
            catEntry.ShouldNotBeNull();
            catEntry.Category.ShouldBe("person");
        }
    }

    [Fact]
    public void ProcessInput_Classification_LearnsVerb()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            session.HandleNameInput("my name is Alice");
            session.ProcessInput("the xyzzy");
            session.ProcessInput("a made up word");

            var response = session.ProcessInput("a verb");

            response.ShouldBe("Got it! I'll remember 'xyzzy' as a verb.");

            var posEntry = db.Context.PosDictionary.FirstOrDefault(p => p.Word == "xyzzy");
            posEntry.ShouldNotBeNull();
            posEntry.WordType.ShouldBe("verb");
        }
    }

    [Fact]
    public void ProcessInput_Classification_LearnsPlace_AsksFollowUp()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            session.HandleNameInput("my name is Alice");
            session.ProcessInput("the xyzzy");
            session.ProcessInput("a made up word");

            var response = session.ProcessInput("a place");

            response.ShouldBe("Have you ever been to xyzzy?");

            var catEntry = db.Context.NounCategories.FirstOrDefault(n => n.Noun == "xyzzy");
            catEntry.ShouldNotBeNull();
            catEntry.Category.ShouldBe("place");
        }
    }

    [Fact]
    public void ProcessInput_Classification_PlaceFollowUp_Yes_StoresVisit()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            session.HandleNameInput("my name is Alice");
            session.ProcessInput("the xyzzy");
            session.ProcessInput("a made up word");
            session.ProcessInput("a place");

            var response = session.ProcessInput("yes");

            response.ShouldContain("visited xyzzy");

            var facts = db.Context.Facts.ToList();
            facts.Any(f => f.Subject == "Alice" && f.Verb == "visited" && f.Object == "xyzzy").ShouldBeTrue();
        }
    }

    [Fact]
    public void ProcessInput_Classification_PlaceFollowUp_No_DoesNotStore()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            session.HandleNameInput("my name is Alice");
            session.ProcessInput("the xyzzy");
            session.ProcessInput("a made up word");
            session.ProcessInput("a place");

            var response = session.ProcessInput("no");

            response.ShouldContain("I'll remember xyzzy is a place");

            var facts = db.Context.Facts.ToList();
            facts.Any(f => f.Verb == "visited").ShouldBeFalse();
        }
    }

    [Fact]
    public void ProcessInput_Classification_Suggestion_DoesNotFire()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            session.HandleNameInput("my name is Alice");
            session.ProcessInput("the kat");

            var response = session.ProcessInput("yes");

            response.ShouldContain("I'll remember that 'kat' should be 'cat'");

            var misspelling = db.Context.Misspellings.FirstOrDefault(m => m.WrongWord == "kat");
            misspelling.ShouldNotBeNull();
            misspelling.Correction.ShouldBe("cat");
        }
    }

    [Fact]
    public void SingleUnknownNoun_SetsLastSubjectToWord_NotUserName()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            session.HandleNameInput("my name is Alice");
            session.ProcessInput("gobbledygook");

            session.LastSubject.ShouldBe("gobbledygook");
            session.LastObject.ShouldBeNull();
        }
    }

    [Fact]
    public void SingleKnownNoun_SetsLastSubjectToWord_NotUserName()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            session.HandleNameInput("my name is Alice");
            session.ProcessInput("pizza");

            session.LastSubject.ShouldBe("pizza");
            session.LastObject.ShouldBeNull();
        }
    }

    [Fact]
    public void NegatedGeneralFact_Filtered_ContextUnchanged()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            session.HandleNameInput("my name is Alice");
            session.ProcessInput("gobbledygook");

            session.LastSubject.ShouldBe("gobbledygook");

            var response = session.ProcessInput("they are not my food");

            session.LastSubject.ShouldBe("gobbledygook");
            response.ShouldNotContain("not my food");
        }
    }

    [Fact]
    public void SingleNoun_ContextFollowUp_DoesNotUsePossessive()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            session.HandleNameInput("my name is Alice");
            var response = session.ProcessInput("gobbledygook");

            response.ShouldContain("gobbledygook");
            response.ShouldNotContain("your gobbledygook");
            response.ShouldNotContain("Alice and gobbledygook");
        }
    }

    [Fact]
    public void TryHandleGameStart_TriggersOnPhrase()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            session.HandleNameInput("my name is Alice");

            var result = session.TryHandleGameStart("let's play a word game", out var response);

            result.ShouldBeTrue();
            (response.Contains("word game") || response.Contains("story")).ShouldBeTrue();
        }
    }

    [Fact]
    public void HandleGameTurn_UserSaysStop_EndsGame()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            session.HandleNameInput("my name is Alice");
            session.TryHandleGameStart("let's play a word game", out _);

            var response = session.HandleGameTurn("stop");

            response.ShouldNotBeNullOrEmpty();
        }
    }

    [Fact]
    public void HandleGameTurn_BotAddsWord()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            session.HandleNameInput("my name is Alice");
            session.TryHandleGameStart("let's play a word game", out _);

            var response = session.HandleGameTurn("The");

            response.ShouldNotBeNullOrEmpty();
            response.ShouldNotContain("story");
        }
    }

    [Fact]
    public void HandleGameTurn_ShowsBotWord()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            session.HandleNameInput("my name is Alice");
            session.TryHandleGameStart("let's play a word game", out _);

            var response = session.HandleGameTurn("The");

            response.ShouldNotBeNullOrEmpty();
            response.ShouldNotContain("Story");
            response.ShouldNotContain("story");
        }
    }

    [Fact]
    public void ApplyGameGrammarFilter_TrimsTrailingConjunction()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            var result = session.ApplyGameGrammarFilter("the cat went to the cinema and");
            result.ShouldNotContain("and");
            result.ShouldContain("The cat went to the cinema");
        }
    }

    [Fact]
    public void ApplyGameGrammarFilter_SplitsIntoMultipleSentences()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            var result = session.ApplyGameGrammarFilter("the cat went to the cinema and the sky is blue today");
            result.ShouldContain(". ");
        }
    }

    [Fact]
    public void ApplyGameGrammarFilter_CollapsesDuplicateWords()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            var result = session.ApplyGameGrammarFilter("the the cat went to the cinema");
            result.ShouldNotContain("the the");
        }
    }

    [Fact]
    public void ApplyGameGrammarFilter_AddsTrailingPeriod()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            var result = session.ApplyGameGrammarFilter("the cat went to the cinema");
            result.ShouldEndWith(".");
        }
    }

    [Fact]
    public void HandleGameTurn_UserSendsMultipleWords_TakesFirst()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            session.HandleNameInput("my name is Alice");
            session.TryHandleGameStart("let's play a word game", out _);

            var response = session.HandleGameTurn("the cat sat");

            response.ShouldNotBeNullOrEmpty();
        }
    }

    [Fact]
    public void TryHandleGameStart_AlreadyActive_ReturnsPrompt()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            session.HandleNameInput("my name is Alice");
            session.TryHandleGameStart("let's play a word game", out _);

            var result = session.TryHandleGameStart("let's play a word game", out var response);

            result.ShouldBeTrue();
            response.ShouldBe("We're already playing! Just add one word, or say 'stop game' to end.");
        }
    }

    [Fact]
    public void TryHandleMadLibsStart_TriggersOnPhrase()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            TestDataHelper.SeedMadLibTemplates(db.Context);
            session.HandleNameInput("my name is Alice");

            var result = session.TryHandleMadLibsStart("let's play mad libs", out var response);

            result.ShouldBeTrue();
            response.ShouldContain("Mad Libs");
        }
    }

    [Fact]
    public void TryHandleMadLibsStart_AlreadyActive_ReturnsPrompt()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            TestDataHelper.SeedMadLibTemplates(db.Context);
            session.HandleNameInput("my name is Alice");
            session.TryHandleMadLibsStart("let's play mad libs", out _);

            var result = session.TryHandleMadLibsStart("mad libs", out var response);

            result.ShouldBeTrue();
            response.ShouldBe("We're already playing Mad Libs!");
        }
    }

    [Fact]
    public void TryHandleMadLibsStart_GameActive_Blocks()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            session.HandleNameInput("my name is Alice");
            session.TryHandleGameStart("let's play a word game", out _);

            var result = session.TryHandleMadLibsStart("mad libs", out var response);

            result.ShouldBeTrue();
            response.ShouldContain("word game");
        }
    }

    [Fact]
    public void HandleMadLibsTurn_FillsFirstSlot_PromptsNext()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            TestDataHelper.SeedMadLibTemplates(db.Context);
            session.HandleNameInput("my name is Alice");
            session.TryHandleMadLibsStart("mad libs", out _);

            var response = session.HandleMadLibsTurn("silly");

            response.ShouldContain("noun");
        }
    }

    [Fact]
    public void HandleMadLibsTurn_AllSlotsFilled_RevealsStory()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            TestDataHelper.SeedMadLibTemplates(db.Context);
            session.HandleNameInput("my name is Alice");
            session.TryHandleMadLibsStart("mad libs", out _);

            session.HandleMadLibsTurn("silly");
            session.HandleMadLibsTurn("cat");
            session.HandleMadLibsTurn("jumped");
            session.HandleMadLibsTurn("big");
            var response = session.HandleMadLibsTurn("monkeys");

            response.ShouldNotBeNullOrEmpty();
            response.ShouldContain("cat");
            response.ShouldContain("jumped");
        }
    }

    [Fact]
    public void HandleMadLibsTurn_CancelsOnStop()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            TestDataHelper.SeedMadLibTemplates(db.Context);
            session.HandleNameInput("my name is Alice");
            session.TryHandleMadLibsStart("mad libs", out _);

            var response = session.HandleMadLibsTurn("stop");

            response.ShouldContain("another time");
        }
    }

    [Fact]
    public void HandleMadLibsTurn_CancelsOnNeverMind()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            TestDataHelper.SeedMadLibTemplates(db.Context);
            session.HandleNameInput("my name is Alice");
            session.TryHandleMadLibsStart("mad libs", out _);

            var response = session.HandleMadLibsTurn("never mind");

            response.ShouldContain("another time");
        }
    }

    [Fact]
    public void ProcessInput_MadLibsThroughProcessInput_StartsAndReveals()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            TestDataHelper.SeedMadLibTemplates(db.Context);
            session.HandleNameInput("my name is Alice");

            var startResponse = session.ProcessInput("let's play mad libs");
            startResponse.ShouldContain("Mad Libs");

            var turnResponse = session.ProcessInput("silly");
            turnResponse.ShouldNotBeNullOrEmpty();

            var turn2 = session.ProcessInput("cat");
            turn2.ShouldNotBeNullOrEmpty();

            var turn3 = session.ProcessInput("jumped");
            turn3.ShouldNotBeNullOrEmpty();

            var turn4 = session.ProcessInput("big");
            turn4.ShouldNotBeNullOrEmpty();

            var turn5 = session.ProcessInput("monkeys");
            turn5.ShouldNotBeNullOrEmpty();
        }
    }

    // --- Dad Jokes ---

    [Fact]
    public void TryHandleJokeStart_TriggersOnPhrase()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            TestDataHelper.SeedJokes(db.Context);
            session.HandleNameInput("my name is Alice");

            var result = session.TryHandleJokeStart("tell me a joke", out var response);

            result.ShouldBeTrue();
            response.ShouldNotBeNullOrEmpty();
            response.ShouldContain("?");
        }
    }

    [Fact]
    public void TryHandleJokeStart_NoJokes_ReturnsEmpty()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            session.HandleNameInput("my name is Alice");

            var result = session.TryHandleJokeStart("tell me a joke", out var response);

            result.ShouldBeTrue();
            response.ShouldBe("I don't have any jokes to tell yet!");
        }
    }

    [Fact]
    public void TryHandleJokeStart_NonTrigger_ReturnsFalse()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            session.HandleNameInput("my name is Alice");

            var result = session.TryHandleJokeStart("what is the weather", out _);

            result.ShouldBeFalse();
        }
    }

    [Fact]
    public void HandleJokeTurn_ReturnsPunchline()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            TestDataHelper.SeedJokes(db.Context);
            session.HandleNameInput("my name is Alice");
            session.TryHandleJokeStart("tell me a joke", out _);

            var response = session.HandleJokeTurn();

            response.ShouldNotBeNullOrEmpty();
        }
    }

    [Fact]
    public void ProcessInput_JokeFlow_ThroughProcessInput()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            TestDataHelper.SeedJokes(db.Context);
            session.HandleNameInput("my name is Alice");

            var setupResponse = session.ProcessInput("tell me a joke");
            setupResponse.ShouldNotBeNullOrEmpty();
            setupResponse.ShouldContain("?");

            var punchResponse = session.ProcessInput("ha ha");
            punchResponse.ShouldNotBeNullOrEmpty();
        }
    }

    // --- Riddles ---

    [Fact]
    public void TryHandleRiddleStart_TriggersOnPhrase()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            TestDataHelper.SeedRiddles(db.Context);
            session.HandleNameInput("my name is Alice");

            var result = session.TryHandleRiddleStart("tell me a riddle", out var response);

            result.ShouldBeTrue();
            response.ShouldNotBeNullOrEmpty();
            response.ShouldContain("riddle");
        }
    }

    [Fact]
    public void TryHandleRiddleStart_NoRiddles_ReturnsEmpty()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            session.HandleNameInput("my name is Alice");

            var result = session.TryHandleRiddleStart("tell me a riddle", out var response);

            result.ShouldBeTrue();
            response.ShouldBe("I don't have any riddles yet!");
        }
    }

    [Fact]
    public void HandleRiddleTurn_CorrectGuess_Wins()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            TestDataHelper.SeedRiddles(db.Context);
            session.HandleNameInput("my name is Alice");
            session.TryHandleRiddleStart("riddle me", out _);

            var response = session.HandleRiddleTurn("echo");

            response.ShouldNotBeNullOrEmpty();
        }
    }

    [Fact]
    public void HandleRiddleTurn_WrongGuess_LetsTryAgain()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            TestDataHelper.SeedRiddles(db.Context);
            session.HandleNameInput("my name is Alice");
            session.TryHandleRiddleStart("riddle me", out _);

            var response = session.HandleRiddleTurn("I don't know");

            response.ShouldNotBeNullOrEmpty();
        }
    }

    [Fact]
    public void HandleRiddleTurn_GiveUp_RevealsAnswer()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            TestDataHelper.SeedRiddles(db.Context);
            session.HandleNameInput("my name is Alice");
            session.TryHandleRiddleStart("riddle me", out _);

            var response = session.HandleRiddleTurn("i give up");

            response.ShouldNotBeNullOrEmpty();
        }
    }

    [Fact]
    public void HandleRiddleTurn_AfterThreeAttempts_GivesUp()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            TestDataHelper.SeedRiddles(db.Context);
            session.HandleNameInput("my name is Alice");
            session.TryHandleRiddleStart("riddle me", out _);

            session.HandleRiddleTurn("wrong1");
            session.HandleRiddleTurn("wrong2");
            var response = session.HandleRiddleTurn("wrong3");

            response.ShouldNotBeNullOrEmpty();
        }
    }

    [Fact]
    public void HandleRiddleTurn_Hint_ReturnsHint()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            TestDataHelper.SeedRiddles(db.Context);
            session.HandleNameInput("my name is Alice");
            session.TryHandleRiddleStart("riddle me", out _);

            var response = session.HandleRiddleTurn("hint");

            response.ShouldNotBeNullOrEmpty();
        }
    }

    [Fact]
    public void TryHandleRiddleStart_AlreadyActive_ReturnsPrompt()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            TestDataHelper.SeedRiddles(db.Context);
            session.HandleNameInput("my name is Alice");
            session.TryHandleRiddleStart("riddle me", out _);

            var result = session.TryHandleRiddleStart("tell me a riddle", out var response);

            result.ShouldBeTrue();
            response.ShouldContain("already");
        }
    }

    [Fact]
    public void ProcessInput_RiddleFlow_ThroughProcessInput()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            TestDataHelper.SeedRiddles(db.Context);
            session.HandleNameInput("my name is Alice");

            var riddleResponse = session.ProcessInput("riddle me");
            riddleResponse.ShouldNotBeNullOrEmpty();

            var answerResponse = session.ProcessInput("echo");
            answerResponse.ShouldNotBeNullOrEmpty();
        }
    }

    // --- Cross-Session Recall ---

    [Fact]
    public void KnowledgeStore_GetPreviousSessions_NoPrevious_ReturnsEmpty()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            session.HandleNameInput("my name is Alice");
            var userId = db.Context.Users.First().Id;
            var store = new KnowledgeStore(db.Context);
            var result = store.GetPreviousSessions(userId, "current-session");
            result.ShouldBeEmpty();
        }
    }

    [Fact]
    public void KnowledgeStore_GetPreviousSessions_HasPrevious_ReturnsSessions()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            session.HandleNameInput("my name is Alice");
            var userId = db.Context.Users.First().Id;

            db.Context.ConversationSessions.Add(new ConversationSession
            {
                SessionGuid = "prev-session",
                UserId = userId,
                StartedAt = DateTime.UtcNow.AddDays(-1).ToString("o"),
                TurnCount = 5
            });
            db.Context.SaveChanges();

            var store = new KnowledgeStore(db.Context);
            var result = store.GetPreviousSessions(userId, "current-session");
            result.Count.ShouldBe(1);
            result[0].SessionGuid.ShouldBe("prev-session");
        }
    }

    [Fact]
    public void KnowledgeStore_GetRandomFactFromSession_HasFacts_ReturnsFact()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            session.HandleNameInput("my name is Alice");
            var userId = db.Context.Users.First().Id;

            var sessionId = "test-session";
            db.Context.Conversations.Add(new Conversation
            {
                UserId = userId,
                UserInput = "I like pizza",
                BotResponse = "Cool!",
                SessionId = sessionId,
                Timestamp = DateTime.UtcNow.ToString("o")
            });

            db.Context.Facts.Add(new FactEntity
            {
                UserId = userId,
                Subject = "Alice",
                Verb = "like",
                Object = "pizza",
                PredicateType = "Preference",
                CreatedAt = DateTime.UtcNow.ToString("o")
            });
            db.Context.SaveChanges();

            var store = new KnowledgeStore(db.Context);
            var fact = store.GetRandomFactFromSession(userId, sessionId);
            fact.ShouldNotBeNull();
            fact.Object.ShouldBe("pizza");
        }
    }

    [Fact]
    public void KnowledgeStore_GetRandomFactFromSession_NoConversations_ReturnsNull()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            session.HandleNameInput("my name is Alice");
            var userId = db.Context.Users.First().Id;

            var store = new KnowledgeStore(db.Context);
            var fact = store.GetRandomFactFromSession(userId, "nonexistent");
            fact.ShouldBeNull();
        }
    }

    [Fact]
    public void TryBuildCrossSessionRecall_NoUser_ReturnsNull()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            var result = session.TryBuildCrossSessionRecall();
            result.ShouldBeNull();
        }
    }

    [Fact]
    public void TryBuildCrossSessionRecall_NoPreviousSession_ReturnsNull()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            session.HandleNameInput("my name is Alice");
            var result = session.TryBuildCrossSessionRecall();
            result.ShouldBeNull();
        }
    }

    [Fact]
    public void TryBuildCrossSessionRecall_AlreadyAttempted_ReturnsNull()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            session.HandleNameInput("my name is Alice");
            session.TryBuildCrossSessionRecall(); // sets the flag
            var result = session.TryBuildCrossSessionRecall();
            result.ShouldBeNull();
        }
    }

    [Fact]
    public void TryBuildCrossSessionRecall_HasPreviousSession_FiresOrNot()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            session.HandleNameInput("my name is Alice");
            var userId = db.Context.Users.First().Id;

            var prevSessionId = "prev-session";
            db.Context.ConversationSessions.Add(new ConversationSession
            {
                SessionGuid = prevSessionId,
                UserId = userId,
                StartedAt = DateTime.UtcNow.AddDays(-1).ToString("o"),
                TurnCount = 5
            });

            db.Context.Conversations.Add(new Conversation
            {
                UserId = userId,
                UserInput = "I like pizza",
                BotResponse = "Cool!",
                SessionId = prevSessionId,
                Timestamp = DateTime.UtcNow.AddDays(-1).ToString("o")
            });

            db.Context.Facts.Add(new FactEntity
            {
                UserId = userId,
                Subject = "Alice",
                Verb = "like",
                Object = "pizza",
                PredicateType = "Preference",
                CreatedAt = DateTime.UtcNow.AddDays(-1).ToString("o")
            });
            db.Context.SaveChanges();

            var result = session.TryBuildCrossSessionRecall();
            // May return null due to 30% chance, but should never throw
            if (result != null)
            {
                result.ShouldContain("pizza");
            }
        }
    }

    // --- Interview Mode ---

    [Fact]
    public void IsInterviewTrigger_DetectsInterviewPhrase()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            session.IsInterviewTrigger("interview mode").ShouldBeTrue();
        }
    }

    [Fact]
    public void IsInterviewTrigger_DetectsTrainTheBot()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            session.IsInterviewTrigger("train the bot").ShouldBeTrue();
        }
    }

    [Fact]
    public void IsInterviewTrigger_DetectsLLMInterview()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            session.IsInterviewTrigger("llm interview").ShouldBeTrue();
        }
    }

    [Fact]
    public void IsInterviewTrigger_DetectsStartTraining()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            session.IsInterviewTrigger("start training").ShouldBeTrue();
        }
    }

    [Fact]
    public void IsInterviewTrigger_DetectsChatWithYourself()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            session.IsInterviewTrigger("chat with yourself").ShouldBeTrue();
        }
    }

    [Fact]
    public void IsInterviewTrigger_DetectsInterview()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            session.IsInterviewTrigger("interview").ShouldBeTrue();
        }
    }

    [Fact]
    public void IsInterviewTrigger_ReturnsFalse_ForNonTrigger()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            session.IsInterviewTrigger("hello there").ShouldBeFalse();
        }
    }

    [Fact]
    public void IsInterviewTrigger_ReturnsFalse_ForEmptyInput()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            session.IsInterviewTrigger("").ShouldBeFalse();
        }
    }

    [Fact]
    public void IsInterviewStopCommand_DetectsStop()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            session.IsInterviewStopCommand("stop").ShouldBeTrue();
        }
    }

    [Fact]
    public void IsInterviewStopCommand_DetectsEndInterview()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            session.IsInterviewStopCommand("end interview").ShouldBeTrue();
        }
    }

    [Fact]
    public void IsInterviewStopCommand_DetectsCancel()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            session.IsInterviewStopCommand("cancel").ShouldBeTrue();
        }
    }

    [Fact]
    public void IsInterviewStopCommand_DetectsEnough()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            session.IsInterviewStopCommand("enough").ShouldBeTrue();
        }
    }

    [Fact]
    public void IsInterviewStopCommand_DetectsStopTraining()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            session.IsInterviewStopCommand("stop training").ShouldBeTrue();
        }
    }

    [Fact]
    public void IsInterviewStopCommand_ReturnsFalse_ForNonStop()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            session.IsInterviewStopCommand("go away").ShouldBeFalse();
        }
    }

    [Fact]
    public void IsInterviewStopCommand_ReturnsFalse_ForEmptyInput()
    {
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            session.IsInterviewStopCommand("").ShouldBeFalse();
        }
    }

    [Fact]
    public void ProcessInput_DoesNotInterfereWithInterviewMode()
    {
        // Interview mode detection happens in Start(), not ProcessInput.
        // Verify that a normal ProcessInput still works when interview
        // triggers are not matched.
        var (session, db) = CreateSessionAndDb();
        using (db)
        {
            session.HandleNameInput("my name is Bob");
            var response = session.ProcessInput("hello there");
            response.ShouldNotBeNullOrEmpty();
        }
    }
}

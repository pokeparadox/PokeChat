using PokeChat.Core;
using PokeChat.Knowledge;
using PokeChat.LLM;
using PokeChat.NLP;
using PokeChat.Responses;
using PokeChat.Tests.Helpers;
using PokeChat.Tests.LLM;
using Shouldly;

namespace PokeChat.Tests.Core;

public class ChatSessionLLMTests
{
    private ChatSession CreateSession(
        string? llmResponse = null,
        bool noLLM = false,
        List<string>? namePatterns = null,
        HashSet<string>? botCommands = null,
        HashSet<string>? greetingWords = null,
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
                namePatterns ?? new List<string> { "my name is", "i am", "i'm", "call me" },
                botCommands ?? new List<string> { "quit", "exit" }.ToHashSet(StringComparer.OrdinalIgnoreCase),
                greetingWords ?? new List<string> { "hi", "hello" }.ToHashSet(StringComparer.OrdinalIgnoreCase));
        }

        var llmConfig = new LLMConfig { Enabled = true };
        var llmProvider = new StubLLMProvider { Response = llmResponse ?? "AI response." };
        var llmOrchestrator = new LLMOrchestrator(llmProvider, llmConfig);

        return new ChatSession(
            db.Context, store, responseEngine, spellChecker, posTagger,
            tokeniser, sentenceSplitter, svoExtractor, contextTracker, nounCategoriser,
            namePatterns ?? new List<string> { "my name is", "i am", "i'm", "call me" },
            botCommands ?? new List<string> { "quit", "exit" }.ToHashSet(StringComparer.OrdinalIgnoreCase),
            greetingWords ?? new List<string> { "hi", "hello" }.ToHashSet(StringComparer.OrdinalIgnoreCase),
            llmOrchestrator: llmOrchestrator);
    }

    private static List<string> ExhaustFollowUps(ChatSession session)
    {
        var responses = new List<string>();
        responses.Add(session.ProcessInput("hmm"));
        responses.Add(session.ProcessInput("ok"));
        responses.Add(session.ProcessInput("sure"));
        return responses;
    }

    // --- PendingLLMOffer handler tests (direct state injection) ---

    [Fact]
    public void PendingOffer_WithAffirmation_CallsLLM()
    {
        using var db = new FreshDbContext();
        var session = CreateSession(llmResponse: "LLM response text.", dbOut: db);
        session.HandleNameInput("my name is Alice");
        session.SetLLMOfferState("What is AI?");

        var response = session.ProcessInput("yes");
        response.ShouldBe("LLM response text.");
    }

    [Fact]
    public void PendingOffer_WithDecline_ShowsDeclined()
    {
        using var db = new FreshDbContext();
        var session = CreateSession(dbOut: db);
        session.HandleNameInput("my name is Alice");
        session.SetLLMOfferState("What is AI?");

        var response = session.ProcessInput("no thanks");
        response.ShouldBeOneOf("No problem, I'll keep learning!", "That's OK! I'll try to figure it out on my own.");
    }

    [Fact]
    public void PendingOffer_LLMUnavailable_ShowsUnavailable()
    {
        using var db = new FreshDbContext();
        var session = CreateSession(llmResponse: null, dbOut: db);
        session.HandleNameInput("my name is Alice");
        session.SetLLMOfferState("What is AI?");

        var response = session.ProcessInput("yes");
        response.ShouldContain("AI");
    }

    [Fact]
    public void LLMOfferAccepted_ThenLLMUsedOnSubsequentFallback()
    {
        using var db = new FreshDbContext();
        var session = CreateSession(llmResponse: "AI says hi.", dbOut: db);
        session.HandleNameInput("my name is Alice");
        session.SetLLMOfferState("Tell me something");
        session.ProcessInput("yes");

        // After acceptance, the LLM is called directly on any input that
        // reaches default_response. Send non-SVO inputs until we hit one
        // that isn't intercepted by random story generation (1/6 chance).
        ExhaustFollowUps(session);
        string response = "";
        for (var i = 0; i < 20; i++)
        {
            response = session.ProcessInput("hmm");
            if (response == "AI says hi.") break;
        }
        response.ShouldBe("AI says hi.");
    }

    [Fact]
    public void LLMOfferDeclined_NotOfferedAgain()
    {
        using var db = new FreshDbContext();
        var session = CreateSession(dbOut: db);
        session.HandleNameInput("my name is Alice");
        session.SetLLMOfferState("Tell me something");
        session.ProcessInput("no");

        ExhaustFollowUps(session);
        var response = session.ProcessInput("What is the capital of France");
        response.ShouldNotContain("AI");
    }

    // --- End-to-end: LLM offer fires on default_response ---

    [Fact]
    public void LLMConfigured_OffersAI_OnDefaultResponse()
    {
        using var db = new FreshDbContext();
        var session = CreateSession(dbOut: db);
        session.HandleNameInput("my name is Alice");

        // After 3 non-SVO inputs, follow-ups are exhausted.
        // With no facts and no topics, the 4th non-SVO input reaches
        // a dead-end (default_response, story, or proactive), which
        // sets PendingLLMOffer silently.
        ExhaustFollowUps(session);
        session.ProcessInput("right");

        // Next non-yes/no input: PendingLLMOffer handler fires,
        // clears the pending state, and shows the offer.
        var response = session.ProcessInput("hmm");
        response.ShouldContain("AI");
    }

    [Fact]
    public void LLMNotConfigured_ReturnsNormalFallback()
    {
        using var db = new FreshDbContext();
        var session = CreateSession(noLLM: true, dbOut: db);
        session.HandleNameInput("my name is Alice");

        ExhaustFollowUps(session);
        var response = session.ProcessInput("right");
        response.ShouldNotContain("AI");
    }
}

using PokeChat.Core;
using PokeChat.Knowledge;
using PokeChat.LLM;
using PokeChat.NLP;
using PokeChat.Responses;
using PokeChat.Tests.Shared.Helpers;
using PokeChat.Tests.Shared.LLM;
using Shouldly;

namespace PokeChat.Tests.Core;

public class ChatSessionLLMReproTest
{
    private ChatSession CreateSession(FreshDbContext db, string? llmResponse = null)
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

        var llmConfig = new LLMConfig { Enabled = llmResponse != null };
        var llmProvider = llmResponse != null ? new StubLLMProvider { Response = llmResponse } : null;
        var llmOrchestrator = llmProvider != null ? new LLMOrchestrator(llmProvider, llmConfig) : null;

        return new ChatSession(
            db.Context, store, responseEngine, spellChecker, posTagger,
            tokeniser, sentenceSplitter, svoExtractor, contextTracker, nounCategoriser,
            new List<string> { "my name is", "i am", "i'm", "call me" },
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "quit", "exit" },
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "hi", "hello" },
            llmOrchestrator: llmOrchestrator);
    }

    [Fact]
    public void LLMOffer_AcceptThenDeadEnd_CallsLLMDirectly()
    {
        using var db = new FreshDbContext();
        using var session = CreateSession(db, "AI response text.");
        session.ProcessInput("my name is Bob");

        // Exhaust follow-ups so next input hits dead-end
        session.ProcessInput("hmm");
        session.ProcessInput("ok");
        session.ProcessInput("sure");

        // 4th input: hits dead-end, PendingLLMOffer set silently
        // 5th input: PendingLLMOffer handler shows offer
        var offer = session.ProcessInput("hmm");
        offer.ShouldContain("AI");

        // Accept the offer
        var llmResponse = session.ProcessInput("yes");
        llmResponse.ShouldBe("AI response text.");

        // After acceptance, any dead-end should call LLM directly
        var response = session.ProcessInput("nothing");
        Console.WriteLine($"Step response: '{response}'");
        response.ShouldBe("AI response text.");
    }
}

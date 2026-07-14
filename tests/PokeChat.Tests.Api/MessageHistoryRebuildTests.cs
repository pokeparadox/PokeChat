using Microsoft.EntityFrameworkCore;
using PokeChat.Api.Models;
using PokeChat.Api.Services;
using PokeChat.Core;
using PokeChat.Data;
using PokeChat.Knowledge;
using PokeChat.Math;
using PokeChat.NLP;
using PokeChat.Responses;
using PokeChat.Tests.Shared.Helpers;
using Shouldly;

namespace PokeChat.Tests.Api;

public class MessageHistoryRebuildTests : IDisposable
{
    private readonly FreshDbContext _db;

    public MessageHistoryRebuildTests()
    {
        _db = new FreshDbContext();
        DbSeeder.Seed(_db.Context);
    }

    public void Dispose() => _db.Dispose();

    private ChatEngine CreateEngine(string persona = "chat")
    {
        var knowledgeStore = new KnowledgeStore(_db.Context);
        var context = new ContextTracker();
        var spellChecker = new SpellChecker();
        var posEntries = knowledgeStore.GetPosDictionary();
        var contractions = knowledgeStore.GetContractions().ToDictionary(c => c.Contraction, c => c.Expansion);
        var expander = new ContractionExpander(contractions);
        var tokeniser = new Tokeniser(expander);
        var sentenceSplitter = new SentenceSplitter();
        var svoExtractor = new SvoExtractor();
        var posTagger = new PosTagger(posEntries);
        var nounCategoriser = new NounCategoriser(knowledgeStore);
        var namePatterns = knowledgeStore.GetNamePatterns().Select(p => p.Pattern.ToLowerInvariant()).ToList();
        var botCommands = knowledgeStore.GetBotCommands().Select(c => c.Command).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var greetingWords = knowledgeStore.GetGreetingWords().Select(gw => gw.Word.ToLowerInvariant()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var renamePatterns = knowledgeStore.GetBotRenamePatterns();
        var responseEngine = new ResponseEngine(knowledgeStore, context, spellChecker, posTagger, tokeniser, svoExtractor, timeEngine: new SystemTimeEngine());
        var spellDict = new HashSet<string>(posEntries.Select(e => e.Word), StringComparer.OrdinalIgnoreCase);
        var misspellings = knowledgeStore.GetMisspellings();
        spellChecker.Initialise(spellDict, misspellings);

        return new ChatEngine(_db.Context, knowledgeStore, responseEngine, spellChecker, posTagger, tokeniser, sentenceSplitter, svoExtractor, context, nounCategoriser, namePatterns, botCommands, greetingWords, renamePatterns: renamePatterns, persona: persona);
    }

    [Fact]
    public void RebuildHistory_SingleUserMessage_SkipsRebuild()
    {
        using var engine = CreateEngine();
        engine.EstablishDefaultUser("Alice");

        var messages = new List<ChatMessage>
        {
            new() { Role = "user", Content = "Hello" }
        };

        OpenAIAdapter.RebuildHistory(engine, messages);

        engine.GetContextValue(ContextKeys.LastProcessedHistoryHash).ShouldBeNull();
    }

    [Fact]
    public void RebuildHistory_NoUserMessages_SkipsRebuild()
    {
        using var engine = CreateEngine();
        engine.EstablishDefaultUser("Alice");

        var messages = new List<ChatMessage>
        {
            new() { Role = "system", Content = "You are helpful." },
            new() { Role = "assistant", Content = "Hi there!" }
        };

        OpenAIAdapter.RebuildHistory(engine, messages);

        engine.GetContextValue(ContextKeys.LastProcessedHistoryHash).ShouldBeNull();
    }

    [Fact]
    public void RebuildHistory_TwoUserMessages_ProcessesFirst()
    {
        using var engine = CreateEngine();
        engine.EstablishDefaultUser("Alice");

        var messages = new List<ChatMessage>
        {
            new() { Role = "user", Content = "my name is Bob" },
            new() { Role = "assistant", Content = "Nice to meet you, Bob!" },
            new() { Role = "user", Content = "what is my name" }
        };

        OpenAIAdapter.RebuildHistory(engine, messages);

        engine.GetContextValue(ContextKeys.LastProcessedHistoryHash).ShouldNotBeNull();
    }

    [Fact]
    public void RebuildHistory_SkipsSystemAndToolMessages()
    {
        using var engine = CreateEngine();
        engine.EstablishDefaultUser("Alice");

        var messages = new List<ChatMessage>
        {
            new() { Role = "system", Content = "You are a coding assistant." },
            new() { Role = "user", Content = "my name is Charlie" },
            new() { Role = "assistant", Content = "Got it, Charlie!" },
            new() { Role = "tool", Content = "some tool result" },
            new() { Role = "user", Content = "remember that" }
        };

        OpenAIAdapter.RebuildHistory(engine, messages);

        engine.GetContextValue(ContextKeys.LastProcessedHistoryHash).ShouldNotBeNull();
    }

    [Fact]
    public void RebuildHistory_EmptyMessages_SkipsRebuild()
    {
        using var engine = CreateEngine();
        engine.EstablishDefaultUser("Alice");

        var messages = new List<ChatMessage>();

        OpenAIAdapter.RebuildHistory(engine, messages);

        engine.GetContextValue(ContextKeys.LastProcessedHistoryHash).ShouldBeNull();
    }

    [Fact]
    public void RebuildHistory_Dedup_SameHashSkipsSecondCall()
    {
        using var engine = CreateEngine();
        engine.EstablishDefaultUser("Alice");

        var messages = new List<ChatMessage>
        {
            new() { Role = "user", Content = "my name is Dave" },
            new() { Role = "assistant", Content = "Hello Dave!" },
            new() { Role = "user", Content = "what is 2+2" }
        };

        OpenAIAdapter.RebuildHistory(engine, messages);
        var firstHash = engine.GetContextValue(ContextKeys.LastProcessedHistoryHash);

        OpenAIAdapter.RebuildHistory(engine, messages);
        var secondHash = engine.GetContextValue(ContextKeys.LastProcessedHistoryHash);

        firstHash.ShouldBe(secondHash);
    }

    [Fact]
    public void RebuildHistory_DifferentHash_RebuildsOnSecondCall()
    {
        using var engine = CreateEngine();
        engine.EstablishDefaultUser("Alice");

        var messages1 = new List<ChatMessage>
        {
            new() { Role = "user", Content = "my name is Eve" },
            new() { Role = "assistant", Content = "Hi Eve!" },
            new() { Role = "user", Content = "hello" }
        };

        OpenAIAdapter.RebuildHistory(engine, messages1);
        var hash1 = engine.GetContextValue(ContextKeys.LastProcessedHistoryHash);

        var messages2 = new List<ChatMessage>
        {
            new() { Role = "user", Content = "my name is Eve" },
            new() { Role = "assistant", Content = "Hi Eve!" },
            new() { Role = "user", Content = "I like pizza" },
            new() { Role = "assistant", Content = "Nice!" },
            new() { Role = "user", Content = "what do I like" }
        };

        OpenAIAdapter.RebuildHistory(engine, messages2);
        var hash2 = engine.GetContextValue(ContextKeys.LastProcessedHistoryHash);

        hash1.ShouldNotBe(hash2);
    }

    [Fact]
    public void RebuildHistory_OnlyUserMessagesCounted()
    {
        using var engine = CreateEngine();
        engine.EstablishDefaultUser("Alice");

        var messages = new List<ChatMessage>
        {
            new() { Role = "system", Content = "Be helpful." },
            new() { Role = "user", Content = "my name is Frank" },
            new() { Role = "assistant", Content = "Nice to meet you, Frank!" },
            new() { Role = "user", Content = "what is my name" }
        };

        OpenAIAdapter.RebuildHistory(engine, messages);

        var lastUserInput = engine.GetContextValue(ContextKeys.LastUserInput);
        lastUserInput.ShouldNotBeNull();
    }

    [Fact]
    public void RebuildHistory_WhitespaceContent_Skipped()
    {
        using var engine = CreateEngine();
        engine.EstablishDefaultUser("Alice");

        var messages = new List<ChatMessage>
        {
            new() { Role = "user", Content = "   " },
            new() { Role = "assistant", Content = "Hmm?" },
            new() { Role = "user", Content = "hello" }
        };

        OpenAIAdapter.RebuildHistory(engine, messages);

        engine.GetContextValue(ContextKeys.LastProcessedHistoryHash).ShouldNotBeNull();
    }

    [Fact]
    public void RebuildHistory_RebuildMode_PreventsStorage()
    {
        using var engine = CreateEngine();
        engine.EstablishDefaultUser("Alice");

        var messages = new List<ChatMessage>
        {
            new() { Role = "user", Content = "my name is Grace" },
            new() { Role = "assistant", Content = "Hi Grace!" },
            new() { Role = "user", Content = "what is my name" }
        };

        OpenAIAdapter.RebuildHistory(engine, messages);

        engine.RebuildMode.ShouldBeFalse();
    }
}

using PokeChat.Core;
using PokeChat.Knowledge;
using PokeChat.NLP;
using PokeChat.Responses;
using PokeChat.Tests.Helpers;
using Shouldly;

var db = new FreshDbContext();
TestDataHelper.SeedBotResponses(db.Context);
TestDataHelper.SeedPosDictionary(db.Context);
TestDataHelper.SeedMadLibTemplates(db.Context);

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
    new List<string> { "my name is", "i am", "i'm", "call me" },
    new List<string> { "quit", "exit" }.ToHashSet(StringComparer.OrdinalIgnoreCase),
    new List<string> { "hi", "hello" }.ToHashSet(StringComparer.OrdinalIgnoreCase)
);

session.HandleNameInput("my name is Alice");

var result = session.TryHandleMadLibsStart("let's play mad libs", out var startResponse);
Console.WriteLine($"Start result: {result}, Response: {startResponse}");

// Check template ID was stored
var templateId = contextTracker.GetContext("MadLibsTemplateId");
Console.WriteLine($"TemplateId context: '{templateId}'");
var active = contextTracker.GetContext("MadLibsActive");
Console.WriteLine($"Active context: '{active}'");

// First turn
var turn1 = session.HandleMadLibsTurn("silly");
Console.WriteLine($"Turn 1: {turn1}");
templateId = contextTracker.GetContext("MadLibsTemplateId");
Console.WriteLine($"TemplateId after turn 1: '{templateId}'");

var turn2 = session.HandleMadLibsTurn("cat");
Console.WriteLine($"Turn 2: {turn2}");
templateId = contextTracker.GetContext("MadLibsTemplateId");
Console.WriteLine($"TemplateId after turn 2: '{templateId}'");

var turn3 = session.HandleMadLibsTurn("jumped");
Console.WriteLine($"Turn 3: {turn3}");

var turn4 = session.HandleMadLibsTurn("big");
Console.WriteLine($"Turn 4: {turn4}");

var turn5 = session.HandleMadLibsTurn("monkeys");
Console.WriteLine($"Turn 5: {turn5}");

db.Dispose();

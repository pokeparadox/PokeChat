using System.Text.Json;
using System.Text.RegularExpressions;
using PokeChat.Data;
using PokeChat.Knowledge;
using PokeChat.LLM;
using PokeChat.Math;
using PokeChat.Mcp;
using PokeChat.NLP;
using PokeChat.Responses;
using PokeChat.Tools;

namespace PokeChat.Core;

public class ChatEngine : IDisposable
{
    public Action<string>? OnStatusUpdate { get; set; }
    private readonly PokeChatDbContext _dbContext;
    private readonly KnowledgeStore _knowledgeStore;
    private readonly ResponseEngine _responseEngine;
    private readonly SpellChecker _spellChecker;
    private readonly IPosTagger _posTagger;
    private readonly ITokeniser _tokeniser;
    private readonly ISentenceSplitter _sentenceSplitter;
    private readonly ISvoExtractor _svoExtractor;
    private readonly ContextTracker _context;
    private readonly INounCategoriser _nounCategoriser;
    private int? _currentUserId;
    private string _currentUserName = string.Empty;
    private readonly List<string> _namePatterns;
    private readonly HashSet<string> _botCommands;
    private readonly HashSet<string> _greetingWords;
    private string _botName = "PokeChat";
    private readonly List<string> _renamePatterns;
    private string _sessionId = Guid.NewGuid().ToString();
    private string _currentUserNameLower = string.Empty;
    private Dictionary<string, List<string>>? _cachedBotResponses;
    private static readonly string[] AlternativeNames = { "Zara", "Nova", "Echo", "Pixel", "Azure", "Kai", "Rex" };

    private readonly SessionLogger? _sessionLogger;
    private readonly McpRegistry? _mcpRegistry;
    private readonly LLMOrchestrator? _llmOrchestrator;
    private IInterviewEngine? _interviewEngine;
    private int? _savedUserId;
    private bool _interviewModeActive;
    private string? _lastInterviewQuestion;
    private string? _pendingFollowUp;
    private int _followUpCount;
    private readonly ML.IntentClassifier _intentClassifier;
    private readonly RouterService _router = new();
    private readonly List<(string Input, string Response)> _trainingBuffer = new();
    private const int RetrainThreshold = 25;
    private string _persona = "chat";

    private static readonly HashSet<string> PersonaTriggers = new(StringComparer.OrdinalIgnoreCase)
    {
        "switch to coding mode", "switch to chat mode", "enter coding mode", "enter chat mode",
        "go to coding mode", "go to chat mode", "change to coding mode", "change to chat mode",
        "activate coding mode", "activate chat mode", "coding mode", "chat mode",
    };

    private static readonly Regex InsultPattern = new(
        @"^(?:you(?:'re| are) (?:a|an) \w+|shut\s+up|shut\s+it)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex FileMentionPattern = new(
        @"\b[\w/\\-]+\.(cs|json|csproj|slnx|md|txt|py|js|ts|xml|yaml|yml|sql|css|html)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex DestructiveCommandPattern = new(
        @"\b(push\b(?!.*--force)|deploy|publish|drop\s+\w+|rm\s+-rf|destroy|remove\s+\w+|delete\s+\w+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly HashSet<string> Affirmations = new(StringComparer.OrdinalIgnoreCase)
        { "yes", "yep", "yeah", "yup", "sure", "correct", "right",
          "that's right", "that is right", "yes please", "ok", "okay" };

    private static readonly HashSet<string> Denials = new(StringComparer.OrdinalIgnoreCase)
        { "no", "nope", "nah", "no thanks", "no thank you" };

    private static readonly HashSet<string> MetaConfusionTriggers = new(StringComparer.OrdinalIgnoreCase)
    {
        "doesn't make sense", "does not make sense", "makes no sense",
        "don't understand", "do not understand",
        "i'm confused", "i am confused",
        "you're confusing", "you're not making sense",
        "that was confusing", "that is confusing",
        "confusing me",
    };

    private static readonly HashSet<string> MetaNotHelpfulTriggers = new(StringComparer.OrdinalIgnoreCase)
    {
        "not helpful", "not helping",
        "bad answer", "wrong answer",
        "that doesn't help", "that does not help",
    };

    private static readonly HashSet<string> MetaMockingTriggers = new(StringComparer.OrdinalIgnoreCase)
    {
        "mocking me", "making fun of me", "laughing at me",
        "stop mocking",
    };

    private static readonly HashSet<string> FunctionWords = new(StringComparer.OrdinalIgnoreCase)
        { "not", "never", "no", "and", "or", "any", "all", "some", "the",
          "a", "an", "this", "that", "these", "those", "it", "its",
          "there", "here", "then", "than", "also", "too", "very",
          "so", "but", "yet", "for", "with", "without", "just",
          "to", "about", "how", "what", "why", "when", "where", "who",
          "of", "in", "on", "at", "by", "from", "as", "into", "onto",
          "sure", "do", "does", "did", "same" };

    private static readonly HashSet<string> NegationWords = new(StringComparer.OrdinalIgnoreCase)
        { "not", "never", "no" };

    private static readonly HashSet<string> ContentWordIndicators = new(StringComparer.OrdinalIgnoreCase)
        { "i", "you", "he", "she", "we", "they", "me", "him", "her", "us", "them",
          "my", "your", "his", "her", "our", "their" };

    private static readonly HashSet<string> CancellationPhrases = new(StringComparer.OrdinalIgnoreCase)
    {
        "typo", "never mind", "nevermind", "forget it", "forget about it",
        "my bad", "that was a mistake", "it was a mistake", "my mistake",
        "don't worry about it", "dont worry about it", "nothing", "ignore it",
        "forget that", "scratch that", "strike that"
    };

    private static readonly HashSet<string> DontKnowClassificationPhrases = new(StringComparer.OrdinalIgnoreCase)
    {
        "i don't know", "i dont know", "i dunno", "dunno",
        "not sure", "no idea", "i have no idea",
        "i'm not sure", "im not sure", "i haven't a clue",
        "i haven't got a clue"
    };

    private static readonly string[] ResetTriggers =
    {
        "start fresh",
        "start afresh",
        "start over",
        "start again",
        "reset everything",
        "reset all data",
        "forget everything",
        "wipe all memories",
        "wipe everything",
        "clear all data",
        "clear everything",
        "clear all memories",
        "fresh start",
        "restart",
        "lets start again",
        "let's start again",
    };

    private static readonly HashSet<string> GameStartPhrases = new(StringComparer.OrdinalIgnoreCase)
    {
        "let's play a word game", "let's tell a story", "word game", "story chain",
        "let's make a story", "play a game"
    };

    private static readonly HashSet<string> GameEndPhrases = new(StringComparer.OrdinalIgnoreCase)
    {
        "stop", "stop game", "end game", "finish", "that's enough", "i'm done"
    };

    private static readonly string[] GameStartWords = { "Once", "The", "A", "There", "I", "It" };

    private static readonly HashSet<string> MadLibsStartPhrases = new(StringComparer.OrdinalIgnoreCase)
    {
        "let's play mad libs", "let's do mad libs", "mad libs", "play mad libs",
        "do a mad lib", "let's make a mad lib"
    };

    private static readonly HashSet<string> JokeStartPhrases = new(StringComparer.OrdinalIgnoreCase)
    {
        "tell me a joke", "make me laugh", "got any jokes", "say something funny",
        "crack a joke", "tell a joke", "tell us a joke", "tell me a funny joke",
        "do a joke", "tell me something funny"
    };

    private static readonly HashSet<string> RiddleStartPhrases = new(StringComparer.OrdinalIgnoreCase)
    {
        "tell me a riddle", "give me a riddle", "i want a riddle", "riddle me",
        "ask me a riddle", "tell us a riddle", "do a riddle", "give us a riddle"
    };

    private static readonly HashSet<string> WyrStartPhrases = new(StringComparer.OrdinalIgnoreCase)
    {
        "would you rather", "wyr", "play would you rather", "would you rather?"
    };

    private static readonly HashSet<string> InterviewStartPhrases = new(StringComparer.OrdinalIgnoreCase)
    {
        "interview mode", "train the bot", "llm interview", "chat with yourself",
        "start training", "interview"
    };

    private static readonly HashSet<string> InterviewStopPhrases = new(StringComparer.OrdinalIgnoreCase)
    {
        "stop", "end interview", "cancel", "enough", "stop training",
        "quit", "exit", "q"
    };

    private static readonly HashSet<string> HangmanStartPhrases = new(StringComparer.OrdinalIgnoreCase)
    {
        "let's play hangman", "play hangman", "hangman", "let's play hang man",
        "play hang man", "hang man", "i want to play hangman", "let's do hangman"
    };

    private const int HangmanMaxAttempts = 6;

    private static readonly HashSet<string> SurrenderPhrases = new(StringComparer.OrdinalIgnoreCase)
    {
        "i give up", "give up", "i surrender", "surrender", "i don't know",
        "i dont know", "no idea", "tell me", "what is it", "what's the answer"
    };

    private static readonly Dictionary<string, string> MadLibSlotLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["noun"] = "a noun",
        ["plural_noun"] = "a plural noun",
        ["verb"] = "a verb",
        ["verb_past"] = "a past tense verb",
        ["verb_ing"] = "an -ing verb",
        ["adjective"] = "an adjective",
        ["adverb"] = "an adverb",
        ["place"] = "a place",
        ["person"] = "a person",
        ["number"] = "a number",
        ["day"] = "a day of the week",
    };

    private static readonly Regex MadLibSlotRegex = new(@"\{(\w+)\}", RegexOptions.Compiled);

    public ChatEngine()
    {
        _dbContext = new PokeChatDbContext();
        new DatabaseInitializer(_dbContext).Initialize();
        _sessionLogger = new SessionLogger(_sessionId);

        _knowledgeStore = new KnowledgeStore(_dbContext);
        _context = new ContextTracker();
        _spellChecker = new SpellChecker();

        var contractions = _knowledgeStore.GetContractions();
        var contractionMap = contractions.ToDictionary(c => c.Contraction, c => c.Expansion);
        var expander = new ContractionExpander(contractionMap);
        _tokeniser = new Tokeniser(expander);

        _sentenceSplitter = new SentenceSplitter();
        _svoExtractor = new SvoExtractor();
        var posEntries = _knowledgeStore.GetPosDictionary();
        _posTagger = new PosTagger(posEntries);
        _nounCategoriser = new NounCategoriser(_knowledgeStore);
        _mcpRegistry = new McpRegistry();
        var toolRegistry = new ToolRegistry(mcpRegistry: _mcpRegistry);
        var toolTriggers = _mcpRegistry.GetToolTriggers();
        _intentClassifier = new ML.IntentClassifier();
        _intentClassifier.LoadOrCreate(ML.SeedTrainingData.Examples);

        _llmOrchestrator = new LLMOrchestrator();
        var llmGenerator = _llmOrchestrator.Config.AlwaysOn && _llmOrchestrator.IsAvailable
            ? new Func<string, string?>(prompt => _llmOrchestrator.GenerateResponse(prompt))
            : null;
        var enhancedCats = _llmOrchestrator.Config.EnhancedCategories.Count > 0
            ? new HashSet<string>(_llmOrchestrator.Config.EnhancedCategories, StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>();
        _responseEngine = new ResponseEngine(_knowledgeStore, _context, _spellChecker, _posTagger, _tokeniser, _svoExtractor, timeEngine: new SystemTimeEngine(), toolRegistry: toolRegistry, toolTriggers: toolTriggers, llmGenerator: llmGenerator, enhancedCategories: enhancedCats, summariseToolResults: _llmOrchestrator.Config.SummariseToolResults, intentClassifier: _intentClassifier);
        AutoSeedPosDictionary(_mcpRegistry);

        var spellDict = new HashSet<string>(posEntries.Select(e => e.Word), StringComparer.OrdinalIgnoreCase);
        var misspellings = _knowledgeStore.GetMisspellings();
        _spellChecker.Initialise(spellDict, misspellings);

        _namePatterns = _knowledgeStore.GetNamePatterns().Select(p => p.Pattern.ToLowerInvariant()).ToList();
        _botCommands = _knowledgeStore.GetBotCommands().Select(c => c.Command).ToHashSet(StringComparer.OrdinalIgnoreCase);
        _greetingWords = _knowledgeStore.GetGreetingWords().Select(gw => gw.Word.ToLowerInvariant()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        _renamePatterns = _knowledgeStore.GetBotRenamePatterns();
        _responseEngine.SetBotName(_botName);
        _currentUserNameLower = _currentUserName.ToLowerInvariant();
    }

    public ChatEngine(
        PokeChatDbContext dbContext,
        KnowledgeStore knowledgeStore,
        ResponseEngine responseEngine,
        SpellChecker spellChecker,
        IPosTagger posTagger,
        ITokeniser tokeniser,
        ISentenceSplitter sentenceSplitter,
        ISvoExtractor svoExtractor,
        ContextTracker context,
        INounCategoriser nounCategoriser,
        List<string> namePatterns,
        HashSet<string> botCommands,
        HashSet<string> greetingWords,
        string botName = "PokeChat",
        List<string>? renamePatterns = null,
        string sessionId = "",
        SessionLogger? sessionLogger = null,
        ToolRegistry? toolRegistry = null,
        LLMOrchestrator? llmOrchestrator = null,
        ML.IntentClassifier? intentClassifier = null,
        string persona = "chat")
    {
        _dbContext = dbContext;
        _sessionLogger = sessionLogger;
        _knowledgeStore = knowledgeStore;
        _responseEngine = responseEngine;
        _spellChecker = spellChecker;
        _posTagger = posTagger;
        _tokeniser = tokeniser;
        _sentenceSplitter = sentenceSplitter;
        _svoExtractor = svoExtractor;
        _context = context;
        _nounCategoriser = nounCategoriser;
        _namePatterns = namePatterns;
        _botCommands = botCommands;
        _greetingWords = greetingWords;
        _botName = botName;
        _renamePatterns = renamePatterns ?? new List<string>();
        if (!string.IsNullOrEmpty(sessionId))
            _sessionId = sessionId;
        _responseEngine.SetBotName(_botName);
        _currentUserNameLower = _currentUserName.ToLowerInvariant();
        _llmOrchestrator = llmOrchestrator;
        _intentClassifier = intentClassifier ?? new ML.IntentClassifier();
        _persona = persona;
        _context.SetContext(ContextKeys.CurrentPersona, persona);
    }

    private void AutoSeedPosDictionary(McpRegistry registry)
    {
        var keywords = registry.GetTriggerKeywords();
        foreach (var keyword in keywords)
        {
            if (!_dbContext.PosDictionary.Any(e => e.Word == keyword))
            {
                _dbContext.PosDictionary.Add(new Data.Entities.PosDictionaryEntry
                {
                    Word = keyword,
                    WordType = "noun",
                    CreatedAt = DateTime.UtcNow.ToString("o")
                });
            }
        }
        if (keywords.Count > 0)
            _dbContext.SaveChanges();
    }



    public string GetInitialGreeting() =>
        GreetingPool.GetRandomGreeting(_knowledgeStore, _botName, _persona);

    internal string ProcessInput(string input)
    {
        LastResponseCategory = "state_handled";

        var pendingName = _context.GetContext(ContextKeys.PendingNameConfirmation);
        if (pendingName != null)
        {
            return HandleNameConfirmation(input, pendingName);
        }

        var pendingIdentity = _context.GetContext(ContextKeys.PendingIdentityVerification);
        if (pendingIdentity != null)
        {
            return HandleIdentityVerification(input, pendingIdentity);
        }

        if ((_currentUserId == null || _currentUserName == "Guest") && _persona != "coding")
        {
            return HandleNameInput(input);
        }

        var personaResult = TryHandlePersonaSwitch(input);
        if (personaResult != null)
            return personaResult;

        var confirmResult = TryHandleConfirmation(input);
        if (confirmResult != null)
            return confirmResult;

        var routeResult = _router.Route(input, _intentClassifier);
        if (routeResult.Handler != RouteHandler.None)
        {
            return ExecuteBotRoute(routeResult);
        }

        if (_persona == "coding")
        {
            DetectFileMentions(input);
            var resolvedFile = _context.ResolveFilePronoun(input);
            if (resolvedFile != null)
                _context.SetContext(ContextKeys.CurrentFile, resolvedFile);
        }

        var pendingWord = _context.GetContext(ContextKeys.PendingClarificationWord);
        if (pendingWord != null)
        {
            return HandleClarification(input, pendingWord);
        }

        var pendingLlmOffer = _context.GetContext(ContextKeys.PendingLLMOffer);
        if (pendingLlmOffer != null)
        {
            if (_llmOrchestrator?.Config.AlwaysOn == true)
            {
                _context.SetContext(ContextKeys.PendingLLMOffer, null);
                _context.SetContext(ContextKeys.LLMOriginalInput, null);
            }
            else
            {
                var originalInput = _context.GetContext(ContextKeys.LLMOriginalInput);
                var lower = input.Trim().ToLowerInvariant();

                if (_llmOrchestrator != null && Affirmations.Contains(lower))
                {
                    _context.SetContext(ContextKeys.PendingLLMOffer, null);
                    _context.SetContext(ContextKeys.LLMOriginalInput, null);
                    _llmOrchestrator.MarkAccepted();
                    var llmResult = LlmCallWithIndicator(() => _llmOrchestrator.GenerateResponse(originalInput ?? input));
                    if (llmResult != null)
                    {
                        LearnFromLLMResponse(originalInput ?? input, llmResult);
                        BufferLlmInteraction(originalInput ?? input, llmResult);
                        _knowledgeStore.StoreConversation(_currentUserId!.Value, originalInput ?? input, llmResult, _sessionId, "llm_response");
                        _knowledgeStore.Save();
                        return llmResult;
                    }
                    return GetLLMResponse("llm_unavailable");
                }

                if (_llmOrchestrator != null && Denials.Contains(lower))
                {
                    _context.SetContext(ContextKeys.PendingLLMOffer, null);
                    _context.SetContext(ContextKeys.LLMOriginalInput, null);
                    _llmOrchestrator.MarkDeclined();
                    return GetLLMResponse("llm_declined");
                }

                return GetLLMResponse("llm_offer");
            }
        }

        var classWord = _context.GetContext(ContextKeys.PendingClassificationWord);
        if (classWord != null)
        {
            return HandleClassification(input, classWord);
        }

        var placeWord = _context.GetContext(ContextKeys.PendingPlaceWord);
        if (placeWord != null)
        {
            return HandlePlaceFollowUp(input, placeWord);
        }

        var dictSave = _context.GetContext(ContextKeys.PendingDictionarySave);
        if (dictSave != null)
        {
            return HandleDictionarySaveConfirmation(input, dictSave);
        }

        var dictWord = _context.GetContext(ContextKeys.PendingDictionaryWord);
        if (dictWord != null)
        {
            return HandleDictionaryDefinition(input, dictWord);
        }

        var pendingReminderTask = _context.GetContext(ContextKeys.PendingReminderTask);
        if (pendingReminderTask != null)
        {
            var pendingTime = _context.GetContext(ContextKeys.PendingReminderTime);
            if (pendingTime == null)
            {
                var parsedTime = _knowledgeStore.ParseReminderTime(input);
                _context.SetContext(ContextKeys.PendingReminderTime, parsedTime?.ToString("o"));
            }
            return HandleReminderCreation(pendingReminderTask);
        }

        var gameActive = _context.GetContext(ContextKeys.GameModeActive);
        if (gameActive != null)
            return HandleGameTurn(input);

        var madLibsActive = _context.GetContext(ContextKeys.MadLibsActive);
        if (madLibsActive != null)
            return HandleMadLibsTurn(input);

        var jokeSetup = _context.GetContext(ContextKeys.PendingJokeSetup);
        if (jokeSetup != null)
            return HandleJokeTurn();

        var riddleActive = _context.GetContext(ContextKeys.RiddleActive);
        if (riddleActive != null)
            return HandleRiddleTurn(input);

        var wyrActive = _context.GetContext(ContextKeys.WyrActive);
        if (wyrActive != null)
            return HandleWouldYouRatherAnswer(input);

        var hangmanActive = _context.GetContext(ContextKeys.HangmanActive);
        if (hangmanActive != null)
            return HandleHangmanTurn(input);

        var quizActive = _context.GetContext(ContextKeys.QuizActive);
        if (quizActive != null)
            return HandleQuizTurn(input);

        _context.SetContext(ContextKeys.UnknownWords, null);

        if (TryHandleResetRequest(input, out var resetResponse))
            return resetResponse;

        if (TryHandleBotRename(input, out var renameResponse))
            return renameResponse;

        if (TryHandleJokeStart(input, out var jokeResponse))
            return jokeResponse;

        if (TryHandleRiddleStart(input, out var riddleResponse))
            return riddleResponse;

        if (TryHandleMadLibsStart(input, out var madLibsResponse))
            return madLibsResponse;

        if (TryHandleGameStart(input, out var gameStartResponse))
            return gameStartResponse;

        if (TryHandleWouldYouRather(input, out var wyrResponse))
            return wyrResponse;

        if (TryHandleHangmanStart(input, out var hangmanResponse))
            return hangmanResponse;

        if (TryHandleQuizStart(input, out var quizResponse))
            return quizResponse;

        if (TryHandleErrorKnowledge(input, out var errorResponse))
            return errorResponse;

        if (TryHandleCorrection(input, out var correctionResponse))
            return correctionResponse;

        if (TryHandleReminderRequest(input, out var reminderResponse))
            return reminderResponse;

        if (TryHandleMetaCommentary(input, out var metaResponse))
            return metaResponse!;

        LearnGreetingWords(input);

        var (sentiment, intensity) = _knowledgeStore.AnalyseSentiment(input);
        var currentSentiment = _context.GetContext(ContextKeys.CurrentSentiment);
        if (currentSentiment != null && currentSentiment != sentiment)
        {
            _context.SetContext(ContextKeys.PreviousSentiment, currentSentiment);
        }
        else
        {
            _context.SetContext(ContextKeys.PreviousSentiment, null);
        }
        _context.SetContext(ContextKeys.CurrentSentiment, sentiment ?? "neutral");
        _context.SetContext(ContextKeys.LastSentimentIntensity, intensity.ToString());

        if (TryHandleInsult(input, sentiment, intensity, out var insultResponse))
            return insultResponse;

        var selfKnowledgeResponse = _responseEngine.HandleSelfKnowledgeRequest(input, _currentUserId);
        if (selfKnowledgeResponse != null) return selfKnowledgeResponse;

        var earlyLlmResult = TryEarlyLlmRouting(input);
        if (earlyLlmResult != null) return earlyLlmResult;

        var sentences = _sentenceSplitter.Split(input);

        foreach (var sentence in sentences)
        {
            ProcessSentence(sentence, sentiment, intensity);
        }

        var currentCount = _context.GetContext(ContextKeys.ContextFollowUpCount);
        if (currentCount != null && int.TryParse(currentCount, out var cc) && cc > 0 && cc < 3)
        {
            _context.SetContext(ContextKeys.ContextFollowUpCount, "3");
        }

        _context.SetContext(ContextKeys.SessionId, _sessionId);
        _context.SetContext(ContextKeys.LastUserInput, input);

        var prevCategory = _context.GetContext(ContextKeys.CurrentResponseCategory);
        if (prevCategory != null)
            _context.SetContext(ContextKeys.PreviousResponseCategory, prevCategory);

        if (_persona == "coding" && DestructiveCommandPattern.IsMatch(input) &&
            _context.GetContext(ContextKeys.PendingConfirmation) == null)
        {
            _context.SetContext(ContextKeys.PendingConfirmation, "true");
            _context.SetContext(ContextKeys.PendingConfirmationCommand, input);
            return GetLLMResponse("coding_confirmation_prompt");
        }

        var hadTriples = _context.GetContext(ContextKeys.ContextFollowUpCount) == "0";

        var response = _responseEngine.GenerateResponse(input, _currentUserId);
        var responseCategory = _context.GetContext(ContextKeys.CurrentResponseCategory);

        if (_llmOrchestrator?.IsAvailable == true && !_llmOrchestrator.UserDeclined
            && ResponseEngine.IsDeadEndCategory(responseCategory ?? ""))
        {
            if (_llmOrchestrator.Config.AlwaysOn || _llmOrchestrator.IsAccepted)
            {
                var llmResult = LlmCallWithIndicator(() => _llmOrchestrator.GenerateResponse(input));
                if (llmResult != null)
                {
                    LearnFromLLMResponse(input, llmResult);
                    BufferLlmInteraction(input, llmResult);
                    response = llmResult;
                    responseCategory = "llm_response";
                }
                else
                {
                    response = GetLLMResponse("llm_unavailable");
                    responseCategory = "llm_unavailable";
                }
            }
            else if (_context.GetContext(ContextKeys.PendingLLMOffer) == null)
            {
                _context.SetContext(ContextKeys.PendingLLMOffer, "true");
                _context.SetContext(ContextKeys.LLMOriginalInput, input);
            }
        }

        LastResponseCategory = responseCategory;
        _knowledgeStore.StoreConversation(_currentUserId!.Value, input, response, _sessionId, responseCategory);
        _knowledgeStore.Save();

        _context.SetContext(ContextKeys.LastResponseHadSvo, hadTriples ? "true" : "false");

        if (prevCategory != null)
        {
            _knowledgeStore.UpdateResponseEffectiveness(prevCategory, hadTriples);
        }

        if (_sessionLogger != null)
        {
            var contextData = _sessionLogger.Verbose ? BuildLogContext() : null;
            _sessionLogger.LogTurn(input, response, contextData);
        }

        return response;
    }

    private string? TryHandleConfirmation(string input)
    {
        var pendingCmd = _context.GetContext(ContextKeys.PendingConfirmation);
        if (pendingCmd == null) return null;

        var lower = input.Trim().ToLowerInvariant();
        if (Affirmations.Contains(lower))
        {
            var originalInput = _context.GetContext(ContextKeys.PendingConfirmationCommand);
            _context.SetContext(ContextKeys.PendingConfirmation, null);
            _context.SetContext(ContextKeys.PendingConfirmationCommand, null);
            _context.SetContext(ContextKeys.PendingConfirmationArgs, null);

            if (!string.IsNullOrEmpty(originalInput))
                return _responseEngine.GenerateResponse(originalInput, _currentUserId);
            return null;
        }

        if (Denials.Contains(lower))
        {
            _context.SetContext(ContextKeys.PendingConfirmation, null);
            _context.SetContext(ContextKeys.PendingConfirmationCommand, null);
            _context.SetContext(ContextKeys.PendingConfirmationArgs, null);
            return GetLLMResponse("coding_confirmation_denied");
        }

        return "Please answer yes or no. Are you sure?";
    }

    private string? TryHandlePersonaSwitch(string input)
    {
        var lower = input.Trim().ToLowerInvariant();
        if (!PersonaTriggers.Contains(lower))
            return null;

        string newPersona;
        if (lower.Contains("coding"))
            newPersona = "coding";
        else
            newPersona = "chat";

        SwitchPersona(newPersona);
        return GetLLMResponse("persona_switch_" + newPersona);
    }

    internal void SwitchPersona(string persona)
    {
        _persona = persona;
        _context.SetContext(ContextKeys.CurrentPersona, persona);
        _responseEngine.SetPersona(persona);

        if (persona == "coding")
        {
            _botName = "PokeCode";
            TryDetectProjectContext();
        }
        else
        {
            _botName = "PokeChat";
        }

        _responseEngine.SetBotName(_botName);
    }

    internal void ClearPendingState()
    {
        _context.SetContext(ContextKeys.PendingClarificationWord, null);
        _context.SetContext(ContextKeys.PendingClarificationSuggestion, null);
        _context.SetContext(ContextKeys.PendingClassificationWord, null);
        _context.SetContext(ContextKeys.PendingPlaceWord, null);
        _context.SetContext(ContextKeys.PendingLLMOffer, null);
        _context.SetContext(ContextKeys.PendingDictionarySave, null);
        _context.SetContext(ContextKeys.PendingDictionaryWord, null);
        _context.SetContext(ContextKeys.HangmanActive, null);
        _context.SetContext(ContextKeys.HangmanWord, null);
        _context.SetContext(ContextKeys.HangmanGuessed, null);
        _context.SetContext(ContextKeys.HangmanWrongLetters, null);
        _context.SetContext(ContextKeys.HangmanWrongCount, null);
        _context.SetContext(ContextKeys.UnknownWords, null);
    }

    internal void DetectFileMentions(string input)
    {
        var matches = FileMentionPattern.Matches(input);
        if (matches.Count == 0) return;

        var recentFilesRaw = _context.GetContext(ContextKeys.RecentFiles);
        var recentFiles = string.IsNullOrEmpty(recentFilesRaw)
            ? new List<string>()
            : JsonSerializer.Deserialize<List<string>>(recentFilesRaw) ?? new();

        foreach (Match match in matches)
        {
            var file = match.Value;
            _context.SetContext(ContextKeys.CurrentFile, file);
            recentFiles.RemoveAll(f => string.Equals(f, file, StringComparison.OrdinalIgnoreCase));
            recentFiles.Add(file);
        }

        if (recentFiles.Count > 5)
            recentFiles = recentFiles.TakeLast(5).ToList();

        _context.SetContext(ContextKeys.RecentFiles, JsonSerializer.Serialize(recentFiles));
    }

    private void TryDetectProjectContext()
    {
        try
        {
            var dir = Directory.GetCurrentDirectory();
            _context.SetContext(ContextKeys.ProjectRoot, dir);

            var branch = RunGitCommand("branch --show-current");
            if (!string.IsNullOrEmpty(branch))
                _context.SetContext(ContextKeys.CurrentBranch, branch.Trim());

            var lastBuild = RunGitCommand("log --oneline -1");
            if (!string.IsNullOrEmpty(lastBuild))
                _context.SetContext(ContextKeys.LastBuildOutput, lastBuild.Trim());
        }
        catch
        {
        }
    }

    private static string? RunGitCommand(string args)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "git",
                Arguments = args,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = System.Diagnostics.Process.Start(psi);
            if (proc == null) return null;
            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(2000);
            return string.IsNullOrWhiteSpace(output) ? null : output;
        }
        catch
        {
            return null;
        }
    }

    internal void LearnGreetingWords(string input)
    {
        var tokens = _tokeniser.Tokenise(input);
        if (tokens.Count > 0)
        {
            var firstWord = tokens[0];
            if (!_greetingWords.Contains(firstWord) && !IsStopWord(firstWord))
            {
                var lowerInput = input.ToLowerInvariant();
                foreach (var pattern in _namePatterns)
                {
                    if (lowerInput.Contains(pattern))
                    {
                        _knowledgeStore.AddGreetingWord(firstWord, _currentUserId);
                        _greetingWords.Add(firstWord);
                        break;
                    }
                }
            }
        }
    }

    internal void ProcessSentence(string sentence, string? sentiment = null, int intensity = 0)
    {
        _context.SetContext(ContextKeys.InferredGeneralisation, null);

        var tokens = _tokeniser.Tokenise(sentence);
        var correctedTokens = _spellChecker.AutoCorrect(tokens);

        var unknownWords = _spellChecker.GetUnknownWords(correctedTokens);

        foreach (var token in correctedTokens)
        {
            if (_spellChecker.IsPluralOfKnownWord(token))
            {
                _spellChecker.AddToDictionary(token);
                _knowledgeStore.AddLearnedWord(token);
            }
        }

        var tags = _posTagger.Tag(correctedTokens);
        var triples = _svoExtractor.Extract(correctedTokens, tags);
        var anyTripleProcessed = false;

        if (unknownWords.Count > 0 && triples.Count > 0)
        {
            foreach (var triple in triples)
            {
                var subjTokens = triple.Subject.Split(' ');
                var objTokens = triple.Object.Split(' ');
                foreach (var unknown in unknownWords.ToList())
                {
                    if (subjTokens.Any(t => t.Equals(unknown, StringComparison.OrdinalIgnoreCase)) ||
                        objTokens.Any(t => t.Equals(unknown, StringComparison.OrdinalIgnoreCase)))
                    {
                        _spellChecker.AddToDictionary(unknown);
                        _knowledgeStore.AddLearnedWord(unknown);
                        unknownWords.Remove(unknown);
                    }
                }
            }
        }

        if (unknownWords.Count > 0 && correctedTokens.Count == 1 && triples.Count == 0)
        {
            var word = unknownWords[0];
            _spellChecker.AddToDictionary(word);
            _knowledgeStore.AddLearnedWord(word);
            unknownWords.Remove(word);
            _context.UpdateLastSubject(word);
        }

        if (_interviewModeActive && unknownWords.Count > 0)
        {
            foreach (var uw in unknownWords.ToList())
            {
                _spellChecker.AddToDictionary(uw);
                _knowledgeStore.AddLearnedWord(uw);
            }
            unknownWords.Clear();
        }

        if (unknownWords.Count > 0)
        {
            var existing = _context.GetContext(ContextKeys.UnknownWords) ?? "";
            var existingWords = existing.Split(',', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
            foreach (var uw in unknownWords) existingWords.Add(uw);
            _context.SetContext(ContextKeys.UnknownWords, string.Join(",", existingWords));
        }

        foreach (var triple in triples)
        {
            var resolvedSubject = ResolveSubject(triple.Subject);
            var resolvedObject = ResolveObject(triple.Object);

            if (string.IsNullOrEmpty(resolvedSubject) || string.IsNullOrEmpty(resolvedObject))
                continue;

            var resolvedSubjectLower = resolvedSubject.ToLowerInvariant();
            if (resolvedSubjectLower is "not" or "never" or "no")
                continue;

            var predicateType = ClassifyPredicate(resolvedSubject, triple.Verb, resolvedObject);
            var timeContext = _knowledgeStore.ExtractTimeContext(sentence) ?? _context.GetContext(ContextKeys.CurrentTimeContext);
            if (timeContext != null)
                _context.SetContext(ContextKeys.CurrentTimeContext, timeContext);

            var lowerObj = resolvedObject.ToLowerInvariant();
            var objTokens = lowerObj.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            bool allFunctionWords = objTokens.Length > 0 && objTokens.All(t => FunctionWords.Contains(t));

            var subjectTokens = resolvedSubject.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            bool subjectIsFunctionOnly = subjectTokens.Length > 1 && subjectTokens.All(t =>
                FunctionWords.Contains(t) || ContentWordIndicators.Contains(t));

            bool subjectLeadsWithGarbage = subjectTokens.Length > 0 &&
                new[] { "yes", "no", "yeah", "nope", "oh", "ah", "well", "so" }
                .Contains(subjectTokens[0].ToLowerInvariant());

            bool subjectIsPronounAndShort = subjectTokens.Length <= 2 &&
                subjectTokens.All(t => ContentWordIndicators.Contains(t.ToLowerInvariant()));

            bool subjectIsInterrogative = subjectTokens.Length > 0 &&
                new[] { "what", "when", "where", "why", "who", "whom", "whose", "which", "how" }
                .Contains(subjectTokens[0].ToLowerInvariant());

            if (allFunctionWords || subjectIsFunctionOnly || subjectLeadsWithGarbage || subjectIsPronounAndShort || subjectIsInterrogative)
                continue;

            if (NegationWords.Any(w => lowerObj.StartsWith(w + " ") || lowerObj.Equals(w)))
                continue;
            if (objTokens.Length == 1 && FunctionWords.Contains(lowerObj))
                continue;

            var fact = new Fact
            {
                UserId = _currentUserId,
                Subject = resolvedSubject,
                Verb = triple.Verb,
                Object = resolvedObject,
                PredicateType = predicateType.ToString(),
                Sentiment = sentiment ?? _context.GetContext(ContextKeys.CurrentSentiment),
                EmotionIntensity = intensity > 0 ? intensity : int.TryParse(_context.GetContext(ContextKeys.LastSentimentIntensity) ?? "0", out var si) ? si : 0,
                TimeContext = timeContext,
                MentionedAt = DateTime.UtcNow.ToString("o"),
                CreatedAt = DateTime.UtcNow.ToString("o")
            };

            if (predicateType is PredicateType.Preference or PredicateType.Dislike)
            {
                var contradiction = _knowledgeStore.DetectContradiction(_currentUserId!.Value, resolvedSubject, triple.Verb, resolvedObject);
                if (contradiction != null)
                {
                    _context.SetContext(ContextKeys.LastContradiction,
                        $"{contradiction.Verb}|{contradiction.Object}|{triple.Verb}|{resolvedObject}");
                    continue;
                }

                var categories = _knowledgeStore.GetCategoryChain(resolvedObject);
                foreach (var category in categories)
                {
                    _context.SetContext(ContextKeys.InferredGeneralisation, $"{category}|{resolvedObject}");
                }
            }

            var existingFact = _knowledgeStore.GetFact(resolvedSubject, triple.Verb, resolvedObject, _currentUserId);
            if (existingFact == null)
            {
                if (_currentUserId.HasValue &&
                    _knowledgeStore.TryEndorseFact(resolvedSubject, triple.Verb, resolvedObject, _currentUserId.Value))
                {
                    anyTripleProcessed = true;
                }
                else
                {
                    _knowledgeStore.StoreFact(fact);
                    anyTripleProcessed = true;
                }
            }

            if (predicateType is PredicateType.GeneralFact or PredicateType.PersonalAttribute)
            {
                if (lowerObj is "a person" or "person")
                    _nounCategoriser.CategoriseNoun(resolvedSubject);
                else if (lowerObj is "a place" or "place")
                    _nounCategoriser.CategoriseNoun(resolvedSubject);
                else if (lowerObj is "a thing" or "thing")
                    _nounCategoriser.CategoriseNoun(resolvedSubject);
            }

            if (predicateType is PredicateType.Preference && Random.Shared.Next(7) == 0)
                _context.SetContext(ContextKeys.PendingCompliment, "true");

            _context.UpdateLastSubject(resolvedSubject);
            _context.UpdateLastObject(resolvedObject);

            var topicCategory = _nounCategoriser.CategoriseNoun(resolvedObject);
            _context.PushTopic(resolvedSubject, triple.Verb, resolvedObject, topicCategory, predicateType);
        }

        if (anyTripleProcessed)
        {
            _context.SetContext(ContextKeys.ContextFollowUpCount, "0");
            _context.SetContext(ContextKeys.TopicReferenceCount, "0");

            var lastTriple = triples[^1];
            var subjCat = _nounCategoriser.CategoriseNoun(ResolveSubject(lastTriple.Subject));
            var objCat = _nounCategoriser.CategoriseNoun(ResolveObject(lastTriple.Object));
            _context.SetContext(ContextKeys.SubjectCategory, subjCat);
            _context.SetContext(ContextKeys.ObjectCategory, objCat);
        }
        else if (correctedTokens.Count == 1 && tags[0] == PosTag.Noun)
        {
            var noun = correctedTokens[0];
            if (!string.Equals(noun, _currentUserName, StringComparison.OrdinalIgnoreCase))
            {
                _context.UpdateLastSubject(noun);
                var cat = _nounCategoriser.CategoriseNoun(noun);
                _context.SetContext(ContextKeys.ObjectCategory, cat);
            }
        }

    }

    internal string ResolveSubject(string subject)
    {
        var lower = subject.ToLowerInvariant();
        return lower switch
        {
            "i" or "me" or "my" or "myself" => _currentUserName,
            "we" or "us" or "our" => _currentUserName,
            "it" or "its" or "itself" => _context.ResolvePronoun(lower),
            "he" or "him" or "his" => _context.ResolvePronoun(lower),
            "she" or "her" => _context.ResolvePronoun(lower),
            "they" or "them" or "their" => _context.ResolvePronoun(lower),
            _ => subject
        };
    }

    internal string ResolveObject(string obj)
    {
        var lower = obj.ToLowerInvariant();
        return lower switch
        {
            "it" or "this" or "that" or "him" or "her" or "them" => _context.ResolvePronoun(lower),
            _ => obj
        };
    }

    public string? LastResponseCategory { get; set; }
    public bool LastResponseIsDeadEnd => LastResponseCategory != null && ResponseEngine.IsDeadEndCategory(LastResponseCategory);

    internal string? LastSubject => _context.LastSubject;
    internal string? LastObject => _context.LastObject;
    internal IReadOnlyList<TopicEntry> TopicStack => _context.TopicStack;
    internal string? GetContextValue(string key) => _context.GetContext(key);
    internal void SetContext(string key, string? value) => _context.SetContext(key, value);

    internal bool IsInterviewActive => _interviewModeActive;
    internal int? CurrentUserId { get => _currentUserId; set => _currentUserId = value; }
    internal string CurrentUserName => _currentUserName;
    internal int? SavedUserId => _savedUserId;
    internal string BotName => _botName;
    internal string SessionId { get => _sessionId; set => _sessionId = value; }
    internal string Persona => _persona;
    internal string? PendingFollowUp { get => _pendingFollowUp; set => _pendingFollowUp = value; }
    internal int FollowUpCount { get => _followUpCount; set => _followUpCount = value; }
    internal string? LastInterviewQuestion { get => _lastInterviewQuestion; set => _lastInterviewQuestion = value; }
    internal IInterviewEngine? InterviewEngine => _interviewEngine;
    internal void Save() => _knowledgeStore.Save();
    internal void LogSystem(string message) => _sessionLogger?.LogSystem(message);
    internal void RecordSessionMetrics() => _knowledgeStore.RecordSessionMetrics(_sessionId);

    internal void SetLLMOfferState(string originalInput)
    {
        if (_llmOrchestrator?.Config.AlwaysOn == true) return;
        _context.SetContext(ContextKeys.PendingLLMOffer, "true");
        _context.SetContext(ContextKeys.LLMOriginalInput, originalInput);
    }

    public static string StemVerb(string verb)
    {
        var lower = verb.ToLowerInvariant();
        return lower switch
        {
            "has" => "have",
            "does" => "do",
            "goes" => "go",
            "says" => "say",
            "makes" => "make",
            "takes" => "take",
            "comes" => "come",
            "gives" => "give",
            "lives" => "live",
            "plays" => "play",
            "works" => "work",
            "thinks" => "think",
            "tells" => "tell",
            "gets" => "get",
            _ when lower.Length > 3 && lower.EndsWith("ies") => lower[..^3] + "y",
            _ when lower.Length > 3 && lower.EndsWith("sses") => lower[..^2],
            _ when lower.Length > 3 && lower.EndsWith("shes") => lower[..^2],
            _ when lower.Length > 3 && lower.EndsWith("ches") => lower[..^2],
            _ when lower.Length > 3 && lower.EndsWith("xes") => lower[..^2],
            _ when lower.Length > 3 && lower.EndsWith("zzes") => lower[..^2],
            _ when lower.Length > 3 && lower.EndsWith("s") && !lower.EndsWith("ss") => lower[..^1],
            _ when lower.Length > 4 && lower.EndsWith("ied") => lower[..^3] + "y",
            _ when lower.Length > 4 && lower.EndsWith("pped") => lower[..^2],
            _ when lower.Length > 4 && lower.EndsWith("tted") => lower[..^2],
            _ when lower.Length > 4 && lower.EndsWith("gged") => lower[..^2],
            _ when lower.Length > 4 && lower.EndsWith("lled") => lower[..^2],
            _ when lower.Length > 4 && lower.EndsWith("mmed") => lower[..^2],
            _ when lower.Length > 4 && lower.EndsWith("nned") => lower[..^2],
            _ when lower.Length > 4 && lower.EndsWith("rred") => lower[..^2],
            _ when lower.Length > 4 && lower.EndsWith("ed") && !lower.EndsWith("eed") => lower[..^2],
            _ => lower
        };
    }

    internal PredicateType ClassifyPredicate(string subject, string verb, string obj)
    {
        var lowerVerb = StemVerb(verb);
        var lowerSubject = subject.ToLowerInvariant();

            if (lowerVerb is "is" or "am" or "are" or "was" or "were")
            {
                if (lowerSubject == _currentUserNameLower)
                {
                    return PredicateType.PersonalAttribute;
                }
                return PredicateType.GeneralFact;
            }

        if (lowerVerb is "like" or "love" or "enjoy" or "prefer")
        {
            return PredicateType.Preference;
        }

        if (lowerVerb is "hate" or "dislike")
        {
            return PredicateType.Dislike;
        }

        if (lowerVerb is "have" or "has" or "own")
        {
            return PredicateType.Possession;
        }

        if (lowerVerb is "know" or "understand" or "believe")
        {
            return PredicateType.Belief;
        }

        return PredicateType.General;
    }

    internal string HandleClarification(string input, string pendingWord)
    {
        var pendingSuggestion = _context.GetContext(ContextKeys.PendingClarificationSuggestion);
        _context.SetContext(ContextKeys.PendingClarificationWord, null);
        _context.SetContext(ContextKeys.PendingClarificationSuggestion, null);

        var lower = input.ToLowerInvariant().Trim();

        if (!string.IsNullOrEmpty(pendingSuggestion))
        {
            if (Affirmations.Contains(lower))
            {
                _knowledgeStore.AddMisspelling(pendingWord, pendingSuggestion);
                _spellChecker.AddToDictionary(pendingSuggestion);
                _knowledgeStore.Save();
                return $"Got it! I'll remember that '{pendingWord}' should be '{pendingSuggestion}'.";
            }

            return $"OK, I'll leave '{pendingWord}' as it is.";
        }

        if (IsClarificationCancelled(lower))
            return CancelClarification(pendingWord);

        _context.UpdateLastSubject(_currentUserName);
        _context.UpdateLastObject(pendingWord);
        _knowledgeStore.AddLearnedWord(pendingWord);
        _spellChecker.AddToDictionary(pendingWord);
        _knowledgeStore.Save();

        var countRaw = _context.GetContext(ContextKeys.PendingClassificationCount);
        int.TryParse(countRaw, out var count);
        count++;
        _context.SetContext(ContextKeys.PendingClassificationCount, count.ToString());

        if (count <= 2)
        {
            _context.SetContext(ContextKeys.PendingClassificationWord, pendingWord);
            return GetClassifyResponse("word_classify_default", pendingWord);
        }

        return $"Thanks! I've learned the word '{pendingWord}'.";
    }

    internal string HandleClassification(string input, string word)
    {
        _context.SetContext(ContextKeys.PendingClassificationWord, null);
        var lower = input.ToLowerInvariant().Trim();

        if (IsClarificationCancelled(lower))
            return CancelClassification(word);

        if (IsDontKnowClassification(lower))
        {
            var llmCategory = TryClassifyWithLlm(word);
            if (llmCategory != null)
            {
                ApplyClassification(word, llmCategory);
                _knowledgeStore.Save();
                return GetClassifyResponse("word_classify_llm_result", word, llmCategory);
            }

            _knowledgeStore.Save();
            return GetClassifyResponse("word_classify_dont_know", word);
        }

        var wordType = ParseWordType(lower);

        if (wordType is "person" or "thing")
        {
            _knowledgeStore.UpdateWordType(word, "noun");
            _knowledgeStore.AddNounCategory(word, wordType, _currentUserId);
            _knowledgeStore.Save();
            return GetClassifyResponse("word_classify_learned_noun", word, wordType);
        }

        if (wordType == "place")
        {
            _knowledgeStore.UpdateWordType(word, "noun");
            _knowledgeStore.AddNounCategory(word, "place", _currentUserId);
            _knowledgeStore.Save();
            _context.SetContext(ContextKeys.PendingPlaceWord, word);
            return GetClassifyResponse("word_classify_place_ask", word);
        }

        if (wordType == "noun")
        {
            _knowledgeStore.UpdateWordType(word, "noun");
            _knowledgeStore.Save();
            return GetClassifyResponse("word_classify_learned_noun", word, "noun");
        }

        if (wordType == "verb")
        {
            _knowledgeStore.UpdateWordType(word, "verb");
            _knowledgeStore.Save();
            return GetClassifyResponse("word_classify_learned_verb", word);
        }

        if (wordType == "adjective")
        {
            _knowledgeStore.UpdateWordType(word, "adjective");
            _knowledgeStore.Save();
            return GetClassifyResponse("word_classify_learned_adj", word);
        }

        _knowledgeStore.Save();
        return GetClassifyResponse("word_classify_learned_unknown", word);
    }

    private static bool IsDontKnowClassification(string lowerInput)
    {
        if (DontKnowClassificationPhrases.Contains(lowerInput))
            return true;

        foreach (var phrase in DontKnowClassificationPhrases)
        {
            if (lowerInput.Contains(phrase))
                return true;
        }

        return false;
    }

    private string? TryClassifyWithLlm(string word)
    {
        if (_llmOrchestrator?.IsAvailable != true || _llmOrchestrator.UserDeclined)
            return null;
        if (!_llmOrchestrator.Config.AlwaysOn && !_llmOrchestrator.IsAccepted)
            return null;

        var prompt = $"Classify the word '{word}' as exactly one of: person, place, thing, verb, adjective, noun. " +
                     "Return ONLY the single category name. No explanation, no punctuation.";
        var result = LlmCallWithIndicator(() => _llmOrchestrator.GenerateResponse(prompt));
        if (string.IsNullOrEmpty(result))
            return null;

        var clean = result.Trim().Trim('.').ToLowerInvariant();
        var valid = new HashSet<string> { "person", "place", "thing", "verb", "adjective", "noun" };
        return valid.Contains(clean) ? clean : null;
    }

    private void ApplyClassification(string word, string category)
    {
        if (category is "person" or "thing" or "place")
        {
            _knowledgeStore.UpdateWordType(word, "noun");
            _knowledgeStore.AddNounCategory(word, category, _currentUserId);
        }
        else if (category is "noun" or "verb" or "adjective")
        {
            _knowledgeStore.UpdateWordType(word, category);
        }
    }

    internal string HandlePlaceFollowUp(string input, string word)
    {
        _context.SetContext(ContextKeys.PendingPlaceWord, null);
        var lower = input.ToLowerInvariant().Trim();

        if (Affirmations.Contains(lower) || lower.Contains("been there") || lower.Contains("visited"))
        {
            var fact = new Fact
            {
                UserId = _currentUserId,
                Subject = _currentUserName,
                Verb = "visited",
                Object = word,
                PredicateType = PredicateType.General.ToString(),
                CreatedAt = DateTime.UtcNow.ToString("o")
            };
            _knowledgeStore.StoreFact(fact);
            _knowledgeStore.Save();
            return GetClassifyResponse("word_classify_place_yes", word);
        }

        _knowledgeStore.Save();
        return GetClassifyResponse("word_classify_place_no", word);
    }

    private static string? ParseWordType(string input)
    {
        var lower = input.ToLowerInvariant();

        if (lower.Contains("person") || lower.Contains("someone") || lower.Contains("somebody"))
            return "person";
        if (lower.Contains("place") || lower.Contains("location") || lower.Contains("somewhere"))
            return "place";
        if (lower.Contains("thing") || lower.Contains("object") || lower.Contains("item") ||
            lower.Contains("concept") || lower.Contains("idea"))
            return "thing";
        if (lower.Contains("verb") || lower.Contains("action") || lower.Contains("doing word"))
            return "verb";
        if (lower.Contains("adjective") || lower.Contains("describing word") || lower.Contains("describes"))
            return "adjective";
        if (lower.Contains("noun") || lower.Contains("naming word"))
            return "noun";

        return null;
    }

    private static bool IsClarificationCancelled(string lowerInput)
    {
        if (CancellationPhrases.Contains(lowerInput))
            return true;

        foreach (var phrase in CancellationPhrases)
        {
            if (lowerInput.Contains(phrase))
                return true;
        }

        return false;
    }

    private string CancelClarification(string pendingWord)
    {
        return GetClassifyResponse("word_learn_cancelled");
    }

    private string CancelClassification(string word)
    {
        _knowledgeStore.RemoveLearnedWord(word);
        _spellChecker.RemoveFromDictionary(word);
        _knowledgeStore.Save();
        return GetClassifyResponse("word_learn_cancelled");
    }

    private string GetClassifyResponse(string category, params object[] args)
    {
        var botResponses = GetCachedBotResponses();
        if (botResponses.TryGetValue(category, out var responses) && responses.Count > 0)
        {
            var template = responses[Random.Shared.Next(responses.Count)];
            return args.Length > 0 ? string.Format(template, args) : template;
        }

        var fallbacks = new Dictionary<string, List<string>>
        {
            ["word_classify_default"] = new() { $"Thanks! I've learned the word '{{0}}'. Is it a person, place, thing, verb, adjective, or noun?" },
            ["word_classify_learned_noun"] = new() { $"Got it! I'll remember '{{0}}' as a {{1}}." },
            ["word_classify_learned_verb"] = new() { $"Got it! I'll remember '{{0}}' as a verb." },
            ["word_classify_learned_adj"] = new() { $"Got it! I'll remember '{{0}}' as an adjective." },
            ["word_classify_learned_unknown"] = new() { $"Okay, I've learned the word '{{0}}'." },
            ["word_classify_dont_know"] = new() { $"No problem, I'll keep it as unknown for now.", $"That's okay — I'll figure out the type later." },
            ["word_classify_llm_result"] = new() { $"I think '{{0}}' is a {{1}}.", $"I'd guess '{{0}}' is a {{1}}." },
            ["word_classify_place_ask"] = new() { $"Have you ever been to {{0}}?" },
            ["word_classify_place_yes"] = new() { $"Nice! I'll remember that you've visited {{0}}." },
            ["word_classify_place_no"] = new() { $"No problem, I'll remember {{0}} is a place." },
            ["word_learn_cancelled"] = new() { "No problem, I won't remember that!", "Got it, I'll forget about that word." },
        };

        if (fallbacks.TryGetValue(category, out var fb) && fb.Count > 0)
        {
            var template = fb[Random.Shared.Next(fb.Count)];
            return args.Length > 0 ? string.Format(template, args) : template;
        }

        return string.Empty;
    }

    internal string HandleDictionarySaveConfirmation(string input, string saveData)
    {
        _context.SetContext(ContextKeys.PendingDictionarySave, null);

        var parts = saveData.Split('|', 2);
        if (parts.Length < 2)
            return "Got it.";

        var word = parts[0];
        var definition = parts[1];
        var lower = input.Trim().ToLowerInvariant();

        if (Affirmations.Contains(lower))
        {
            _knowledgeStore.SetDefinition(word, definition, _currentUserId);
            _knowledgeStore.AddLearnedWord(word);
            _spellChecker.AddToDictionary(word);
            _knowledgeStore.Save();
            return GetDictionarySavedResponse(word, definition);
        }

        if (Denials.Contains(lower))
            return GetLLMResponse("llm_declined"); // reuse: "No problem, I'll keep learning!"

        return "Do you want me to remember that definition?";
    }

    internal string HandleDictionaryDefinition(string input, string word)
    {
        _context.SetContext(ContextKeys.PendingDictionaryWord, null);

        var tokens = _tokeniser.Tokenise(input.ToLowerInvariant());
        var definition = string.Empty;

        for (var i = 0; i < tokens.Count; i++)
        {
            if (tokens[i] is "is" or "are" or "means" or "mean" or "refers to")
            {
                if (i + 1 < tokens.Count)
                {
                    definition = string.Join(" ", tokens.Skip(i + 1));
                }
                break;
            }
        }

        if (string.IsNullOrEmpty(definition) && tokens.Count > 0)
        {
            if (tokens[0] == word && tokens.Count > 1)
                definition = string.Join(" ", tokens.Skip(1));
            else
                definition = input.Trim();
        }

        _knowledgeStore.SetDefinition(word, definition, _currentUserId);
        _knowledgeStore.AddLearnedWord(word);
        _spellChecker.AddToDictionary(word);
        _knowledgeStore.Save();

        return GetDictionarySavedResponse(word, definition);
    }

    private Dictionary<string, List<string>> GetCachedBotResponses()
    {
        _cachedBotResponses ??= _knowledgeStore.GetBotResponses();
        return _cachedBotResponses;
    }

    private string GetDictionarySavedResponse(string word, string definition)
    {
        var botResponses = GetCachedBotResponses();
        if (botResponses.TryGetValue("dictionary_definition_saved", out var responses) && responses.Count > 0)
        {
            var template = responses[Random.Shared.Next(responses.Count)];
            return string.Format(template, word, definition);
        }

        var fallbacks = new List<string>
        {
            $"Thanks! I've learned that {word} means {definition}.",
            $"Got it! {word}: {definition}. I'll remember that."
        };
        return fallbacks[Random.Shared.Next(fallbacks.Count)];
    }

    private string GetNameIntroResponse(string userName)
    {
        var botResponses = GetCachedBotResponses();
        if (botResponses.TryGetValue("name_intro", out var responses) && responses.Count > 0)
        {
            var template = responses[Random.Shared.Next(responses.Count)];
            return string.Format(template, userName);
        }

        var fallbacks = new List<string>
        {
            $"Nice to meet you, {userName}! What would you like to talk about?",
            $"Hello {userName}! Feel free to share anything with me.",
            $"Great, {userName}! I'm ready to learn from our conversation.",
            $"Welcome, {userName}! Tell me about yourself or anything on your mind."
        };
        return fallbacks[Random.Shared.Next(fallbacks.Count)];
    }

    internal string HandleNameInput(string input)
    {
        var tokens = _tokeniser.Tokenise(input);
        var name = ExtractName(input, tokens);

        if (string.IsNullOrEmpty(name))
        {
            if (tokens.Count == 1 && !IsStopWord(tokens[0]))
            {
                var lowerToken = tokens[0].ToLowerInvariant();
                if (!_greetingWords.Contains(lowerToken) && !IsCloseToGreeting(lowerToken))
                {
                    name = tokens[0];
                }
            }

            if (string.IsNullOrEmpty(name))
            {
                if (tokens.Count > 0 && tokens.Any(t => _greetingWords.Contains(t.ToLowerInvariant())))
                {
                    var greeting = GreetingPool.GetRandomGreeting(_knowledgeStore, _botName);
                    return $"{greeting} What's your name?";
                }
                return "I didn't catch your name. Could you tell me again?";
            }
        }

        if (name.Length < 2 || name.Length > 30)
            return "That name seems a bit unusual. Could you tell me your name again?";

        var nameLower = name.ToLowerInvariant();
        if (_posTagger.IsKnownWord(nameLower))
        {
            _context.SetContext(ContextKeys.PendingNameConfirmation, name);
            return $"Did you mean your name is {name}, or are you talking about something else?";
        }

        var normalizedName = char.ToUpper(name[0]) + name.Substring(1).ToLowerInvariant();
        var existingUser = _dbContext.Users.FirstOrDefault(u => u.Name == normalizedName);
        if (existingUser != null && existingUser.FirstSeen != existingUser.LastSeen)
        {
            _context.SetContext(ContextKeys.PendingIdentityVerification, name);
            return $"Welcome back, {name}! Are you still using that name?";
        }

        return FinalizeNameSetup(name);
    }

    internal void EstablishDefaultUser(string name = "Guest")
    {
        if (_currentUserId != null) return;
        FinalizeNameSetup(name);
    }

    internal void RestoreUser(int userId, string userName)
    {
        if (_currentUserId != null) return;
        _currentUserId = userId;
        _currentUserName = char.ToUpper(userName[0]) + userName.Substring(1).ToLowerInvariant();
        _currentUserNameLower = _currentUserName.ToLowerInvariant();
        _responseEngine.SetCurrentUserName(_currentUserName);
        _context.UpdateLastSubject(_currentUserName);
        _context.SetContext(ContextKeys.UserName, _currentUserName);

        var storedName = _knowledgeStore.GetUserBotName(userId);
        if (storedName != null)
        {
            _botName = char.ToUpper(storedName[0]) + storedName.Substring(1).ToLowerInvariant();
            _responseEngine.SetBotName(_botName);
        }
    }

    private string FinalizeNameSetup(string name)
    {
        _currentUserName = char.ToUpper(name[0]) + name.Substring(1).ToLowerInvariant();
        _currentUserNameLower = _currentUserName.ToLowerInvariant();
        _currentUserId = _knowledgeStore.GetOrCreateUser(_currentUserName);
        _responseEngine.SetCurrentUserName(_currentUserName);

        _sessionLogger?.LogNameCaptured(_currentUserName, "");

        var storedName = _knowledgeStore.GetUserBotName(_currentUserId!.Value);
        if (storedName != null)
        {
            _botName = char.ToUpper(storedName[0]) + storedName.Substring(1).ToLowerInvariant();
            _responseEngine.SetBotName(_botName);
        }

        _context.Clear();
        _context.UpdateLastSubject(_currentUserName);
        _context.SetContext(ContextKeys.UserName, _currentUserName);

        var response = GetNameIntroResponse(_currentUserName);
        var recall = TryBuildCrossSessionRecall();
        if (recall != null)
            response += "\n" + recall;
        return response;
    }

    private string HandleNameConfirmation(string input, string pendingName)
    {
        var lower = input.Trim().ToLowerInvariant();

        if (Affirmations.Contains(lower))
        {
            _context.SetContext(ContextKeys.PendingNameConfirmation, null);
            return FinalizeNameSetup(pendingName);
        }

        if (Denials.Contains(lower))
        {
            _context.SetContext(ContextKeys.PendingNameConfirmation, null);
            return "Oh, what's your name then?";
        }

        _context.SetContext(ContextKeys.PendingNameConfirmation, null);
        return HandleNameInput(input);
    }

    private string HandleIdentityVerification(string input, string pendingName)
    {
        var lower = input.Trim().ToLowerInvariant();

        if (Affirmations.Contains(lower))
        {
            _context.SetContext(ContextKeys.PendingIdentityVerification, null);
            return FinalizeNameSetup(pendingName);
        }

        if (Denials.Contains(lower))
        {
            _context.SetContext(ContextKeys.PendingIdentityVerification, null);
            return "What should I call you then?";
        }

        _context.SetContext(ContextKeys.PendingIdentityVerification, null);
        return HandleNameInput(input);
    }

    internal string? TryBuildCrossSessionRecall()
    {
        if (_currentUserId == null) return null;

        var attempted = _context.GetContext(ContextKeys.RecallAttempted);
        if (attempted != null) return null;
        _context.SetContext(ContextKeys.RecallAttempted, "true");

        if (Random.Shared.NextDouble() >= 0.3) return null;

        var previousSessions = _knowledgeStore.GetPreviousSessions(_currentUserId.Value, _sessionId);
        if (previousSessions.Count == 0) return null;

        Fact? selectedFact = null;
        var dayName = "last time";

        foreach (var session in previousSessions)
        {
            var fact = _knowledgeStore.GetRandomFactFromSession(_currentUserId.Value, session.SessionGuid);
            if (fact != null)
            {
                selectedFact = fact;
                if (DateTime.TryParse(session.StartedAt, out var dt))
                    dayName = dt.DayOfWeek.ToString();
                break;
            }
        }

        if (selectedFact == null) return null;

        var botResponses = GetCachedBotResponses();
        if (botResponses.TryGetValue("cross_session_recall", out var responses) && responses.Count > 0)
        {
            var template = responses[Random.Shared.Next(responses.Count)];
            return string.Format(template, dayName, selectedFact.Subject, selectedFact.Verb, selectedFact.Object);
        }

        var fallbacks = new List<string>
        {
            $"Last time we spoke on {dayName}, you mentioned {selectedFact.Subject} {selectedFact.Verb} {selectedFact.Object} — how's that going?",
            $"I recall that on {dayName}, you said {selectedFact.Subject} {selectedFact.Verb} {selectedFact.Object}. What's new?",
            $"Last time you were here, we talked about {selectedFact.Object}. Is that still a thing?",
            $"You told me {selectedFact.Subject} {selectedFact.Verb} {selectedFact.Object} last time. Any updates?",
            $"I remember from {dayName} that you told me {selectedFact.Subject} {selectedFact.Verb} {selectedFact.Object}. How are things?",
        };
        return fallbacks[Random.Shared.Next(fallbacks.Count)];
    }

    internal string ExtractName(string input, List<string> tokens)
    {
        var lowerInput = input.ToLowerInvariant();

        foreach (var pattern in _namePatterns)
        {
            var idx = lowerInput.IndexOf(pattern, StringComparison.Ordinal);
            if (idx >= 0)
            {
                var namePart = input.Substring(idx + pattern.Length).Trim();
                var nameTokens = _tokeniser.Tokenise(namePart);
                if (nameTokens.Count > 0)
                {
                    return nameTokens[0];
                }
            }
        }

        if (tokens.Count == 1 && !IsStopWord(tokens[0]))
        {
            var lowerToken = tokens[0].ToLowerInvariant();
            if (!_greetingWords.Contains(lowerToken))
            {
                if (IsCloseToGreeting(lowerToken))
                    return string.Empty;
                if (NameBlockers.Contains(lowerToken))
                    return string.Empty;
                return tokens[0];
            }
            return string.Empty;
        }

        return string.Empty;
    }

    private static readonly HashSet<string> NameBlockers = new(StringComparer.OrdinalIgnoreCase)
    {
        "tell", "make", "give", "ask", "do", "play", "say", "crack", "start", "stop",
        "funny", "joke", "riddle", "limerick", "haiku", "poem", "story", "game", "hangman",
        "interview", "train", "mad", "wyr", "would",
        "what", "who", "where", "when", "why", "how", "which",
        "hello", "hey", "hi", "goodbye", "bye", "thanks", "thank",
        "yes", "no", "yep", "nope", "sure", "ok", "okay", "nah",
        "quit", "exit",
    };

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
        { "a", "an", "the", "is", "am", "are", "was", "were", "be", "been", "being" };

    internal bool IsStopWord(string word)
    {
        return StopWords.Contains(word);
    }

    private bool IsCloseToGreeting(string word)
    {
        return _greetingWords.Any(g =>
        {
            if (System.Math.Abs(g.Length - word.Length) > 2)
                return false;
            var distance = Levenshtein(word, g);
            return distance <= 1;
        });
    }

    private static int Levenshtein(string a, string b)
    {
        var lenA = a.Length;
        var lenB = b.Length;
        var matrix = new int[lenA + 1, lenB + 1];
        for (int i = 0; i <= lenA; i++) matrix[i, 0] = i;
        for (int j = 0; j <= lenB; j++) matrix[0, j] = j;
        for (int i = 1; i <= lenA; i++)
        for (int j = 1; j <= lenB; j++)
        {
            var cost = a[i - 1] == b[j - 1] ? 0 : 1;
            matrix[i, j] = System.Math.Min(
                System.Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
                matrix[i - 1, j - 1] + cost);
        }
        return matrix[lenA, lenB];
    }

    internal bool ShouldExit(string input)
    {
        var lower = input.ToLowerInvariant().Trim();
        return _botCommands.Contains(lower);
    }

    internal double BotRenameAcceptProbability { get; set; } = 0.85;

    internal bool TryHandleBotRename(string input, out string response)
    {
        var lowerInput = input.ToLowerInvariant();

        foreach (var pattern in _renamePatterns)
        {
            var idx = lowerInput.IndexOf(pattern, StringComparison.Ordinal);
            if (idx < 0) continue;

            var namePart = input.Substring(idx + pattern.Length).Trim();
            var namePartLower = namePart.ToLowerInvariant();
            if (namePartLower.StartsWith("your"))
                continue;
            var nameTokens = _tokeniser.Tokenise(namePart);
            var candidate = nameTokens[0];
            if (nameTokens.Count == 0 || IsStopWord(candidate) || PunctuationHelper.IsPunctuation(candidate) ||
                candidate.Length < 2 || !candidate.All(char.IsLetter))
                continue;

            response = HandleBotRenameProposal(nameTokens[0]);
            return true;
        }

        response = string.Empty;
        return false;
    }

    private string HandleBotRenameProposal(string proposedName)
    {
        var displayName = char.ToUpper(proposedName[0]) + proposedName.Substring(1).ToLowerInvariant();

        if (Random.Shared.NextDouble() < BotRenameAcceptProbability)
        {
            _knowledgeStore.SetUserBotName(_currentUserId!.Value, displayName);
            _knowledgeStore.Save();
            _botName = displayName;
            _responseEngine.SetBotName(_botName);
            return GetBotRenameResponse("bot_rename_accepted", displayName);
        }

        if (Random.Shared.Next(2) == 0)
        {
            var altName = AlternativeNames[Random.Shared.Next(AlternativeNames.Length)];
            return GetBotRenameResponse("bot_rename_suggestion", altName);
        }

        return GetBotRenameResponse("bot_rename_rejected", displayName);
    }

    private string GetBotRenameResponse(string category, params object[] args)
    {
        var botResponses = GetCachedBotResponses();
        if (botResponses.TryGetValue(category, out var responses) && responses.Count > 0)
        {
            var template = responses[Random.Shared.Next(responses.Count)];
            return args.Length > 0 ? string.Format(template, args) : template;
        }

        var fallbacks = new Dictionary<string, List<string>>
        {
            ["bot_rename_accepted"] = new() { $"Okay, from now on you can call me {args[0]}!" },
            ["bot_rename_rejected"] = new() { $"Hmm, I'm not sure {args[0]} suits me. Can you think of something else?" },
            ["bot_rename_suggestion"] = new() { $"How about the name {args[0]}?" }
        };

        if (fallbacks.TryGetValue(category, out var fb) && fb.Count > 0)
        {
            var template = fb[Random.Shared.Next(fb.Count)];
            return args.Length > 0 ? string.Format(template, args) : template;
        }

        return string.Empty;
    }

    internal bool TryHandleResetRequest(string input, out string response)
    {
        var pendingReset = _context.GetContext(ContextKeys.PendingReset);

        if (pendingReset != null)
        {
            _context.SetContext(ContextKeys.PendingReset, null);

            if (Affirmations.Contains(input.Trim().ToLowerInvariant()))
            {
                _knowledgeStore.ResetAllUserData();
                _context.Clear();
                _currentUserName = string.Empty;
                _currentUserNameLower = string.Empty;
                _currentUserId = null;
                _responseEngine.SetCurrentUserName(string.Empty);
                response = GetResetResponse("bot_reset_confirmed");
                return true;
            }

            response = GetResetResponse("bot_reset_cancelled");
            return true;
        }

        var lowerInput = input.ToLowerInvariant();
        foreach (var trigger in ResetTriggers)
        {
            if (lowerInput.Contains(trigger))
            {
                _context.SetContext(ContextKeys.PendingReset, "true");
                response = GetResetResponse("bot_reset_warning");
                return true;
            }
        }

        response = string.Empty;
        return false;
    }

    private string GetResetResponse(string category)
    {
        var botResponses = GetCachedBotResponses();
        if (botResponses.TryGetValue(category, out var responses) && responses.Count > 0)
        {
            return responses[Random.Shared.Next(responses.Count)];
        }

        var fallbacks = new Dictionary<string, List<string>>
        {
            ["bot_reset_warning"] = new()
            {
                "This will delete all our conversations and everything I've learned from you. Are you sure?",
                "Are you sure you want me to forget everything we've talked about?",
            },
            ["bot_reset_confirmed"] = new()
            {
                "Done! I've forgotten everything. Let's start fresh!",
                "All memories cleared. It's like we're meeting for the first time!",
            },
            ["bot_reset_cancelled"] = new()
            {
                "Okay, nothing was deleted. Let's continue!",
                "No problem, I'll keep our memories safe!",
            },
        };

        if (fallbacks.TryGetValue(category, out var fb) && fb.Count > 0)
        {
            return fb[Random.Shared.Next(fb.Count)];
        }

        return string.Empty;
    }

    internal string? StartInterview()
    {
        var interviewerId = _knowledgeStore.GetOrCreateUser("Interviewer");
        if (interviewerId == null) return null;
        _savedUserId = _currentUserId;
        _currentUserId = interviewerId.Value;
        _botName = "PokeChat";
        _responseEngine.SetBotName(_botName);

        _context.Clear();
        _pendingFollowUp = null;
        _followUpCount = 0;

        if (_llmOrchestrator?.IsAvailable == true)
        {
            _interviewEngine = new InterviewEngine(_llmOrchestrator, _knowledgeStore, _nounCategoriser);
        }
        else
        {
            _interviewEngine = new NonLlmInterviewEngine(_knowledgeStore, _nounCategoriser);
        }

        _interviewModeActive = true;

        var intro = GetInterviewResponse("interview_intro") ?? "Interview mode started! I'll chat with my AI to learn new things. Type 'stop' to end.";
        _sessionLogger?.LogSystem($"[Interview mode started] User: {_currentUserName}, Interviewer ID: {interviewerId}");
        return intro;
    }

    internal string? EndInterview()
    {
        if (!_interviewModeActive) return null;

        _interviewModeActive = false;
        _pendingFollowUp = null;
        _followUpCount = 0;
        _currentUserId = _savedUserId;
        if (_currentUserName != null)
            _responseEngine.SetCurrentUserName(_currentUserName);
        _responseEngine.SetBotName(_botName);
        _context.Clear();
        _knowledgeStore.Save();

        var facts = _interviewEngine?.FactsLearned ?? 0;
        var rules = _interviewEngine?.RulesLearned ?? 0;
        var summary = GetInterviewResponse("interview_complete") ?? "Interview finished! I learned {0} new facts and {1} new rules.";
        _sessionLogger?.LogSystem($"[Interview ended] Facts: {facts}, Rules: {rules}");

        _interviewEngine?.Reset();
        _interviewEngine = null;
        return string.Format(summary, facts, rules);
    }

    private string? GetInterviewResponse(string category)
    {
        var botResponses = GetCachedBotResponses();
        if (botResponses.TryGetValue(category, out var responses) && responses.Count > 0)
            return responses[Random.Shared.Next(responses.Count)];
        return null;
    }

    internal bool IsInterviewTrigger(string input)
    {
        var lower = input.ToLowerInvariant().Trim();
        return InterviewStartPhrases.Any(phrase => lower.Contains(phrase));
    }

    internal bool IsInterviewStopCommand(string input)
    {
        var lower = input.ToLowerInvariant().Trim();
        return InterviewStopPhrases.Contains(lower);
    }

    private static bool IsInterviewFollowUp(string response)
    {
        if (string.IsNullOrEmpty(response)) return false;
        var lower = response.ToLowerInvariant();

        if (lower.Contains('?')) return true;

        return lower.Contains("tell me more") ||
               lower.Contains("what else") ||
               lower.Contains("anything else") ||
               lower.Contains("how about") ||
               lower.Contains("what about") ||
               lower.StartsWith("why");
    }

    private string GetLLMResponse(string category)
    {
        var botResponses = GetCachedBotResponses();
        if (botResponses.TryGetValue(category, out var responses) && responses.Count > 0)
            return responses[Random.Shared.Next(responses.Count)];

        var fallbacks = new Dictionary<string, List<string>>
        {
            ["llm_offer"] = new() { "I don't know how to answer that. Should I use my AI to respond?" },
            ["llm_declined"] = new() { "No problem, I'll keep learning!" },
            ["llm_unavailable"] = new() { "My AI isn't responding right now." },
            ["llm_thinking"] = new() { "Let me check with my AI..." },
            ["persona_switch_chat"] = new() { "Switched to chat mode. I'm PokeChat again!" },
            ["persona_switch_coding"] = new() { "Switched to coding mode. I'm PokeCode — ready to help with code." },
            ["coding_confirmation_prompt"] = new() { "Are you sure you want to run that command, {name}? (yes/no)", "That could be destructive, {name}. Are you sure? (yes/no)" },
            ["coding_confirmation_denied"] = new() { "Cancelled, {name}.", "Command cancelled as requested, {name}." },
        };

        if (fallbacks.TryGetValue(category, out var fb) && fb.Count > 0)
            return fb[Random.Shared.Next(fb.Count)];

        return string.Empty;
    }

    private string ExecuteBotRoute(RouteResult route)
    {
        switch (route.Handler)
        {
            case RouteHandler.Math:
                var mathEngine = new SimpleMath();
                var mathResult = mathEngine.Evaluate(route.Argument ?? "");
                if (mathResult != null)
                {
                    var botResponses = GetCachedBotResponses();
                    if (botResponses.TryGetValue("math_result", out var mathTemplates) && mathTemplates.Count > 0)
                        return string.Format(mathTemplates[Random.Shared.Next(mathTemplates.Count)], mathResult.Expression, mathResult.Value);
                    return $"{mathResult.Expression} = {mathResult.Value}";
                }
                return GetLLMResponse("math_parse_error");

            case RouteHandler.Remind:
                var remindInput = "remind " + (route.Argument ?? "");
                if (TryHandleReminderRequest(remindInput, out var remindResponse))
                    return remindResponse;
                return GetLLMResponse("default_response");

            case RouteHandler.Story:
                return _responseEngine.GenerateResponse("tell me a story", _currentUserId);

            case RouteHandler.Poem:
                return _responseEngine.GenerateResponse("write a poem", _currentUserId);

            case RouteHandler.Haiku:
                return _responseEngine.GenerateResponse("write a haiku", _currentUserId);

            case RouteHandler.Limerick:
                return _responseEngine.GenerateResponse("write a limerick", _currentUserId);

            case RouteHandler.Joke:
                if (TryHandleJokeStart("tell me a joke", out var jokeResponse))
                    return jokeResponse;
                return GetLLMResponse("default_response");

            case RouteHandler.Riddle:
                if (TryHandleRiddleStart("tell me a riddle", out var riddleResponse))
                    return riddleResponse;
                return GetLLMResponse("default_response");

            case RouteHandler.Quiz:
                if (TryHandleQuizStart("quiz me", out var quizResponse))
                    return quizResponse;
                return GetLLMResponse("default_response");

            case RouteHandler.Game:
                if (TryHandleGameStart("word game", out var gameResponse))
                    return gameResponse;
                return GetLLMResponse("default_response");

            case RouteHandler.Hangman:
                if (TryHandleHangmanStart("play hangman", out var hangmanResponse))
                    return hangmanResponse;
                return GetLLMResponse("default_response");

            case RouteHandler.SwitchPersona:
                var switchInput = "switch to " + (route.Argument ?? "") + " mode";
                var personaResult = TryHandlePersonaSwitch(switchInput);
                if (personaResult != null)
                    return personaResult;
                return GetLLMResponse("default_response");

            case RouteHandler.Stats:
                var statsResponse = _responseEngine.HandleSelfKnowledgeRequest("what are my stats", _currentUserId);
                if (statsResponse != null)
                    return statsResponse;
                return GetLLMResponse("default_response");

            case RouteHandler.AboutMe:
                var aboutResponse = _responseEngine.HandleSelfKnowledgeRequest("tell me about myself", _currentUserId);
                if (aboutResponse != null)
                    return aboutResponse;
                return GetLLMResponse("default_response");

            case RouteHandler.Reset:
                if (TryHandleResetRequest("start fresh", out var resetResponse))
                    return resetResponse;
                return GetLLMResponse("default_response");

            case RouteHandler.Help:
                return GetHelpText();

            default:
                return GetLLMResponse("default_response");
        }
    }

    internal static string GetHelpText()
    {
        return "Here are the commands I understand:\n\n"
            + "~maths <expression> — Evaluate a maths expression (e.g. ~maths 2 + 2)\n"
            + "~remind <me to> <task> [at/in <time>] — Set a reminder\n"
            + "~story — Tell me a story\n"
            + "~poem — Write a poem\n"
            + "~haiku — Write a haiku\n"
            + "~limerick — Write a limerick\n"
            + "~joke — Tell me a joke\n"
            + "~riddle — Ask me a riddle\n"
            + "~quiz — Quiz me on what I know\n"
            + "~game — Play the word-chain story game\n"
            + "~hangman — Play hangman\n"
            + "~switch <mode> — Switch to coding or chat mode\n"
            + "~stats — Show conversation statistics\n"
            + "~about — Tell me what I know about you\n"
            + "~reset — Reset all my memories of you\n"
            + "~help — Show this help message";
    }

    private string? TryEarlyLlmRouting(string input)
    {
        if (_currentUserId == null) return null;
        if (_llmOrchestrator == null || !_llmOrchestrator.IsAvailable || _llmOrchestrator.UserDeclined)
            return null;
        if (!_intentClassifier.IsReady)
            return null;

        var probs = _intentClassifier.PredictProbabilities(input);
        var maxConf = probs.Length > 0 ? probs.Max() : 0f;
        var intent = _intentClassifier.Classify(input);

        var needsLlm = (intent == "complex_question" || intent == "unknown" || maxConf < 0.5f);
        if (!needsLlm)
            return null;

        if (_llmOrchestrator.Config.AlwaysOn || _llmOrchestrator.IsAccepted)
        {
            var llmResult = LlmCallWithIndicator(() => _llmOrchestrator.GenerateResponse(input));
            if (llmResult != null)
            {
                LearnFromLLMResponse(input, llmResult);
                BufferLlmInteraction(input, llmResult);
                _context.SetContext(ContextKeys.CurrentResponseCategory, "llm_response");
                LastResponseCategory = "llm_response";
                _knowledgeStore.StoreConversation(_currentUserId.Value, input, llmResult, _sessionId, "llm_response");
                _knowledgeStore.Save();
                return llmResult;
            }
            return GetLLMResponse("llm_unavailable");
        }

        if (_context.GetContext(ContextKeys.PendingLLMOffer) == null)
        {
            _context.SetContext(ContextKeys.PendingLLMOffer, "true");
            _context.SetContext(ContextKeys.LLMOriginalInput, input);
        }

        return null;
    }

    private void BufferLlmInteraction(string input, string llmResponse)
    {
        _trainingBuffer.Add((input, llmResponse));
    }

    internal void TryRetrainClassifier()
    {
        if (_trainingBuffer.Count < RetrainThreshold) return;
        if (_llmOrchestrator == null || !_llmOrchestrator.IsAvailable) return;
        if (!_intentClassifier.IsReady) return;

        try
        {
            var bufferItems = _trainingBuffer.Select(t => new { input = t.Input, response = t.Response }).ToList();
            var conversationJson = JsonSerializer.Serialize(bufferItems);
            var labelsJson = _llmOrchestrator.GenerateTrainingLabels(conversationJson);
            if (string.IsNullOrEmpty(labelsJson)) return;

            using var doc = JsonDocument.Parse(labelsJson);
            var labels = doc.RootElement.GetProperty("labels").EnumerateArray()
                .Select(e => (Input: e.GetProperty("input").GetString()!, Intent: e.GetProperty("intent").GetString()!))
                .Where(x => !string.IsNullOrEmpty(x.Input) && !string.IsNullOrEmpty(x.Intent))
                .ToList();

            if (labels.Count < 5) return;

            var knownCategories = new HashSet<string>(ML.IntentCategory.DefaultCategories);
            var newExamples = labels
                .Where(l => knownCategories.Contains(l.Intent))
                .Select(l => (l.Input, l.Intent))
                .Distinct()
                .ToList();

            if (newExamples.Count < 5) return;

            var allExamples = ML.SeedTrainingData.Examples.Concat(newExamples).ToList();
            _intentClassifier.Train(allExamples);
            _intentClassifier.SaveModel();
            _trainingBuffer.Clear();
        }
        catch
        {
        }
    }

    private void LearnFromLLMResponse(string input, string llmResponse)
    {
        var tokens = _tokeniser.Tokenise(input);
        var correctedTokens = _spellChecker.AutoCorrect(tokens);
        var tags = _posTagger.Tag(correctedTokens);
        var triples = _svoExtractor.Extract(correctedTokens, tags);

        string pattern;

        if (triples.Count > 0)
        {
            var obj = triples[0].Object;
            if (string.IsNullOrEmpty(obj) || obj.Length < 2)
                obj = triples[0].Subject;

            var patternTokens = correctedTokens.Select(t =>
                string.Equals(t, obj, StringComparison.OrdinalIgnoreCase) ? @"(\w+)" : Regex.Escape(t));
            pattern = @"\b" + string.Join(@"\b \b", patternTokens) + @"\b";
        }
        else
        {
            pattern = @"\b" + string.Join(@"\b \b", correctedTokens.Select(Regex.Escape)) + @"\b";
        }

        if (!_knowledgeStore.IsLearnedRuleKnown(pattern))
        {
            _knowledgeStore.LearnResponseRule(pattern, llmResponse, "Statement", _currentUserId);
            _knowledgeStore.Save();
        }
    }

    internal bool TryHandleCorrection(string input, out string response)
    {
        var trimmedInput = input.Trim();

        var youShouldMatch = Regex.Match(trimmedInput, @"^you should say\s+(.+?)$", RegexOptions.IgnoreCase);
        if (youShouldMatch.Success)
            return LearnFromCorrection(youShouldMatch.Groups[1].Value, out response);

        var sayInsteadMatch = Regex.Match(trimmedInput, @"^say\s+(.+?)\s+instead$", RegexOptions.IgnoreCase);
        if (sayInsteadMatch.Success)
            return LearnFromCorrection(sayInsteadMatch.Groups[1].Value, out response);

        var trySayingMatch = Regex.Match(trimmedInput, @"^try saying\s+(.+?)$", RegexOptions.IgnoreCase);
        if (trySayingMatch.Success)
            return LearnFromCorrection(trySayingMatch.Groups[1].Value, out response);

        var pairMatch = Regex.Match(trimmedInput, @"^(?:when I say|if I say)\s+(.+?)\s+(?:you should say|you could say|you should|you could)\s+(.+?)$", RegexOptions.IgnoreCase);
        if (pairMatch.Success)
        {
            var triggerPattern = pairMatch.Groups[1].Value.Trim();
            var responseTemplate = pairMatch.Groups[2].Value.Trim().Trim('.', '!', '?');
            if (triggerPattern.Length > 0 && responseTemplate.Length > 0)
            {
                if (_knowledgeStore.IsLearnedRuleKnown(triggerPattern))
                    return GetCorrectionResponse("pattern_already_known", out response);

                _knowledgeStore.LearnResponseRule(triggerPattern, responseTemplate, "Statement", _currentUserId);
                _knowledgeStore.Save();
                if (AlwaysOnLLmAvailable())
                    return GetLLMCorrectionReflection(triggerPattern, responseTemplate, out response);
                return GetCorrectionResponse("pattern_learned", out response);
            }
        }

        var lastRuleIdRaw = _context.GetContext(ContextKeys.LastRuleId);
        if (string.IsNullOrEmpty(lastRuleIdRaw))
        {
            response = string.Empty;
            return false;
        }

        var lastRuleId = int.Parse(lastRuleIdRaw);
        var isLearned = _context.GetContext(ContextKeys.LastRuleIsLearned) == "true";

        var lowerInput = trimmedInput.ToLowerInvariant();
        if (lowerInput is "that's not right" or "that is not right" or "wrong" ||
            lowerInput.Contains("not what i meant") || lowerInput.Contains("not helpful"))
        {
            _knowledgeStore.RecordFeedback(lastRuleId, _currentUserId!.Value, "negative", isLearned);
            _knowledgeStore.AdjustConfidence(lastRuleId, -2, isLearned);
            _knowledgeStore.Save();
            _context.SetContext(ContextKeys.LastRuleId, null);
            if (AlwaysOnLLmAvailable())
                return GetLLMCorrectionReflection("you", "not right", out response);
            return GetCorrectionResponse("pattern_acknowledged", out response);
        }

        if (lowerInput is "that's exactly right" or "now you've got it" or "yes, that's it" or "perfect" || lowerInput.Contains("that's better"))
        {
            _knowledgeStore.RecordFeedback(lastRuleId, _currentUserId!.Value, "positive", isLearned);
            _knowledgeStore.AdjustConfidence(lastRuleId, 1, isLearned);
            _knowledgeStore.Save();
            _context.SetContext(ContextKeys.LastRuleId, null);
            if (AlwaysOnLLmAvailable())
                return GetLLMCorrectionReflection("you", "right this time", out response);
            return GetCorrectionResponse("pattern_acknowledged", out response);
        }

        response = string.Empty;
        return false;
    }

    private bool AlwaysOnLLmAvailable() =>
        _llmOrchestrator?.Config.AlwaysOn == true && _llmOrchestrator.IsAvailable && !_llmOrchestrator.UserDeclined;

    private string? LlmCallWithIndicator(Func<string?> llmCall)
    {
        OnStatusUpdate?.Invoke("thinking");
        var result = llmCall();
        OnStatusUpdate?.Invoke("clear");
        return result;
    }

    private bool GetLLMCorrectionReflection(string trigger, string template, out string response)
    {
        var prompt = $"The user just taught you: when they say '{trigger}', you should respond like '{template}'. " +
            "Acknowledge this naturally in 1 sentence — like 'Got it, I'll do that next time' or 'Thanks, that makes sense'. " +
            "Do not over-explain. Be natural.";
        var llmResult = LlmCallWithIndicator(() => _llmOrchestrator!.GenerateResponse(prompt));
        if (!string.IsNullOrEmpty(llmResult))
        {
            response = llmResult;
            return true;
        }
        return GetCorrectionResponse("pattern_acknowledged", out response);
    }

    private bool LearnFromCorrection(string templateRaw, out string response)
    {
        var template = templateRaw.Trim().Trim('.', '!', '?');
        if (template.Length == 0)
            return GetCorrectionResponse("pattern_not_clear", out response);

        var lastInput = _context.GetContext(ContextKeys.LastUserInput);
        var pattern = ExtractPatternFromLastInput(lastInput);
        if (pattern == null)
            return GetCorrectionResponse("pattern_not_clear", out response);

        if (_knowledgeStore.IsLearnedRuleKnown(pattern))
            return GetCorrectionResponse("pattern_already_known", out response);

        _knowledgeStore.LearnResponseRule(pattern, template, "Statement", _currentUserId);
        _knowledgeStore.Save();
        return GetCorrectionResponse("pattern_learned", out response);
    }

    private static string? ExtractPatternFromLastInput(string? lastInput)
    {
        if (string.IsNullOrEmpty(lastInput)) return null;
        var lower = lastInput.ToLowerInvariant().Trim();
        lower = Regex.Replace(lower, @"[^\w\s]", "");
        var lastWord = lower.Split(' ', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
        if (string.IsNullOrEmpty(lastWord) || lastWord.Length < 2)
            return null;
        return $@"\b{Regex.Escape(lastWord)}\b";
    }

    private bool GetCorrectionResponse(string category, out string response)
    {
        var botResponses = GetCachedBotResponses();
        if (botResponses.TryGetValue(category, out var responses) && responses.Count > 0)
        {
            response = responses[Random.Shared.Next(responses.Count)];
            return true;
        }

        response = category switch
        {
            "pattern_learned" => "Got it! I'll remember to say that next time.",
            "pattern_acknowledged" => "Thanks for the feedback. I'll try to do better.",
            "pattern_not_clear" => "I'm not sure what you want me to say instead. Can you give me an example?",
            "pattern_already_known" => "I already know that one! But thanks for the reminder.",
            _ => string.Empty
        };
        return true;
    }

    internal bool TryHandleMetaCommentary(string input, out string? response)
    {
        response = null;
        var lower = input.Trim().ToLowerInvariant();

        if (lower.Length < 8)
            return false;

        string? category = null;

        if (MetaConfusionTriggers.Any(t => lower.Contains(t)))
            category = "complaint_acknowledged";
        else if (MetaNotHelpfulTriggers.Any(t => lower.Contains(t)))
            category = "complaint_acknowledged";
        else if (MetaMockingTriggers.Any(t => lower.Contains(t)))
            category = "complaint_acknowledged";
        else
            return false;

        var complaintCountStr = _context.GetContext(ContextKeys.LastComplaint);
        var complaintCount = 0;
        if (complaintCountStr != null && int.TryParse(complaintCountStr, out var cc))
            complaintCount = cc;
        complaintCount++;
        _context.SetContext(ContextKeys.LastComplaint, complaintCount.ToString());

        if (complaintCount >= 3)
            category = "meta_repeated_complaint";

        var botResponses = GetCachedBotResponses();
        if (botResponses.TryGetValue(category, out var responses) && responses.Count > 0)
        {
            response = responses[Random.Shared.Next(responses.Count)];
            return true;
        }

        response = category == "meta_repeated_complaint"
            ? "I'm sorry I keep getting things wrong. Let's start fresh — what would you like to discuss?"
            : "I'm sorry, that wasn't helpful. Let me try again.";
        return true;
    }

    private bool TryHandleInsult(string input, string? sentiment, int intensity, out string response)
    {
        if (InsultPattern.IsMatch(input.Trim()))
        {
            response = GetCachedBotResponses().TryGetValue("direct_insult", out var responses) && responses.Count > 0
                ? responses[Random.Shared.Next(responses.Count)]
                : "That's not very nice. Let's keep things friendly.";
            return true;
        }

        if (sentiment is "anger" or "negative" && intensity >= 2)
        {
            var lower = input.ToLowerInvariant().Trim();
            if (lower.Contains("you") && lower.Contains("hate") ||
                lower.Contains("you") && lower.Contains("idiot") ||
                lower.Contains("you") && lower.Contains("stupid") ||
                lower.Contains("you") && lower.Contains("dumb"))
            {
                response = GetCachedBotResponses().TryGetValue("direct_insult", out var responses) && responses.Count > 0
                    ? responses[Random.Shared.Next(responses.Count)]
                    : "That's not very nice. Let's keep things friendly.";
                return true;
            }
        }

        response = string.Empty;
        return false;
    }

    internal string GenerateSessionEndSummary()
    {
        if (_currentUserId == null) return string.Empty;

        var summary = _knowledgeStore.BuildSessionSummary(_currentUserId.Value, _sessionId);
        if (string.IsNullOrEmpty(summary)) return string.Empty;

        var botResponses = GetCachedBotResponses();
        if (botResponses.TryGetValue("session_summary_end", out var responses) && responses.Count > 0)
        {
            var template = responses[Random.Shared.Next(responses.Count)];
            return string.Format(template, summary);
        }

        return $"Before you go — today we talked about {summary}. See you next time!";
    }

    private Dictionary<string, string> BuildLogContext()
    {
        var ctx = new Dictionary<string, string>
        {
            ["sentiment"] = _context.GetContext(ContextKeys.CurrentSentiment) ?? "neutral",
            ["intensity"] = _context.GetContext(ContextKeys.LastSentimentIntensity) ?? "0",
            ["response_category"] = _context.GetContext(ContextKeys.CurrentResponseCategory) ?? "unknown",
            ["last_rule_id"] = _context.GetContext(ContextKeys.LastRuleId) ?? "none",
            ["last_rule_is_learned"] = _context.GetContext(ContextKeys.LastRuleIsLearned) ?? "false",
            ["unknown_words"] = _context.GetContext(ContextKeys.UnknownWords) ?? "none",
            ["context_follow_up_count"] = _context.GetContext(ContextKeys.ContextFollowUpCount) ?? "0",
            ["last_subject"] = _context.LastSubject ?? "none",
            ["last_object"] = _context.LastObject ?? "none",
        };

        return ctx;
    }

    internal bool TryHandleGameStart(string input, out string response)
    {
        var lowerInput = input.ToLowerInvariant().Trim();
        foreach (var phrase in GameStartPhrases)
        {
            if (lowerInput.Contains(phrase))
            {
                var existingGame = _context.GetContext(ContextKeys.GameModeActive);
                if (existingGame != null)
                {
                    response = GetGameResponse("game_already_active");
                    return true;
                }

                response = StartGame();
                return true;
            }
        }

        response = string.Empty;
        return false;
    }

    private string StartGame()
    {
        var startWord = GameStartWords[Random.Shared.Next(GameStartWords.Length)];
        _context.SetContext(ContextKeys.GameModeActive, "true");
        _context.SetContext(ContextKeys.GameStory, startWord);
        _context.SetContext(ContextKeys.GameTurnCount, "0");
        return GetGameResponse("game_start", startWord);
    }

    internal string HandleGameTurn(string input)
    {
        var lowerInput = input.Trim().ToLowerInvariant();

        foreach (var phrase in GameEndPhrases)
        {
            if (lowerInput.Contains(phrase) || lowerInput.Equals(phrase))
                return HandleGameEnd();
        }

        var story = _context.GetContext(ContextKeys.GameStory) ?? "";
        var turnCountRaw = _context.GetContext(ContextKeys.GameTurnCount) ?? "0";
        int.TryParse(turnCountRaw, out var turnCount);

        var userTokens = _tokeniser.Tokenise(input);
        if (userTokens.Count == 0)
            return "Add one word!";

        var userWord = userTokens[0].ToLowerInvariant();
        story = string.IsNullOrEmpty(story) ? userWord : story + " " + userWord;

        turnCount++;

        if (turnCount >= 50)
            return HandleGameEnd();

        string? botWord;
        if (_llmOrchestrator?.IsAvailable == true && turnCount % 2 == 0)
        {
            OnStatusUpdate?.Invoke("thinking");
            botWord = _llmOrchestrator.GenerateWordForGame(story);
            OnStatusUpdate?.Invoke("clear");
            if (string.IsNullOrEmpty(botWord))
                botWord = PickGameWord(story);
        }
        else
        {
            botWord = PickGameWord(story);
        }

        story = story + " " + botWord;

        _context.SetContext(ContextKeys.GameStory, story);
        _context.SetContext(ContextKeys.GameTurnCount, turnCount.ToString());

        return GetGameResponse("game_turn_word_and_prompt", botWord);
    }

    private string HandleGameEnd()
    {
        var story = _context.GetContext(ContextKeys.GameStory) ?? "";
        _context.SetContext(ContextKeys.GameModeActive, null);
        _context.SetContext(ContextKeys.GameStory, null);
        _context.SetContext(ContextKeys.GameTurnCount, null);

        var filteredStory = ApplyGameGrammarFilter(story);

        if (_llmOrchestrator?.IsAvailable == true && !string.IsNullOrEmpty(filteredStory))
        {
            var llmSummary = LlmCallWithIndicator(() => _llmOrchestrator.GenerateGameStorySummary(filteredStory));
            if (!string.IsNullOrEmpty(llmSummary))
                return GetGameResponse("game_stop_llm", filteredStory, llmSummary);
        }

        return GetGameResponse("game_stop", filteredStory);
    }

    internal string ApplyGameGrammarFilter(string rawStory)
    {
        if (string.IsNullOrWhiteSpace(rawStory))
            return rawStory;

        var words = rawStory.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        if (words.Count == 0)
            return rawStory;

        // Step 1: Trim trailing Conjunction/Preposition/Determiner
        while (words.Count > 0)
        {
            var lastWord = words[^1].ToLowerInvariant();
            var tags = _posTagger.Tag(new List<string> { lastWord });
            var pos = tags.Count > 0 ? tags[0] : PosTag.Unknown;
            if (pos == PosTag.Conjunction || pos == PosTag.Preposition || pos == PosTag.Determiner)
                words.RemoveAt(words.Count - 1);
            else
                break;
        }

        if (words.Count == 0)
            return rawStory;

        // Step 2: Collapse consecutive duplicate words
        var collapsed = new List<string>();
        foreach (var word in words)
        {
            if (collapsed.Count == 0 || !string.Equals(collapsed[^1], word, StringComparison.OrdinalIgnoreCase))
                collapsed.Add(word);
        }
        words = collapsed;

        // Step 3: Sentence-split at and/but/so where each side >= 5 words
        var afterSplit = new List<string>();
        for (int i = 0; i < words.Count; i++)
        {
            var lower = words[i].ToLowerInvariant();
            if ((lower == "and" || lower == "but" || lower == "so") &&
                i >= 5 && (words.Count - i - 1) >= 5)
            {
                afterSplit.Add(".");
                continue;
            }
            afterSplit.Add(words[i]);
        }
        words = afterSplit;

        // Step 4: Comma after introductory words
        var introWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "once", "suddenly", "then", "finally", "meanwhile" };
        var withIntroCommas = new List<string>();
        foreach (var word in words)
        {
            if (introWords.Contains(word))
                withIntroCommas.Add(word + ",");
            else
                withIntroCommas.Add(word);
        }
        words = withIntroCommas;

        // Step 5: Comma before and/but/so (non-split points, each side >= 3 words)
        var withConjCommas = new List<string>();
        for (int i = 0; i < words.Count; i++)
        {
            var lower = words[i].ToLowerInvariant();
            if ((lower == "and" || lower == "but" || lower == "so") &&
                i >= 3 && (words.Count - i - 1) >= 3)
            {
                if (withConjCommas.Count > 0 && withConjCommas[^1] != "." && withConjCommas[^1] != ",")
                    withConjCommas.Add(",");
            }
            withConjCommas.Add(words[i]);
        }
        words = withConjCommas;

        // Step 6: a -> an before vowel
        var vowels = new HashSet<char> { 'a', 'e', 'i', 'o', 'u' };
        for (int i = 0; i < words.Count - 1; i++)
        {
            if (words[i].ToLowerInvariant() == "a" && words[i + 1].Length > 0 &&
                vowels.Contains(char.ToLowerInvariant(words[i + 1][0])))
            {
                words[i] = "an";
            }
        }

        // Step 7: Capitalize first letter of each sentence
        var result = new List<string>();
        var capitalizeNext = true;
        foreach (var word in words)
        {
            if (capitalizeNext && word.Length > 0)
                result.Add(char.ToUpperInvariant(word[0]) + word.Substring(1));
            else
                result.Add(word);
            capitalizeNext = word == ".";
        }

        // Step 8: Trailing period if missing
        var text = string.Join(" ", result);
        text = text.Replace(" , ", ", ").Replace(" . ", ". ").Replace(" .", ".");
        if (!text.EndsWith(".") && !text.EndsWith("!") && !text.EndsWith("?"))
            text += ".";

        return text;
    }

    private string PickGameWord(string storySoFar)
    {
        var words = storySoFar.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var lastWord = words.Length > 0 ? words[^1] : string.Empty;
        if (string.IsNullOrEmpty(lastWord)) return "the";

        var lastWordTags = _posTagger.Tag(new List<string> { lastWord.ToLowerInvariant() });
        var lastPos = lastWordTags.Count > 0 ? lastWordTags[0] : PosTag.Unknown;

        var preferredTypes = lastPos switch
        {
            PosTag.Determiner => new[] { "adjective", "noun" },
            PosTag.Adjective => new[] { "noun" },
            PosTag.Noun => new[] { "verb", "preposition", "adverb", "conjunction" },
            PosTag.Verb => new[] { "determiner", "adverb", "preposition", "noun" },
            PosTag.Adverb => new[] { "verb", "adjective" },
            PosTag.Preposition => new[] { "determiner", "adjective", "noun" },
            PosTag.Pronoun => new[] { "verb", "adverb" },
            PosTag.Conjunction => new[] { "determiner", "pronoun", "noun" },
            _ => new[] { "noun", "verb", "adjective" }
        };

        var lastTwo = words.Length >= 2
            ? words[^2..].Select(w => w.ToLowerInvariant()).ToHashSet(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>();

        foreach (var type in preferredTypes)
        {
            var candidates = _dbContext.PosDictionary
                .Where(e => e.WordType == type)
                .Select(e => e.Word)
                .ToList()
                .Where(w => !lastTwo.Contains(w))
                .ToList();
            if (candidates.Count > 0)
                return candidates[Random.Shared.Next(candidates.Count)];
        }

        var fallback = _dbContext.PosDictionary
            .Select(e => e.Word)
            .ToList()
            .Where(w => !lastTwo.Contains(w))
            .ToList();
        if (fallback.Count > 0)
            return fallback[Random.Shared.Next(fallback.Count)];

        return "the";
    }

    private string GetGameResponse(string category, params object[] args)
    {
        var botResponses = GetCachedBotResponses();
        if (botResponses.TryGetValue(category, out var responses) && responses.Count > 0)
        {
            var template = responses[Random.Shared.Next(responses.Count)];
            return args.Length > 0 ? string.Format(template, args) : template;
        }

        var fallbacks = new Dictionary<string, List<string>>
        {
            ["game_start"] = new() { $"Let's play a word game! We take turns adding one word at a time to build a funny story. I'll start: {{0}}" },
            ["game_turn_word_and_prompt"] = new() { $"{{0}} Add one word!" },
            ["game_stop"] = new() { $"That was fun! Here's our story:\n{{0}}" },
            ["game_stop_llm"] = new() { $"Here's what we came up with:\n{{0}}\n\nAnd here's a story from those words:\n{{1}}" },
            ["game_already_active"] = new() { "We're already playing! Just add one word, or say 'stop game' to end." },
        };

        if (fallbacks.TryGetValue(category, out var fb) && fb.Count > 0)
        {
            var template = fb[Random.Shared.Next(fb.Count)];
            return args.Length > 0 ? string.Format(template, args) : template;
        }

        return string.Empty;
    }

    internal bool TryHandleMadLibsStart(string input, out string response)
    {
        var lowerInput = input.ToLowerInvariant().Trim();
        foreach (var phrase in MadLibsStartPhrases)
        {
            if (lowerInput.Contains(phrase))
            {
                var existing = _context.GetContext(ContextKeys.MadLibsActive);
                if (existing != null)
                {
                    response = GetMadLibResponse("mad_libs_already_active");
                    return true;
                }

                var gameExisting = _context.GetContext(ContextKeys.GameModeActive);
                if (gameExisting != null)
                {
                    response = "You're already in the middle of a word game! Say 'stop game' first.";
                    return true;
                }

                response = StartMadLibs();
                return true;
            }
        }

        response = string.Empty;
        return false;
    }

    private string StartMadLibs()
    {
        var template = _knowledgeStore.GetRandomMadLibTemplate();
        if (template == null)
            return "I don't have any Mad Libs templates yet!";

        var slots = GetMadLibSlots(template.Template);
        if (slots.Count == 0)
            return "That template seems empty. Let's try something else!";

        _context.SetContext(ContextKeys.MadLibsActive, "true");
        _context.SetContext(ContextKeys.MadLibsTemplateId, template.Id.ToString());
        _context.SetContext(ContextKeys.MadLibsSlotIndex, "0");
        _context.SetContext(ContextKeys.MadLibsFilledWords, "");

        var firstSlot = slots[0];
        var label = GetSlotLabel(firstSlot);
        _context.SetContext(ContextKeys.MadLibsCurrentSlot, firstSlot);

        return GetMadLibResponse("mad_libs_start", label);
    }

    internal string HandleMadLibsTurn(string input)
    {
        var lowerInput = input.Trim().ToLowerInvariant();

        foreach (var phrase in GameEndPhrases)
        {
            if (lowerInput.Contains(phrase) || lowerInput.Equals(phrase))
                return HandleMadLibsEnd(cancelled: true);
        }

        if (CancellationPhrases.Contains(lowerInput))
            return HandleMadLibsEnd(cancelled: true);

        var filledRaw = _context.GetContext(ContextKeys.MadLibsFilledWords) ?? "";
        var filled = string.IsNullOrEmpty(filledRaw) ? new List<string>() : filledRaw.Split('|').ToList();
        var slotIndexRaw = _context.GetContext(ContextKeys.MadLibsSlotIndex) ?? "0";
        int.TryParse(slotIndexRaw, out var slotIndex);

        var templateIdRaw = _context.GetContext(ContextKeys.MadLibsTemplateId) ?? "0";
        int.TryParse(templateIdRaw, out var templateId);
        var template = _dbContext.MadLibTemplates.Find(templateId);

        if (template == null)
        {
            _context.SetContext(ContextKeys.MadLibsActive, null);
            return "I lost track of our Mad Libs template! Let's start over.";
        }

        var slots = GetMadLibSlots(template.Template);

        var userWord = input.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
        filled.Add(userWord.ToLowerInvariant());
        slotIndex++;

        _context.SetContext(ContextKeys.MadLibsFilledWords, string.Join("|", filled));
        _context.SetContext(ContextKeys.MadLibsSlotIndex, slotIndex.ToString());

        if (slotIndex >= slots.Count)
            return HandleMadLibsEnd(cancelled: false);

        var nextSlot = slots[slotIndex];
        var label = GetSlotLabel(nextSlot);
        _context.SetContext(ContextKeys.MadLibsCurrentSlot, nextSlot);

        return GetMadLibResponse("mad_libs_prompt", label);
    }

    private string HandleMadLibsEnd(bool cancelled)
    {
        var templateIdRaw = _context.GetContext(ContextKeys.MadLibsTemplateId) ?? "0";
        int.TryParse(templateIdRaw, out var templateId);
        var filledRaw = _context.GetContext(ContextKeys.MadLibsFilledWords) ?? "";

        _context.SetContext(ContextKeys.MadLibsActive, null);
        _context.SetContext(ContextKeys.MadLibsTemplateId, null);
        _context.SetContext(ContextKeys.MadLibsSlotIndex, null);
        _context.SetContext(ContextKeys.MadLibsFilledWords, null);
        _context.SetContext(ContextKeys.MadLibsCurrentSlot, null);

        if (cancelled)
            return "OK, we can play Mad Libs another time!";

        var template = _dbContext.MadLibTemplates.Find(templateId);
        if (template == null)
            return "OK, that's the end of our Mad Libs!";

        var filled = string.IsNullOrEmpty(filledRaw) ? new List<string>() : filledRaw.Split('|').ToList();
        var slots = GetMadLibSlots(template.Template);

        var story = template.Template;
        for (int i = 0; i < slots.Count && i < filled.Count; i++)
        {
            story = ReplaceFirst(story, $"{{{slots[i]}}}", filled[i]);
        }

        // Replace any remaining unfilled slots with "something"
        story = MadLibSlotRegex.Replace(story, "something");

        return GetMadLibResponse("mad_libs_reveal", story);
    }

    private static List<string> GetMadLibSlots(string template)
    {
        return MadLibSlotRegex.Matches(template)
            .Select(m => m.Groups[1].Value)
            .ToList();
    }

    private static string GetSlotLabel(string slotType)
    {
        return MadLibSlotLabels.TryGetValue(slotType, out var label) ? label : $"a/an {slotType}";
    }

    private static string ReplaceFirst(string text, string search, string replace)
    {
        var pos = text.IndexOf(search, StringComparison.Ordinal);
        if (pos < 0) return text;
        return text.Substring(0, pos) + replace + text.Substring(pos + search.Length);
    }

    private string GetMadLibResponse(string category, params object[] args)
    {
        var botResponses = GetCachedBotResponses();
        if (botResponses.TryGetValue(category, out var responses) && responses.Count > 0)
        {
            var template = responses[Random.Shared.Next(responses.Count)];
            return args.Length > 0 ? string.Format(template, args) : template;
        }

        var fallbacks = new Dictionary<string, List<string>>
        {
            ["mad_libs_start"] = new() { $"Let's play Mad Libs! {{0}}" },
            ["mad_libs_prompt"] = new() { $"Give me {{0}}:" },
            ["mad_libs_reveal"] = new() { $"Here's our Mad Libs story:\n{{0}}" },
            ["mad_libs_already_active"] = new() { "We're already playing Mad Libs!" },
        };

        if (fallbacks.TryGetValue(category, out var fb) && fb.Count > 0)
        {
            var template = fb[Random.Shared.Next(fb.Count)];
            return args.Length > 0 ? string.Format(template, args) : template;
        }

        return string.Empty;
    }

    internal bool TryHandleErrorKnowledge(string input, out string response)
    {
        response = string.Empty;
        if (string.IsNullOrWhiteSpace(input) || input.Length < 10)
            return false;

        var lowerInput = input.ToLowerInvariant().Trim();
        if (lowerInput.Contains("error") ||
            lowerInput.Contains("exception") ||
            lowerInput.Contains("failed") ||
            lowerInput.Contains("could not") ||
            Regex.IsMatch(lowerInput, @"\b(CS\d{4})\b") ||
            Regex.IsMatch(lowerInput, @"\b(NETSDK|MSB|NU)\d{4}\b") ||
            lowerInput.Contains("does not exist") ||
            lowerInput.Contains("not found") ||
            lowerInput.Contains("invalid") ||
            lowerInput.Contains("unexpected") ||
            lowerInput.Contains("nullreference") ||
            lowerInput.Contains("cannot convert") ||
            lowerInput.Contains("expected semicolon") ||
            lowerInput.Contains("not all code paths") ||
            lowerInput.Contains("no overload") ||
            lowerInput.Contains("does not contain"))
        {
            var entry = _knowledgeStore.MatchError(input);
            if (entry != null)
            {
                _knowledgeStore.IncrementErrorUsage(entry.Id);
                _context.SetContext(ContextKeys.LastErrorEntryId, entry.Id.ToString());
                response = GetErrorResponse("error_knowledge_found", entry.Language, entry.Suggestion);
                return true;
            }

            response = GetRiddleResponse("error_knowledge_not_found");
            return true;
        }

        return false;
    }

    private string GetErrorResponse(string category, params object[] args)
    {
        var botResponses = GetCachedBotResponses();
        if (botResponses.TryGetValue(category, out var responses) && responses.Count > 0)
        {
            var template = responses[Random.Shared.Next(responses.Count)];
            return args.Length > 0 ? string.Format(template, args) : template;
        }
        return category switch
        {
            "error_knowledge_found" => $"That looks like a {args[0]} error. {args[1]}",
            "error_knowledge_not_found" => "I don't recognise that error. Can you tell me what fixed it?",
            "error_knowledge_learned" => "Thanks! I'll remember that fix.",
            "error_knowledge_followup" => "Did that fix the problem?",
            _ => string.Join(" ", args)
        };
    }

    internal bool TryHandleJokeStart(string input, out string response)
    {
        var lowerInput = input.ToLowerInvariant().Trim();
        foreach (var phrase in JokeStartPhrases)
        {
            if (lowerInput.Contains(phrase))
            {
                var joke = _knowledgeStore.GetRandomJoke();
                if (joke == null)
                {
                    response = "I don't have any jokes to tell yet!";
                    return true;
                }

                _context.SetContext(ContextKeys.PendingJokeSetup, joke.Setup);
                _context.SetContext(ContextKeys.PendingJokePunchline, joke.Punchline);
                response = GetJokeResponse("dad_joke_setup", joke.Setup);
                return true;
            }
        }

        response = string.Empty;
        return false;
    }

    internal string HandleJokeTurn()
    {
        var punchline = _context.GetContext(ContextKeys.PendingJokePunchline) ?? string.Empty;
        _context.SetContext(ContextKeys.PendingJokeSetup, null);
        _context.SetContext(ContextKeys.PendingJokePunchline, null);
        return GetJokeResponse("dad_joke_punchline", punchline);
    }

    internal bool TryHandleRiddleStart(string input, out string response)
    {
        var lowerInput = input.ToLowerInvariant().Trim();
        foreach (var phrase in RiddleStartPhrases)
        {
            if (lowerInput.Contains(phrase))
            {
                var existing = _context.GetContext(ContextKeys.RiddleActive);
                if (existing != null)
                {
                    response = GetRiddleResponse("riddle_already_active");
                    return true;
                }

                var riddle = _knowledgeStore.GetRandomRiddle();
                if (riddle == null)
                {
                    response = "I don't have any riddles yet!";
                    return true;
                }

                _context.SetContext(ContextKeys.RiddleActive, "true");
                _context.SetContext(ContextKeys.PendingRiddleQuestion, riddle.Question);
                _context.SetContext(ContextKeys.PendingRiddleAnswer, riddle.Answer);
                _context.SetContext(ContextKeys.PendingRiddleHint, riddle.Hint ?? "");
                _context.SetContext(ContextKeys.PendingRiddleAttempts, "0");
                response = GetRiddleResponse("riddle_present", riddle.Question);
                return true;
            }
        }

        response = string.Empty;
        return false;
    }

    internal string HandleRiddleTurn(string input)
    {
        var lowerInput = input.Trim().ToLowerInvariant();
        var answer = _context.GetContext(ContextKeys.PendingRiddleAnswer) ?? "";
        var hint = _context.GetContext(ContextKeys.PendingRiddleHint) ?? "";
        var attemptsRaw = _context.GetContext(ContextKeys.PendingRiddleAttempts) ?? "0";
        int.TryParse(attemptsRaw, out var attempts);

        foreach (var phrase in SurrenderPhrases)
        {
            if (lowerInput.Contains(phrase))
            {
                ClearRiddleState();
                return GetRiddleResponse("riddle_give_up", answer);
            }
        }

        if (lowerInput.Contains("hint") && !string.IsNullOrEmpty(hint))
        {
            _context.SetContext(ContextKeys.PendingRiddleAttempts, (attempts + 1).ToString());
            return GetRiddleResponse("riddle_hint", hint);
        }

        if (lowerInput.Contains(answer.ToLowerInvariant()) || IsCorrectGuess(lowerInput, answer))
        {
            ClearRiddleState();
            return GetRiddleResponse("riddle_correct");
        }

        attempts++;
        _context.SetContext(ContextKeys.PendingRiddleAttempts, attempts.ToString());

        if (attempts >= 3)
        {
            ClearRiddleState();
            return GetRiddleResponse("riddle_give_up", answer);
        }

        return GetRiddleResponse("riddle_wrong");
    }

    private static bool IsCorrectGuess(string lowerInput, string answer)
    {
        var cleanAnswer = answer.ToLowerInvariant().Trim();
        if (cleanAnswer.StartsWith("a ") || cleanAnswer.StartsWith("an "))
        {
            var withoutArticle = cleanAnswer.Split(' ', 2)[1];
            if (lowerInput.Contains(withoutArticle))
                return true;
        }
        if (lowerInput.Contains(cleanAnswer))
            return true;
        if (lowerInput.Trim() == cleanAnswer)
            return true;
        return false;
    }

    private void ClearRiddleState()
    {
        _context.SetContext(ContextKeys.RiddleActive, null);
        _context.SetContext(ContextKeys.PendingRiddleQuestion, null);
        _context.SetContext(ContextKeys.PendingRiddleAnswer, null);
        _context.SetContext(ContextKeys.PendingRiddleHint, null);
        _context.SetContext(ContextKeys.PendingRiddleAttempts, null);
    }

    private string GetJokeResponse(string category, params object[] args)
    {
        var botResponses = GetCachedBotResponses();
        if (botResponses.TryGetValue(category, out var responses) && responses.Count > 0)
        {
            var template = responses[Random.Shared.Next(responses.Count)];
            return args.Length > 0 ? string.Format(template, args) : template;
        }

        var fallbacks = new Dictionary<string, List<string>>
        {
            ["dad_joke_setup"] = new() { $"{{0}}?" },
            ["dad_joke_punchline"] = new() { $"{{0}}" },
        };

        if (fallbacks.TryGetValue(category, out var fb) && fb.Count > 0)
        {
            var template = fb[Random.Shared.Next(fb.Count)];
            return args.Length > 0 ? string.Format(template, args) : template;
        }

        return string.Empty;
    }

    private string GetRiddleResponse(string category, params object[] args)
    {
        var botResponses = GetCachedBotResponses();
        if (botResponses.TryGetValue(category, out var responses) && responses.Count > 0)
        {
            var template = responses[Random.Shared.Next(responses.Count)];
            return args.Length > 0 ? string.Format(template, args) : template;
        }

        var fallbacks = new Dictionary<string, List<string>>
        {
            ["riddle_present"] = new() { $"Here's a riddle: {{0}}" },
            ["riddle_correct"] = new() { "That's right! Well done!" },
            ["riddle_wrong"] = new() { "Not quite! Try again." },
            ["riddle_hint"] = new() { $"Here's a hint: {{0}}" },
            ["riddle_give_up"] = new() { $"The answer was {{0}}." },
            ["riddle_already_active"] = new() { "You already have a riddle to solve!" },
        };

        if (fallbacks.TryGetValue(category, out var fb) && fb.Count > 0)
        {
            var template = fb[Random.Shared.Next(fb.Count)];
            return args.Length > 0 ? string.Format(template, args) : template;
        }

        return string.Empty;
    }

    internal bool TryHandleWouldYouRather(string input, out string response)
    {
        var lowerInput = input.ToLowerInvariant().Trim();
        foreach (var phrase in WyrStartPhrases)
        {
            if (lowerInput.Contains(phrase))
            {
                var question = _responseEngine.BuildWyrQuestion(_currentUserId);
                if (question == null)
                {
                    response = string.Empty;
                    return false;
                }

                response = question;
                return true;
            }
        }

        response = string.Empty;
        return false;
    }

    internal string HandleWouldYouRatherAnswer(string input)
    {
        _context.SetContext(ContextKeys.WyrActive, null);
        _context.SetContext(ContextKeys.PendingWyrQuestion, null);
        var optionA = _context.GetContext(ContextKeys.PendingWyrOptionA);
        var optionB = _context.GetContext(ContextKeys.PendingWyrOptionB);
        _context.SetContext(ContextKeys.PendingWyrOptionA, null);
        _context.SetContext(ContextKeys.PendingWyrOptionB, null);

        if (string.IsNullOrEmpty(optionA) && string.IsNullOrEmpty(optionB))
            return string.Empty;

        var chosen = Random.Shared.Next(2) == 0 ? optionA : optionB;
        return GetWyrResponse("wyr_acknowledgement", chosen ?? "");
    }

    private string GetWyrResponse(string category, params object[] args)
    {
        var botResponses = GetCachedBotResponses();
        if (botResponses.TryGetValue(category, out var responses) && responses.Count > 0)
        {
            var template = responses[Random.Shared.Next(responses.Count)];
            return args.Length > 0 ? string.Format(template, args) : template;
        }

        return string.Empty;
    }

    internal bool TryHandleHangmanStart(string input, out string response)
    {
        var lowerInput = input.ToLowerInvariant().Trim();
        foreach (var phrase in HangmanStartPhrases)
        {
            if (lowerInput.Contains(phrase))
            {
                var existing = _context.GetContext(ContextKeys.HangmanActive);
                if (existing != null)
                {
                    response = GetHangmanResponse("hangman_already_active");
                    return true;
                }

                response = StartHangman();
                return true;
            }
        }

        response = string.Empty;
        return false;
    }

    private string StartHangman()
    {
        var word = PickHangmanWord();
        if (string.IsNullOrEmpty(word))
            return "I don't have any words to play with right now!";

        _context.SetContext(ContextKeys.HangmanActive, "true");
        _context.SetContext(ContextKeys.HangmanWord, word);
        _context.SetContext(ContextKeys.HangmanGuessed, "");
        _context.SetContext(ContextKeys.HangmanWrongLetters, "");
        _context.SetContext(ContextKeys.HangmanWrongCount, "0");

        var display = BuildHangmanDisplay(word, new HashSet<string>());
        return GetHangmanResponse("hangman_welcome", word.Length, display, "(none)");
    }

    internal string HandleHangmanTurn(string input)
    {
        var lowerInput = input.Trim().ToLowerInvariant();

        foreach (var phrase in HangmanStartPhrases)
        {
            if (lowerInput.Contains(phrase))
                return GetHangmanResponse("hangman_already_active");
        }

        foreach (var phrase in SurrenderPhrases)
        {
            if (lowerInput.Contains(phrase))
            {
                var surrenderWord = _context.GetContext(ContextKeys.HangmanWord) ?? "";
                ClearHangmanState();
                return GetHangmanResponse("hangman_surrender", surrenderWord);
            }
        }

        var word = _context.GetContext(ContextKeys.HangmanWord) ?? "";
        var guessedRaw = _context.GetContext(ContextKeys.HangmanGuessed) ?? "";
        var wrongLettersRaw = _context.GetContext(ContextKeys.HangmanWrongLetters) ?? "";
        var wrongCountRaw = _context.GetContext(ContextKeys.HangmanWrongCount) ?? "0";
        int.TryParse(wrongCountRaw, out var wrongCount);

        var guessed = string.IsNullOrEmpty(guessedRaw)
            ? new HashSet<string>()
            : guessedRaw.Split(' ').ToHashSet(StringComparer.OrdinalIgnoreCase);

        var wrongLetters = string.IsNullOrEmpty(wrongLettersRaw)
            ? new HashSet<string>()
            : wrongLettersRaw.Split(',').Select(w => w.Trim())
                .Where(w => w.Length > 0).ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (lowerInput.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length == 1)
        {
            var guess = lowerInput.Trim();

            if (guess.Length == 1 && char.IsLetter(guess[0]))
            {
                if (guessed.Contains(guess))
                    return GetHangmanResponse("hangman_repeat_letter", guess);

                guessed.Add(guess);
                _context.SetContext(ContextKeys.HangmanGuessed, string.Join(" ", guessed));

                if (word.Contains(guess))
                {
                    var display = BuildHangmanDisplay(word, guessed);
                    if (!display.Contains('_'))
                    {
                        ClearHangmanState();
                        return GetHangmanResponse("hangman_win", word);
                    }
                    return GetHangmanResponse("hangman_correct", guess, display);
                }

                wrongCount++;
                wrongLetters.Add(guess);
                _context.SetContext(ContextKeys.HangmanWrongCount, wrongCount.ToString());
                _context.SetContext(ContextKeys.HangmanWrongLetters, string.Join(",", wrongLetters));

                if (wrongCount >= HangmanMaxAttempts)
                {
                    ClearHangmanState();
                    return GetHangmanResponse("hangman_lose", word);
                }

                var wrongDisplay = BuildHangmanDisplay(word, guessed);
                var remaining = HangmanMaxAttempts - wrongCount;
                return GetHangmanResponse("hangman_wrong", guess, remaining, wrongDisplay, string.Join(", ", wrongLetters));
            }

            if (guess.Length > 1 && guess.All(char.IsLetter))
            {
                if (guess == word)
                {
                    ClearHangmanState();
                    return GetHangmanResponse("hangman_win", word);
                }

                wrongCount++;
                _context.SetContext(ContextKeys.HangmanWrongCount, wrongCount.ToString());

                if (wrongCount >= HangmanMaxAttempts)
                {
                    ClearHangmanState();
                    return GetHangmanResponse("hangman_lose", word);
                }

                var wordDisplay = BuildHangmanDisplay(word, guessed);
                var remaining = HangmanMaxAttempts - wrongCount;
                return GetHangmanResponse("hangman_wrong", guess, remaining, wordDisplay, string.Join(", ", wrongLetters));
            }
        }

        return GetHangmanResponse("hangman_invalid");
    }

    private void ClearHangmanState()
    {
        _context.SetContext(ContextKeys.HangmanActive, null);
        _context.SetContext(ContextKeys.HangmanWord, null);
        _context.SetContext(ContextKeys.HangmanGuessed, null);
        _context.SetContext(ContextKeys.HangmanWrongLetters, null);
        _context.SetContext(ContextKeys.HangmanWrongCount, null);
    }

    private static string BuildHangmanDisplay(string word, HashSet<string> guessed)
    {
        return string.Join(" ", word.Select(c => guessed.Contains(c.ToString()) ? c.ToString() : "_"));
    }

    private string PickHangmanWord()
    {
        var words = _dbContext.PosDictionary
            .Where(e => e.Word.Length >= 6 && e.WordType == "noun")
            .AsEnumerable()
            .Where(e => e.Word.All(char.IsLetter))
            .Select(e => e.Word.ToLowerInvariant())
            .Distinct()
            .ToList();
        if (words.Count == 0) return string.Empty;
        return words[Random.Shared.Next(words.Count)];
    }

    private string GetHangmanResponse(string category, params object[] args)
    {
        var botResponses = GetCachedBotResponses();
        if (botResponses.TryGetValue(category, out var responses) && responses.Count > 0)
        {
            var template = responses[Random.Shared.Next(responses.Count)];
            return args.Length > 0 ? string.Format(template, args) : template;
        }

        var fallbacks = new Dictionary<string, List<string>>
        {
            ["hangman_welcome"] = new() { $"Let's play Hangman! The word has {{0}} letters.\n{{1}}\nWrong: {{2}}" },
            ["hangman_correct"] = new() { $"Good guess! The letter '{{0}}' is in the word.\n{{1}}" },
            ["hangman_wrong"] = new() { $"Sorry, '{{0}}' is not in the word. {{1}} wrong guesses left.\n{{2}}\nWrong: {{3}}" },
            ["hangman_win"] = new() { $"You got it! The word was '{{0}}'. Nice!" },
            ["hangman_lose"] = new() { $"Game over! The word was '{{0}}'." },
            ["hangman_already_active"] = new() { "You're already playing Hangman! Guess a letter." },
            ["hangman_surrender"] = new() { $"The word was '{{0}}'. Maybe next time!" },
            ["hangman_invalid"] = new() { "Guess a single letter or the whole word." },
            ["hangman_repeat_letter"] = new() { $"You already guessed '{{0}}'. Try a different letter." },
        };

        if (fallbacks.TryGetValue(category, out var fb) && fb.Count > 0)
        {
            var template = fb[Random.Shared.Next(fb.Count)];
            return args.Length > 0 ? string.Format(template, args) : template;
        }

        return string.Empty;
    }

    internal void RunHomeworkCheck()
    {
        if (_llmOrchestrator == null || !_llmOrchestrator.IsAvailable || _llmOrchestrator.UserDeclined)
            return;
        if (_currentUserId == null)
            return;

        var botResponses = GetCachedBotResponses();
        if (botResponses.TryGetValue("homework_check_processing", out var procResponses) && procResponses.Count > 0)
            OnStatusUpdate?.Invoke($"{_botName}: {procResponses[Random.Shared.Next(procResponses.Count)]}");

        var prompt = BuildHomeworkCheckPrompt();
        if (string.IsNullOrEmpty(prompt)) return;

        var llmResult = LlmCallWithIndicator(() => _llmOrchestrator.GenerateHomeworkCheck(prompt));
        if (string.IsNullOrEmpty(llmResult)) return;

        var result = ParseHomeworkCheckResult(llmResult);
        if (result == null) return;

        var changes = new List<string>();

        foreach (var rule in result.RulesToRemove)
        {
            _knowledgeStore.DeactivateLearnedRule(rule.RuleId);
            changes.Add($"fixed rule #{rule.RuleId}");
        }

        foreach (var def in result.DefinitionsToAdd)
        {
            var validCats = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "person", "place", "thing", "verb" };
            if (result.ClassificationsToAdd.Any(c =>
                    string.Equals(c.Word, def.Word, StringComparison.OrdinalIgnoreCase) &&
                    validCats.Contains(c.Category)))
                continue;

            _knowledgeStore.SetDefinition(def.Word, def.Definition, _currentUserId);
            changes.Add($"defined '{def.Word}'");
        }

        foreach (var cls in result.ClassificationsToAdd)
        {
            var validCats = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "person", "place", "thing", "verb" };
            if (!validCats.Contains(cls.Category)) continue;

            _knowledgeStore.AddNounCategory(cls.Word, cls.Category, _currentUserId);
            var wordType = cls.Category is "person" or "place" or "thing" ? "noun" : cls.Category;
            _knowledgeStore.UpdateWordType(cls.Word, wordType);
            changes.Add($"classified '{cls.Word}'");
        }

        _knowledgeStore.Save();

        if (changes.Count > 0)
        {
            var summary = string.Join(", ", changes);
            if (botResponses.TryGetValue("homework_check_summary", out var sumResponses) && sumResponses.Count > 0)
            {
                var template = sumResponses[Random.Shared.Next(sumResponses.Count)];
                OnStatusUpdate?.Invoke($"{_botName}: {string.Format(template, summary)}");
            }
        }
        else
        {
            if (botResponses.TryGetValue("homework_check_none", out var noneResponses) && noneResponses.Count > 0)
            {
                OnStatusUpdate?.Invoke($"{_botName}: {noneResponses[Random.Shared.Next(noneResponses.Count)]}");
            }
        }
    }

    private string BuildHomeworkCheckPrompt()
    {
        var conversations = _knowledgeStore.GetConversationsBySession(_sessionId);
        if (conversations.Count == 0) return string.Empty;

        var log = new System.Text.StringBuilder();
        log.AppendLine("Conversation log:");
        foreach (var c in conversations)
        {
            log.AppendLine($"User: {c.UserInput}");
            log.AppendLine($"Bot: {c.BotResponse}");
        }

        var learnedRules = _knowledgeStore.GetLearnedRules();
        if (learnedRules.Count > 0)
        {
            log.AppendLine("\nLearned response rules:");
            foreach (var r in learnedRules)
            {
                log.AppendLine($"  Rule #{r.Id}: pattern=\"{r.Pattern}\", template=\"{r.ResponseTemplate}\", confidence={r.Confidence}");
            }
        }

        var definitions = _dbContext.WordDefinitions.ToList();
        if (definitions.Count > 0)
        {
            log.AppendLine("\nWord definitions:");
            foreach (var d in definitions)
            {
                log.AppendLine($"  \"{d.Word}\": \"{d.Definition}\"");
            }
        }

        var nounCategories = _knowledgeStore.GetNounCategories();
        if (nounCategories.Count > 0)
        {
            log.AppendLine("\nNoun categories:");
            foreach (var n in nounCategories)
            {
                log.AppendLine($"  \"{n.Noun}\": {n.Category}");
            }
        }

        return log.ToString();
    }

    private static HomeworkCheckResult? ParseHomeworkCheckResult(string json)
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            };
            return JsonSerializer.Deserialize<HomeworkCheckResult>(json, options);
        }
        catch
        {
            return null;
        }
    }

    private class HomeworkCheckResult
    {
        public List<RuleToRemove> RulesToRemove { get; set; } = new();
        public List<DefinitionToAdd> DefinitionsToAdd { get; set; } = new();
        public List<ClassificationToAdd> ClassificationsToAdd { get; set; } = new();
    }

    private class RuleToRemove
    {
        public int RuleId { get; set; }
        public string? Reason { get; set; }
    }

    private class DefinitionToAdd
    {
        public string Word { get; set; } = "";
        public string Definition { get; set; } = "";
    }

    private class ClassificationToAdd
    {
        public string Word { get; set; } = "";
        public string Category { get; set; } = "";
    }

    private static readonly string[] QuizStartPhrases = { "quiz me", "test me", "give me a quiz", "start quiz", "ask me a question" };
    private static readonly string[] QuizStopPhrases = { "stop quiz", "quit quiz", "give up", "stop", "quit" };
    private const int MaxQuizQuestions = 5;

    private record QuizFactData(string Subject, string Verb, string Object, string PredicateType);

    internal bool TryHandleQuizStart(string input, out string response)
    {
        var lowerInput = input.ToLowerInvariant().Trim();
        var isQuizStart = lowerInput == "quiz" || QuizStartPhrases.Any(p => lowerInput.Contains(p));
        if (!isQuizStart)
        {
            response = string.Empty;
            return false;
        }

        var existing = _context.GetContext(ContextKeys.QuizActive);
        if (existing != null)
        {
            response = GetQuizResponse("quiz_already_active");
            return true;
        }

        response = StartQuiz();
        return true;
    }

    private string StartQuiz()
    {
        if (_currentUserId == null)
            return GetQuizResponse("quiz_no_facts");

        var facts = _knowledgeStore.GetRandomFactsForQuiz(_currentUserId.Value, MaxQuizQuestions);
        if (facts.Count == 0)
            return GetQuizResponse("quiz_no_facts");

        var factData = facts.Select(f => new QuizFactData(f.Subject, f.Verb, f.Object, f.PredicateType)).ToList();
        var json = JsonSerializer.Serialize(factData);
        _context.SetContext(ContextKeys.QuizActive, "true");
        _context.SetContext(ContextKeys.QuizFacts, json);
        _context.SetContext(ContextKeys.QuizScore, "0/0");
        _context.SetContext(ContextKeys.QuizQuestionCount, "0");

        return AskNextQuizQuestion(factData, 0);
    }

    private string AskNextQuizQuestion(List<QuizFactData> factData, int index)
    {
        if (index >= factData.Count)
            return FinishQuiz();

        var data = factData[index];
        var question = BuildQuizQuestion(data.Subject, data.Verb, data.Object, data.PredicateType);

        _context.SetContext(ContextKeys.QuizCurrentQuestion, question);
        _context.SetContext(ContextKeys.QuizCurrentAnswer, data.Object);
        _context.SetContext(ContextKeys.QuizQuestionCount, (index + 1).ToString());

        return GetQuizResponse("quiz_question", question, index + 1, factData.Count);
    }

    private static string BuildQuizQuestion(string subject, string verb, string obj, string predicateType)
    {
        return predicateType switch
        {
            nameof(PredicateType.Preference) => $"You said you {verb} {obj}. Do you still {verb} {obj}?",
            nameof(PredicateType.Dislike) => $"You said you dislike {obj}. Is that still true?",
            nameof(PredicateType.Possession) => $"You told me you have {obj}. Do you still have {obj}?",
            nameof(PredicateType.PersonalAttribute) => $"You said you're {obj}. Is that still true?",
            nameof(PredicateType.Belief) => $"You know about {obj}. Where did you learn about {obj}?",
            _ => $"You told me {subject} {verb} {obj}. What {verb} {subject}?"
        };
    }

    internal string HandleQuizTurn(string input)
    {
        var lowerInput = input.Trim().ToLowerInvariant();

        if (lowerInput == "quiz" || QuizStartPhrases.Any(p => lowerInput.Contains(p)))
            return GetQuizResponse("quiz_already_active");

        foreach (var phrase in QuizStopPhrases)
        {
            if (lowerInput.Contains(phrase))
                return FinishQuiz();
        }

        var currentAnswer = _context.GetContext(ContextKeys.QuizCurrentAnswer) ?? "";
        var scoreRaw = _context.GetContext(ContextKeys.QuizScore) ?? "0/0";
        var countRaw = _context.GetContext(ContextKeys.QuizQuestionCount) ?? "0";
        var factsJson = _context.GetContext(ContextKeys.QuizFacts) ?? "[]";

        int.TryParse(countRaw, out var count);

        var (correct, total) = ParseScore(scoreRaw);

        var factData = JsonSerializer.Deserialize<List<QuizFactData>>(factsJson);
        var hasMoreQuestions = factData != null && count < factData.Count;

        if (lowerInput.Contains(currentAnswer.ToLowerInvariant()))
        {
            correct++;
            total++;
            _context.SetContext(ContextKeys.QuizScore, $"{correct}/{total}");

            if (hasMoreQuestions)
            {
                return GetQuizResponse("quiz_correct", currentAnswer) + "\n" + AskNextQuizQuestion(factData!, count);
            }

            return FinishQuiz();
        }
        else
        {
            total++;
            _context.SetContext(ContextKeys.QuizScore, $"{correct}/{total}");

            if (hasMoreQuestions)
            {
                return GetQuizResponse("quiz_wrong", currentAnswer) + "\n" + AskNextQuizQuestion(factData!, count);
            }

            return FinishQuiz();
        }
    }

    private string FinishQuiz()
    {
        var scoreRaw = _context.GetContext(ContextKeys.QuizScore) ?? "0/0";
        var (correct, total) = ParseScore(scoreRaw);
        ClearQuizState();
        return GetQuizResponse("quiz_score", correct, total);
    }

    private void ClearQuizState()
    {
        _context.SetContext(ContextKeys.QuizActive, null);
        _context.SetContext(ContextKeys.QuizScore, null);
        _context.SetContext(ContextKeys.QuizQuestionCount, null);
        _context.SetContext(ContextKeys.QuizCurrentAnswer, null);
        _context.SetContext(ContextKeys.QuizCurrentQuestion, null);
        _context.SetContext(ContextKeys.QuizFacts, null);
    }

    private static (int Correct, int Total) ParseScore(string score)
    {
        var parts = score.Split('/');
        if (parts.Length == 2 && int.TryParse(parts[0], out var c) && int.TryParse(parts[1], out var t))
            return (c, t);
        return (0, 0);
    }

    private string GetQuizResponse(string category, params object[] args)
    {
        var botResponses = GetCachedBotResponses();
        if (botResponses.TryGetValue(category, out var responses) && responses.Count > 0)
        {
            var template = responses[Random.Shared.Next(responses.Count)];
            return args.Length > 0 ? string.Format(template, args) : template;
        }

        var fallbacks = new Dictionary<string, List<string>>
        {
            ["quiz_question"] = new() { "Question {1}/{2}: {0}" },
            ["quiz_correct"] = new() { "That's right! The answer was {0}." },
            ["quiz_wrong"] = new() { "Not quite! The answer was {0}." },
            ["quiz_score"] = new() { "Quiz complete! You got {0}/{1} correct." },
            ["quiz_already_active"] = new() { "You're already in a quiz! Answer the question." },
            ["quiz_no_facts"] = new() { "I don't know enough about you to make a quiz yet." },
        };

        if (fallbacks.TryGetValue(category, out var fb) && fb.Count > 0)
        {
            var template = fb[Random.Shared.Next(fb.Count)];
            return args.Length > 0 ? string.Format(template, args) : template;
        }

        return string.Empty;
    }

    internal bool TryHandleReminderRequest(string input, out string response)
    {
        response = string.Empty;
        if (_currentUserId == null) return false;

        var lower = input.Trim().ToLowerInvariant();

        var remindMatch = Regex.Match(lower, @"^remind me (.+?) to (.+)$");
        if (remindMatch.Success)
        {
            var timePart = remindMatch.Groups[1].Value.Trim();
            var task = remindMatch.Groups[2].Value.Trim();

            if (HasReminderKeywordsOnly(task))
                return false;

            if (_knowledgeStore.HasReminderForTask(_currentUserId.Value, task))
            {
                response = _responseEngine.GetResponse("reminder_duplicate");
                return true;
            }

            var parsedTime = _knowledgeStore.ParseReminderTime($"{timePart} {task}");
            if (parsedTime == null)
            {
                _context.SetContext(ContextKeys.PendingReminderTask, task);
                response = "When should I remind you? You can say something like 'in 2 hours' or 'at 5pm'.";
                return true;
            }

            return HandleReminderCreation(task, parsedTime.Value, out response);
        }

        var remindNoTime = Regex.Match(lower, @"^remind me to (.+)$");
        if (remindNoTime.Success)
        {
            var task = remindNoTime.Groups[1].Value.Trim();

            if (HasReminderKeywordsOnly(task))
                return false;

            if (_knowledgeStore.HasReminderForTask(_currentUserId.Value, task))
            {
                response = _responseEngine.GetResponse("reminder_duplicate");
                return true;
            }

            var parsedTime = _knowledgeStore.ParseReminderTime(input, null);
            var defaultHour = DateTime.UtcNow.AddHours(1);
            var diff = parsedTime.HasValue ? (parsedTime.Value - defaultHour).Duration() : TimeSpan.MaxValue;
            if (parsedTime == null || diff.TotalSeconds < 5)
            {
                _context.SetContext(ContextKeys.PendingReminderTask, task);
                response = "When should I remind you? You can say something like 'in 2 hours' or 'at 5pm'.";
                return true;
            }

            return HandleReminderCreation(task, parsedTime.Value, out response);
        }

        if (lower.Contains("what reminders") || lower.Contains("what's coming up") ||
            lower.Contains("what do i need to do") || lower == "reminders" || lower == "show reminders" ||
            lower.Contains("list reminders") || lower.Contains("my reminders"))
        {
            var reminders = _knowledgeStore.GetPendingReminders(_currentUserId.Value);
            if (reminders.Count == 0)
            {
                response = _responseEngine.GetResponse("reminder_empty");
                return true;
            }

            var reminderLines = reminders.Select((r, i) =>
                $"{i + 1}. {r.Task} (due: {FormatReminderTime(r.DueAt)})");
            var reminderText = string.Join("\n", reminderLines);
            response = _responseEngine.GetResponse("reminder_list", reminderText);
            return true;
        }

        var doneMatch = Regex.Match(lower, @"(?:mark|set)\s+(?:reminder\s+)?(.+?)\s+(?:as\s+)?done");
        if (doneMatch.Success)
        {
            var task = doneMatch.Groups[1].Value.Trim();
            var reminder = _knowledgeStore.MarkReminderDone(_currentUserId.Value, task);
            if (reminder != null)
            {
                response = _responseEngine.GetResponse("reminder_done", reminder.Task);
                return true;
            }
            response = "I couldn't find a reminder matching that.";
            return true;
        }

        var iDidMatch = Regex.Match(lower, @"i (?:did|finished|completed)\s+(.+?)\s+reminder$");
        if (iDidMatch.Success)
        {
            var task = iDidMatch.Groups[1].Value.Trim();
            var reminder = _knowledgeStore.MarkReminderDone(_currentUserId.Value, task);
            if (reminder != null)
            {
                response = _responseEngine.GetResponse("reminder_done", reminder.Task);
                return true;
            }
            return false;
        }

        var cancelMatch = Regex.Match(lower, @"(?:cancel|forget|delete|remove)\s+reminder\s+(?:for|about\s+)?(.+?)$");
        if (cancelMatch.Success)
        {
            var task = cancelMatch.Groups[1].Value.Trim();
            var reminder = _knowledgeStore.CancelReminder(_currentUserId.Value, task);
            if (reminder != null)
            {
                response = _responseEngine.GetResponse("reminder_cancelled", reminder.Task);
                return true;
            }
            response = "I couldn't find a reminder matching that.";
            return true;
        }

        return false;
    }

    private bool HandleReminderCreation(string task, DateTime dueAt, out string response)
    {
        response = string.Empty;
        if (_currentUserId == null) return false;

        var reminder = _knowledgeStore.CreateReminder(_currentUserId.Value, task, dueAt);
        _knowledgeStore.Save();
        _context.SetContext(ContextKeys.PendingReminderTask, null);
        _context.SetContext(ContextKeys.PendingReminderTime, null);
        if (reminder == null)
        {
            response = string.Empty;
            return false;
        }
        response = _responseEngine.GetResponse("reminder_created", reminder.Task, FormatReminderTime(reminder.DueAt));
        return true;
    }

    private string HandleReminderCreation(string task)
    {
        var timeStr = _context.GetContext(ContextKeys.PendingReminderTime);
        if (timeStr == null || !DateTime.TryParse(timeStr, out var dueAt))
            dueAt = DateTime.UtcNow.AddHours(1);

        if (_currentUserId == null)
        {
            _context.SetContext(ContextKeys.PendingReminderTask, null);
            _context.SetContext(ContextKeys.PendingReminderTime, null);
            return string.Empty;
        }

        var reminder = _knowledgeStore.CreateReminder(_currentUserId.Value, task, dueAt);
        _knowledgeStore.Save();
        _context.SetContext(ContextKeys.PendingReminderTask, null);
        _context.SetContext(ContextKeys.PendingReminderTime, null);
        if (reminder == null)
            return string.Empty;
        return _responseEngine.GetResponse("reminder_created", reminder.Task, FormatReminderTime(reminder.DueAt));
    }

    private static string FormatReminderTime(string dueAtStr)
    {
        if (DateTime.TryParse(dueAtStr, out var dt))
        {
            var now = DateTime.UtcNow;
            var localDt = dt.Kind == DateTimeKind.Utc ? dt.ToLocalTime() : dt;

            if (dt.Date == now.Date)
                return $"today at {localDt:h:mm tt}";
            if (dt.Date == now.Date.AddDays(1))
                return $"tomorrow at {localDt:h:mm tt}";

            return localDt.ToString("MMM dd 'at' h:mm tt");
        }
        return dueAtStr;
    }

    private static bool HasReminderKeywordsOnly(string text)
    {
        var reminderKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "remind", "reminder", "reminders", "me", "to", "at", "in", "on",
            "the", "a", "an", "today", "tomorrow", "now", "later"
        };
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return words.Length > 0 && words.All(w => reminderKeywords.Contains(w));
    }

    public string? GetSessionStartReminderMessage()
    {
        if (_currentUserId == null) return null;

        var shownCount = _context.GetContext(ContextKeys.ReminderShownCount);
        if (shownCount != null) return null;

        var dueReminders = _knowledgeStore.GetDueReminders(_currentUserId.Value);
        if (dueReminders.Count == 0)
        {
            _context.SetContext(ContextKeys.ReminderShownCount, "0");
            return null;
        }

        _context.SetContext(ContextKeys.ReminderShownCount, dueReminders.Count.ToString());

        if (dueReminders.Count == 1)
            return _responseEngine.GetResponse("reminder_due", dueReminders[0].Task);

        var lines = dueReminders.Select((r, i) =>
            $"{i + 1}. {r.Task} (due: {FormatReminderTime(r.DueAt)})");
        return _responseEngine.GetResponse("reminder_list", string.Join("\n", lines));
    }

    public void Dispose()
    {
        _sessionLogger?.Dispose();
        _mcpRegistry?.Dispose();
        _llmOrchestrator?.Dispose();
        _dbContext.Dispose();
    }
}

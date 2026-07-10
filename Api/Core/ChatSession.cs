using PokeChat.Data;
using PokeChat.Knowledge;
using PokeChat.LLM;
using PokeChat.NLP;
using PokeChat.Responses;
using PokeChat.Tools;

namespace PokeChat.Core;

public class ChatSession : IDisposable
{
    private readonly ChatEngine _engine;

    public ChatSession()
    {
        _engine = new ChatEngine();
        _engine.OnStatusUpdate = msg =>
        {
            if (msg == "thinking")
                Console.Write($"\r{_engine.BotName} is thinking...");
            else if (msg == "clear")
                Console.Write("\r" + new string(' ', 40) + "\r");
        };
    }

    internal ChatSession(
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
        _engine = new ChatEngine(
            dbContext, knowledgeStore, responseEngine, spellChecker, posTagger, tokeniser,
            sentenceSplitter, svoExtractor, context, nounCategoriser, namePatterns, botCommands,
            greetingWords, botName, renamePatterns, sessionId, sessionLogger, toolRegistry,
            llmOrchestrator, intentClassifier, persona);
    }

    public void Start()
    {
        var welcome = $"Welcome to {_engine.BotName}!";
        var subtitle = "A chat bot that learns from you!";
        var exitHint = "Type 'quit' or 'exit' to leave.";
        Console.WriteLine(welcome);
        Console.WriteLine(subtitle);
        Console.WriteLine(exitHint);
        Console.WriteLine();
        _engine.LogSystem(welcome);
        _engine.LogSystem(subtitle);
        _engine.LogSystem(exitHint);

        var greeting = _engine.GetInitialGreeting();
        Console.WriteLine(greeting);
        _engine.LogSystem(greeting);

        while (true)
        {
            string input;
            if (_engine.IsInterviewActive && _engine.InterviewEngine != null)
            {
                string question;
                if (_engine.PendingFollowUp != null)
                {
                    question = _engine.PendingFollowUp;
                    _engine.PendingFollowUp = null;
                }
                else
                {
                    if (_engine.InterviewEngine.TurnsRemaining <= 0) { var s = _engine.EndInterview(); if (s != null) Console.WriteLine($"{_engine.BotName}: {s}"); continue; }
                    question = _engine.InterviewEngine.GenerateQuestion();
                    if (question == null) { var s = _engine.EndInterview(); if (s != null) Console.WriteLine($"{_engine.BotName}: {s}"); continue; }
                }

                _engine.LastInterviewQuestion = question;
                Console.WriteLine($"{_engine.BotName}: {question}");

                _engine.ClearPendingState();

                if (_engine.InterviewEngine is InterviewEngine)
                {
                    input = _engine.ProcessInput(question) ?? "";
                    if (string.IsNullOrEmpty(input)) { var s = _engine.EndInterview(); if (s != null) Console.WriteLine($"{_engine.BotName}: {s}"); continue; }
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine($"[{_engine.CurrentUserName}]: {input}");
                    Console.ResetColor();
                }
                else
                {
                    Console.Write("\nYou: ");
                    input = Console.ReadLine();
                    if (input == null) break;
                    if (string.IsNullOrWhiteSpace(input)) continue;
                    var stopCommand = input.Trim().ToLowerInvariant();
                    if (_engine.IsInterviewStopCommand(stopCommand))
                    {
                        var s = _engine.EndInterview();
                        if (s != null) Console.WriteLine($"{_engine.BotName}: {s}");
                        continue;
                    }
                }
            }
            else
            {
                if (_engine.IsInterviewActive) { var s = _engine.EndInterview(); if (s != null) Console.WriteLine($"{_engine.BotName}: {s}"); }
                Console.Write("\nYou: ");
                input = Console.ReadLine();
            }

            if (input == null) break;
            if (string.IsNullOrWhiteSpace(input))
                continue;

            if (_engine.ShouldExit(input))
            {
                _engine.RecordSessionMetrics();
                _engine.TryRetrainClassifier();
                _engine.Save();
                _engine.RunHomeworkCheck();
                var sessionSummary = _engine.GenerateSessionEndSummary();
                if (!string.IsNullOrEmpty(sessionSummary))
                {
                    Console.WriteLine($"{_engine.BotName}: {sessionSummary}");
                    _engine.LogSystem(sessionSummary);
                }
                var goodbye = "Goodbye! It was great chatting with you.";
                Console.WriteLine($"{_engine.BotName}: {goodbye}");
                _engine.LogSystem(goodbye);
                break;
            }

            if (!_engine.IsInterviewActive && _engine.CurrentUserId != null && _engine.IsInterviewTrigger(input))
            {
                var intro = _engine.StartInterview();
                if (intro != null) Console.WriteLine($"{_engine.BotName}: {intro}");
                continue;
            }

            var interviewUserId = _engine.CurrentUserId;
            if (_engine.IsInterviewActive && _engine.SavedUserId.HasValue)
                _engine.CurrentUserId = _engine.SavedUserId;
            var response = _engine.ProcessInput(input);
            if (_engine.IsInterviewActive)
                _engine.CurrentUserId = interviewUserId;
            _engine.SetContext("LastResponse", response);
            Console.WriteLine($"{_engine.BotName}: {response}");

            var reminderMsg = _engine.GetSessionStartReminderMessage();
            if (reminderMsg != null)
            {
                Console.WriteLine($"{_engine.BotName}: {reminderMsg}");
            }

            if (_engine.IsInterviewActive && _engine.InterviewEngine != null)
            {
                if (_engine.InterviewEngine is InterviewEngine && _engine.PendingFollowUp == null && _engine.FollowUpCount < 2 && IsInterviewFollowUp(response))
                {
                    _engine.PendingFollowUp = response;
                    _engine.FollowUpCount++;
                }
                else if (_engine.PendingFollowUp == null)
                {
                    _engine.FollowUpCount = 0;
                }

                _engine.InterviewEngine.AddExchange(_engine.LastInterviewQuestion ?? "", input, response);

                if (_engine.InterviewEngine is InterviewEngine)
                {
                    Console.Write("\n[Interview: Press Enter for next, or type 'stop' to end]: ");
                    var cmd = Console.ReadLine();
                    if (cmd != null)
                    {
                        cmd = cmd.Trim().ToLowerInvariant();
                        if (cmd.Length > 0 && _engine.IsInterviewStopCommand(cmd))
                        {
                            var s = _engine.EndInterview();
                            if (s != null) Console.WriteLine($"{_engine.BotName}: {s}");
                        }
                    }
                }
            }
        }
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

    public string GetInitialGreeting() => _engine.GetInitialGreeting();

    internal string ProcessInput(string input) => _engine.ProcessInput(input);
    internal string HandleNameInput(string input) => _engine.HandleNameInput(input);
    internal bool ShouldExit(string input) => _engine.ShouldExit(input);
    internal string ExtractName(string input, List<string> tokens) => _engine.ExtractName(input, tokens);
    internal string ResolveSubject(string subject) => _engine.ResolveSubject(subject);
    internal string ResolveObject(string obj) => _engine.ResolveObject(obj);
    internal PredicateType ClassifyPredicate(string subject, string verb, string obj) => _engine.ClassifyPredicate(subject, verb, obj);
    internal bool IsStopWord(string word) => _engine.IsStopWord(word);
    internal bool TryHandleBotRename(string input, out string response) => _engine.TryHandleBotRename(input, out response);
    internal bool TryHandleResetRequest(string input, out string response) => _engine.TryHandleResetRequest(input, out response);
    internal string HandleClarification(string input, string pendingWord) => _engine.HandleClarification(input, pendingWord);
    internal string HandleClassification(string input, string word) => _engine.HandleClassification(input, word);
    internal string HandleGameTurn(string input) => _engine.HandleGameTurn(input);
    internal bool TryHandleGameStart(string input, out string response) => _engine.TryHandleGameStart(input, out response);
    internal string ApplyGameGrammarFilter(string rawStory) => _engine.ApplyGameGrammarFilter(rawStory);
    internal string HandleMadLibsTurn(string input) => _engine.HandleMadLibsTurn(input);
    internal bool TryHandleMadLibsStart(string input, out string response) => _engine.TryHandleMadLibsStart(input, out response);
    internal string HandleJokeTurn() => _engine.HandleJokeTurn();
    internal bool TryHandleJokeStart(string input, out string response) => _engine.TryHandleJokeStart(input, out response);
    internal string HandleRiddleTurn(string input) => _engine.HandleRiddleTurn(input);
    internal bool TryHandleRiddleStart(string input, out string response) => _engine.TryHandleRiddleStart(input, out response);
    internal string? TryBuildCrossSessionRecall() => _engine.TryBuildCrossSessionRecall();
    internal bool IsInterviewTrigger(string input) => _engine.IsInterviewTrigger(input);
    internal bool IsInterviewStopCommand(string input) => _engine.IsInterviewStopCommand(input);
    internal void DetectFileMentions(string input) => _engine.DetectFileMentions(input);
    internal string? GetContextValue(string key) => _engine.GetContextValue(key);
    public string? GetSessionStartReminderMessage() => _engine.GetSessionStartReminderMessage();
    internal bool TryHandleMetaCommentary(string input, out string response) => _engine.TryHandleMetaCommentary(input, out response);
    internal IReadOnlyList<TopicEntry> TopicStack => _engine.TopicStack;
    internal string? LastSubject => _engine.LastSubject;
    internal string? LastObject => _engine.LastObject;
    internal double BotRenameAcceptProbability { get => _engine.BotRenameAcceptProbability; set => _engine.BotRenameAcceptProbability = value; }
    internal void RunHomeworkCheck() => _engine.RunHomeworkCheck();
    internal string HandleDictionarySaveConfirmation(string input, string saveData) => _engine.HandleDictionarySaveConfirmation(input, saveData);
    internal string HandleDictionaryDefinition(string input, string word) => _engine.HandleDictionaryDefinition(input, word);
    internal string HandlePlaceFollowUp(string input, string word) => _engine.HandlePlaceFollowUp(input, word);
    internal void SetLLMOfferState(string originalInput) => _engine.SetLLMOfferState(originalInput);
    internal void ProcessSentence(string sentence, string? sentiment = null, int intensity = 0) => _engine.ProcessSentence(sentence, sentiment, intensity);
    internal void LearnGreetingWords(string input) => _engine.LearnGreetingWords(input);
    internal bool TryHandleCorrection(string input, out string response) => _engine.TryHandleCorrection(input, out response);
    internal bool TryHandleWouldYouRather(string input, out string response) => _engine.TryHandleWouldYouRather(input, out response);
    internal string HandleWouldYouRatherAnswer(string input) => _engine.HandleWouldYouRatherAnswer(input);
    internal bool TryHandleHangmanStart(string input, out string response) => _engine.TryHandleHangmanStart(input, out response);
    internal string HandleHangmanTurn(string input) => _engine.HandleHangmanTurn(input);
    internal bool TryHandleQuizStart(string input, out string response) => _engine.TryHandleQuizStart(input, out response);
    internal string HandleQuizTurn(string input) => _engine.HandleQuizTurn(input);
    internal bool TryHandleReminderRequest(string input, out string response) => _engine.TryHandleReminderRequest(input, out response);

    public void Dispose()
    {
        _engine.Dispose();
    }
}

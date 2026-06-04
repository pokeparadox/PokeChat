using System.Text.RegularExpressions;
using PokeChat.Data;
using PokeChat.Knowledge;
using PokeChat.NLP;
using PokeChat.Responses;

namespace PokeChat.Core;

public class ChatSession : IDisposable
{
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
    private readonly string _sessionId = Guid.NewGuid().ToString();
    private string _currentUserNameLower = string.Empty;
    private Dictionary<string, List<string>>? _cachedBotResponses;
    private static readonly string[] AlternativeNames = { "Zara", "Nova", "Echo", "Pixel", "Azure", "Kai", "Rex" };
    private static readonly HashSet<string> Affirmations = new(StringComparer.OrdinalIgnoreCase)
        { "yes", "yep", "yeah", "yup", "sure", "correct", "right",
          "that's right", "that is right", "yes please", "ok", "okay" };

    private static readonly string[] ResetTriggers =
    {
        "start fresh",
        "start afresh",
        "start over",
        "reset everything",
        "reset all data",
        "forget everything",
        "wipe all memories",
        "wipe everything",
        "clear all data",
        "clear everything",
        "clear all memories",
        "fresh start",
    };

    public ChatSession()
    {
        _dbContext = new PokeChatDbContext();
        new DatabaseInitializer(_dbContext).Initialize();

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
        _responseEngine = new ResponseEngine(_knowledgeStore, _context, _spellChecker, _posTagger, _tokeniser, _svoExtractor);

        var spellDict = new HashSet<string>(posEntries.Select(e => e.Word), StringComparer.OrdinalIgnoreCase);
        var misspellings = _knowledgeStore.GetMisspellings();
        _spellChecker.Initialise(spellDict, misspellings);

        _namePatterns = _knowledgeStore.GetNamePatterns().Select(p => p.Pattern.ToLowerInvariant()).ToList();
        _botCommands = _knowledgeStore.GetBotCommands().Select(c => c.Command).ToHashSet(StringComparer.OrdinalIgnoreCase);
        _greetingWords = _knowledgeStore.GetGreetingWords().Select(gw => gw.Word.ToLowerInvariant()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        _renamePatterns = _knowledgeStore.GetBotRenamePatterns();
        _currentUserNameLower = _currentUserName.ToLowerInvariant();
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
        string sessionId = "")
    {
        _dbContext = dbContext;
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
        _currentUserNameLower = _currentUserName.ToLowerInvariant();
    }

    public void Start()
    {
        Console.WriteLine($"Welcome to {_botName}!");
        Console.WriteLine("A chat bot that learns from you!");
        Console.WriteLine("Type 'quit' or 'exit' to leave.");
        Console.WriteLine();

        Console.WriteLine(GreetingPool.GetRandomGreeting(_knowledgeStore, _botName));

        while (true)
        {
            Console.Write("\nYou: ");
            var input = Console.ReadLine();

            if (input == null) break;
            if (string.IsNullOrWhiteSpace(input))
                continue;

            if (ShouldExit(input))
            {
                var sessionSummary = GenerateSessionEndSummary();
                if (!string.IsNullOrEmpty(sessionSummary))
                    Console.WriteLine($"{_botName}: {sessionSummary}");
                Console.WriteLine($"{_botName}: Goodbye! It was great chatting with you.");
                break;
            }

            var response = ProcessInput(input);
            _context.SetContext(ContextKeys.LastResponse, response);
            Console.WriteLine($"{_botName}: {response}");
        }
    }

    internal string ProcessInput(string input)
    {
        if (_currentUserId == null)
        {
            return HandleNameInput(input);
        }

        var pendingWord = _context.GetContext(ContextKeys.PendingClarificationWord);
        if (pendingWord != null)
        {
            return HandleClarification(input, pendingWord);
        }

        var dictWord = _context.GetContext(ContextKeys.PendingDictionaryWord);
        if (dictWord != null)
        {
            return HandleDictionaryDefinition(input, dictWord);
        }

        _context.SetContext(ContextKeys.UnknownWords, null);

        if (TryHandleResetRequest(input, out var resetResponse))
            return resetResponse;

        if (TryHandleBotRename(input, out var renameResponse))
            return renameResponse;

        if (TryHandleCorrection(input, out var correctionResponse))
            return correctionResponse;

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

        var sentences = _sentenceSplitter.Split(input);

        foreach (var sentence in sentences)
        {
            ProcessSentence(sentence, sentiment, intensity);
        }

        _context.SetContext(ContextKeys.SessionId, _sessionId);
        _context.SetContext(ContextKeys.LastUserInput, input);

        var response = _responseEngine.GenerateResponse(input, _currentUserId);
        _knowledgeStore.StoreConversation(_currentUserId!.Value, input, response, _sessionId);
        _knowledgeStore.Save();
        return response;
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

        if (unknownWords.Count > 0)
        {
            var existing = _context.GetContext(ContextKeys.UnknownWords) ?? "";
            var existingWords = existing.Split(',', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
            foreach (var uw in unknownWords) existingWords.Add(uw);
            _context.SetContext(ContextKeys.UnknownWords, string.Join(",", existingWords));
        }

        var tags = _posTagger.Tag(correctedTokens);
        var triples = _svoExtractor.Extract(correctedTokens, tags);

        foreach (var triple in triples)
        {
            var resolvedSubject = ResolveSubject(triple.Subject);
            var resolvedObject = ResolveObject(triple.Object);

            var predicateType = ClassifyPredicate(resolvedSubject, triple.Verb, resolvedObject);
            var timeContext = _knowledgeStore.ExtractTimeContext(sentence) ?? _context.GetContext(ContextKeys.CurrentTimeContext);
            if (timeContext != null)
                _context.SetContext(ContextKeys.CurrentTimeContext, timeContext);

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

            var existingFact = _knowledgeStore.GetFact(resolvedSubject, triple.Verb, resolvedObject);
            if (existingFact == null)
            {
                _knowledgeStore.StoreFact(fact);
            }

            if (predicateType is PredicateType.GeneralFact or PredicateType.PersonalAttribute)
            {
                var lowerObj = resolvedObject.ToLowerInvariant();
                if (lowerObj is "a person" or "person")
                    _nounCategoriser.CategoriseNoun(resolvedSubject);
                else if (lowerObj is "a place" or "place")
                    _nounCategoriser.CategoriseNoun(resolvedSubject);
                else if (lowerObj is "a thing" or "thing")
                    _nounCategoriser.CategoriseNoun(resolvedSubject);
            }

            _context.UpdateLastSubject(resolvedSubject);
            _context.UpdateLastObject(resolvedObject);
        }

        if (triples.Count > 0)
        {
            _context.SetContext(ContextKeys.ContextFollowUpCount, "0");

            var lastTriple = triples[^1];
            var subjCat = _nounCategoriser.CategoriseNoun(ResolveSubject(lastTriple.Subject));
            var objCat = _nounCategoriser.CategoriseNoun(ResolveObject(lastTriple.Object));
            _context.SetContext(ContextKeys.SubjectCategory, subjCat);
            _context.SetContext(ContextKeys.ObjectCategory, objCat);
        }

    }

    internal string ResolveSubject(string subject)
    {
        var lower = subject.ToLowerInvariant();
        return lower switch
        {
            "i" or "me" or "my" or "myself" => _currentUserName,
            "we" or "us" or "our" => _currentUserName,
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

    internal PredicateType ClassifyPredicate(string subject, string verb, string obj)
    {
        var lowerVerb = verb.ToLowerInvariant();
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
            if (lower.StartsWith("yes") ||
                lower.StartsWith("yeah") ||
                lower.StartsWith("yep") ||
                lower.StartsWith("yup"))

            if (Affirmations.Contains(lower))
            {
                _knowledgeStore.AddMisspelling(pendingWord, pendingSuggestion);
                _spellChecker.AddToDictionary(pendingSuggestion);
                return $"Got it! I'll remember that '{pendingWord}' should be '{pendingSuggestion}'.";
            }
        }

        _knowledgeStore.AddLearnedWord(pendingWord);
        _spellChecker.AddToDictionary(pendingWord);
        return string.IsNullOrEmpty(pendingSuggestion)
            ? $"Thanks! I've learned the word '{pendingWord}'."
            : $"Okay, I've learned the word '{pendingWord}'.";
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
            return "I didn't catch your name. Could you tell me again?";
        }

        _currentUserName = char.ToUpper(name[0]) + name.Substring(1).ToLowerInvariant();
        _currentUserNameLower = _currentUserName.ToLowerInvariant();
        _currentUserId = _knowledgeStore.GetOrCreateUser(_currentUserName);

        var storedName = _knowledgeStore.GetUserBotName(_currentUserId!.Value);
        if (storedName != null)
            _botName = char.ToUpper(storedName[0]) + storedName.Substring(1).ToLowerInvariant();

        _context.Clear();
        _context.SetContext(ContextKeys.UserName, _currentUserName);

        return GetNameIntroResponse(_currentUserName);
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
            return tokens[0];
        }

        return string.Empty;
    }

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
        { "a", "an", "the", "is", "am", "are", "was", "were", "be", "been", "being" };

    internal bool IsStopWord(string word)
    {
        return StopWords.Contains(word);
    }

    internal bool ShouldExit(string input)
    {
        var lower = input.ToLowerInvariant().Trim();
        return _botCommands.Contains(lower);
    }

    internal bool TryHandleBotRename(string input, out string response)
    {
        var lowerInput = input.ToLowerInvariant();

        foreach (var pattern in _renamePatterns)
        {
            var idx = lowerInput.IndexOf(pattern, StringComparison.Ordinal);
            if (idx < 0) continue;

            var namePart = input.Substring(idx + pattern.Length).Trim();
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

        if (Random.Shared.NextDouble() < 0.85)
        {
            _knowledgeStore.SetUserBotName(_currentUserId!.Value, displayName);
            _knowledgeStore.Save();
            _botName = displayName;
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
        if (lowerInput is "that's not right" or "that is not right" or "not what i meant" or "wrong" or "not helpful")
        {
            _knowledgeStore.RecordFeedback(lastRuleId, _currentUserId!.Value, "negative", isLearned);
            _knowledgeStore.AdjustConfidence(lastRuleId, -2, isLearned);
            _knowledgeStore.Save();
            _context.SetContext(ContextKeys.LastRuleId, null);
            return GetCorrectionResponse("pattern_acknowledged", out response);
        }

        if (lowerInput is "that's exactly right" or "now you've got it" or "yes, that's it" or "perfect" or "that's better")
        {
            _knowledgeStore.RecordFeedback(lastRuleId, _currentUserId!.Value, "positive", isLearned);
            _knowledgeStore.AdjustConfidence(lastRuleId, 1, isLearned);
            _knowledgeStore.Save();
            _context.SetContext(ContextKeys.LastRuleId, null);
            return GetCorrectionResponse("pattern_acknowledged", out response);
        }

        response = string.Empty;
        return false;
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

    private string GenerateSessionEndSummary()
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

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}

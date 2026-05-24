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
    private string _currentUserNameLower = string.Empty;
    private Dictionary<string, List<string>>? _cachedBotResponses;
    private static readonly string[] AlternativeNames = { "Zara", "Nova", "Echo", "Pixel", "Azure", "Kai", "Rex" };
    private static readonly HashSet<string> Affirmations = new(StringComparer.OrdinalIgnoreCase)
        { "yes", "yep", "yeah", "yup", "sure", "correct", "right",
          "that's right", "that is right", "yes please", "ok", "okay" };

    public ChatSession()
    {
        _dbContext = new PokeChatDbContext();
        _dbContext.Database.EnsureCreated();
        DbSeeder.Seed(_dbContext);

        _knowledgeStore = new KnowledgeStore(_dbContext);
        _context = new ContextTracker();
        _spellChecker = new SpellChecker();
        _tokeniser = new Tokeniser();
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
        List<string>? renamePatterns = null)
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

        if (TryHandleBotRename(input, out var renameResponse))
            return renameResponse;

        LearnGreetingWords(input);

        var sentences = _sentenceSplitter.Split(input);

        foreach (var sentence in sentences)
        {
            ProcessSentence(sentence);
        }

        var response = _responseEngine.GenerateResponse(input, _currentUserId);
        _knowledgeStore.StoreConversation(_currentUserId!.Value, input, response);
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

    internal void ProcessSentence(string sentence)
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

            var fact = new Fact
            {
                UserId = _currentUserId,
                Subject = resolvedSubject,
                Verb = triple.Verb,
                Object = resolvedObject,
                PredicateType = predicateType.ToString(),
                CreatedAt = DateTime.UtcNow.ToString("O")
            };

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

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}

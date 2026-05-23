using PokeChat.Data;
using PokeChat.Knowledge;
using PokeChat.NLP;
using PokeChat.Responses;

namespace PokeChat.Core;

public class ChatSession
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
    private int? _currentUserId;
    private string _currentUserName = string.Empty;
    private readonly List<string> _namePatterns;
    private readonly HashSet<string> _botCommands;
    private readonly HashSet<string> _greetingWords;

    public ChatSession()
    {
        _dbContext = new PokeChatDbContext();
        DbSeeder.Seed(_dbContext);

        _knowledgeStore = new KnowledgeStore(_dbContext);
        _context = new ContextTracker();
        _spellChecker = new SpellChecker();
        _tokeniser = new Tokeniser();
        _sentenceSplitter = new SentenceSplitter();
        _svoExtractor = new SvoExtractor();
        var posEntries = _knowledgeStore.GetPosDictionary();
        _posTagger = new PosTagger(posEntries);
        _responseEngine = new ResponseEngine(_knowledgeStore, _context, _spellChecker, _posTagger, _tokeniser, _svoExtractor);

        var spellDict = new HashSet<string>(posEntries.Select(e => e.Word), StringComparer.OrdinalIgnoreCase);
        var misspellings = _knowledgeStore.GetMisspellings();
        _spellChecker.Initialise(spellDict, misspellings);

        _namePatterns = _knowledgeStore.GetNamePatterns().Select(p => p.Pattern.ToLowerInvariant()).ToList();
        _botCommands = _knowledgeStore.GetBotCommands().Select(c => c.Command.ToLowerInvariant()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        _greetingWords = _knowledgeStore.GetGreetingWords().Select(gw => gw.Word.ToLowerInvariant()).ToHashSet(StringComparer.OrdinalIgnoreCase);
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
        List<string> namePatterns,
        HashSet<string> botCommands,
        HashSet<string> greetingWords)
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
        _namePatterns = namePatterns;
        _botCommands = botCommands;
        _greetingWords = greetingWords;
    }

    public void Start()
    {
        Console.WriteLine("Welcome to PokeChat!");
        Console.WriteLine("A chat bot that learns from you!");
        Console.WriteLine("Type 'quit' or 'exit' to leave.");
        Console.WriteLine();

        Console.WriteLine(GreetingPool.GetRandomGreeting(_knowledgeStore));

        while (true)
        {
            Console.Write("\nYou: ");
            var input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input))
                continue;

            if (ShouldExit(input))
            {
                Console.WriteLine("PokeChat: Goodbye! It was great chatting with you.");
                break;
            }

            var response = ProcessInput(input);
            _context.SetContext(ContextKeys.LastResponse, response);
            Console.WriteLine($"PokeChat: {response}");
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

            _context.UpdateLastSubject(resolvedSubject);
            _context.UpdateLastObject(resolvedObject);
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
            if (lowerSubject == _currentUserName.ToLowerInvariant())
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
            var affirmations = new HashSet<string>
            {
                "yes", "yep", "yeah", "yup", "sure", "correct", "right",
                "that's right", "that is right", "yes please", "ok", "okay"
            };

            if (affirmations.Contains(lower) ||
                lower.StartsWith("yes") ||
                lower.StartsWith("yeah") ||
                lower.StartsWith("yep") ||
                lower.StartsWith("yup"))
            {
                _knowledgeStore.AddMisspelling(pendingWord, pendingSuggestion);
                _spellChecker.AddToDictionary(pendingSuggestion);
                return $"Got it! I'll remember that '{pendingWord}' should be '{pendingSuggestion}'.";
            }
            else
            {
                _knowledgeStore.AddLearnedWord(pendingWord);
                _spellChecker.AddToDictionary(pendingWord);
                return $"Okay, I've learned the word '{pendingWord}'.";
            }
        }
        else
        {
            _knowledgeStore.AddLearnedWord(pendingWord);
            _spellChecker.AddToDictionary(pendingWord);
            return $"Thanks! I've learned the word '{pendingWord}'.";
        }
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

        return GetRandomDictionaryResponse(word, definition);
    }

    private string GetRandomDictionaryResponse(string word, string definition)
    {
        var responses = new List<string>
        {
            $"Thanks! I've learned that {word} means {definition}.",
            $"Got it! {word}: {definition}. I'll remember that.",
            $"I understand now — {word} means {definition}. Thank you!",
            $"Great! I've added '{word}' to my vocabulary with the definition: {definition}."
        };

        return responses[Random.Shared.Next(responses.Count)];
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
        _currentUserId = _knowledgeStore.GetOrCreateUser(_currentUserName);

        _context.Clear();
        _context.SetContext(ContextKeys.UserName, _currentUserName);

        var greetings = new List<string>
        {
            $"Nice to meet you, {_currentUserName}! What would you like to talk about?",
            $"Hello {_currentUserName}! Feel free to share anything with me.",
            $"Great, {_currentUserName}! I'm ready to learn from our conversation.",
            $"Welcome, {_currentUserName}! Tell me about yourself or anything on your mind."
        };

        return greetings[Random.Shared.Next(greetings.Count)];
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

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}

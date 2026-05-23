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
        _responseEngine = new ResponseEngine(_knowledgeStore, _context, _spellChecker);

        var posEntries = _knowledgeStore.GetPosDictionary();
        PosTagger.Initialize(posEntries);

        var spellDict = new HashSet<string>(posEntries.Select(e => e.Word), StringComparer.OrdinalIgnoreCase);
        var misspellings = _knowledgeStore.GetMisspellings();
        _spellChecker.Initialize(spellDict, misspellings);

        _namePatterns = _knowledgeStore.GetNamePatterns().Select(p => p.Pattern.ToLowerInvariant()).ToList();
        _botCommands = _knowledgeStore.GetBotCommands().Select(c => c.Command.ToLowerInvariant()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        _greetingWords = _knowledgeStore.GetGreetingWords().Select(gw => gw.Word.ToLowerInvariant()).ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    internal ChatSession(
        PokeChatDbContext dbContext,
        KnowledgeStore knowledgeStore,
        ResponseEngine responseEngine,
        SpellChecker spellChecker,
        ContextTracker context,
        List<string> namePatterns,
        HashSet<string> botCommands,
        HashSet<string> greetingWords)
    {
        _dbContext = dbContext;
        _knowledgeStore = knowledgeStore;
        _responseEngine = responseEngine;
        _spellChecker = spellChecker;
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
            _context.SetContext("last_response", response);
            Console.WriteLine($"PokeChat: {response}");
        }
    }

    internal string ProcessInput(string input)
    {
        if (_currentUserId == null)
        {
            return HandleNameInput(input);
        }

        var pendingWord = _context.GetContext("pending_clarification_word");
        if (pendingWord != null)
        {
            return HandleClarification(input, pendingWord);
        }

        _context.SetContext("unknown_words", null);

        LearnGreetingWords(input);

        var sentences = SentenceSplitter.Split(input);

        foreach (var sentence in sentences)
        {
            ProcessSentence(sentence);
        }

        return _responseEngine.GenerateResponse(input, _currentUserId);
    }

    internal void LearnGreetingWords(string input)
    {
        var tokens = Tokenizer.Tokenize(input);
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
        var tokens = Tokenizer.Tokenize(sentence);
        var correctedTokens = _spellChecker.AutoCorrect(tokens);

        var unknownWords = _spellChecker.GetUnknownWords(correctedTokens);
        if (unknownWords.Count > 0)
        {
            var existing = _context.GetContext("unknown_words") ?? "";
            var existingWords = existing.Split(',', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
            foreach (var uw in unknownWords) existingWords.Add(uw);
            _context.SetContext("unknown_words", string.Join(",", existingWords));
        }

        var tags = PosTagger.Tag(correctedTokens);
        var triples = SvoExtractor.Extract(correctedTokens, tags);

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
                PredicateType = predicateType,
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

        _knowledgeStore.StoreConversation(_currentUserId!.Value, sentence, string.Empty);
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

    internal string ClassifyPredicate(string subject, string verb, string obj)
    {
        var lowerVerb = verb.ToLowerInvariant();
        var lowerSubject = subject.ToLowerInvariant();

        if (lowerVerb is "is" or "am" or "are" or "was" or "were")
        {
            if (lowerSubject == _currentUserName.ToLowerInvariant())
            {
                return "personal_attribute";
            }
            return "general_fact";
        }

        if (lowerVerb is "like" or "love" or "enjoy" or "prefer")
        {
            return "preference";
        }

        if (lowerVerb is "hate" or "dislike")
        {
            return "dislike";
        }

        if (lowerVerb is "have" or "has" or "own")
        {
            return "possession";
        }

        if (lowerVerb is "know" or "understand" or "believe")
        {
            return "belief";
        }

        return "general";
    }

    internal string HandleClarification(string input, string pendingWord)
    {
        var pendingSuggestion = _context.GetContext("pending_clarification_suggestion");
        _context.SetContext("pending_clarification_word", null);
        _context.SetContext("pending_clarification_suggestion", null);

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

    internal string HandleNameInput(string input)
    {
        var tokens = Tokenizer.Tokenize(input);
        var name = ExtractName(input, tokens);

        if (string.IsNullOrEmpty(name))
        {
            return "I didn't catch your name. Could you tell me again?";
        }

        _currentUserName = char.ToUpper(name[0]) + name.Substring(1).ToLowerInvariant();
        _currentUserId = _knowledgeStore.GetOrCreateUser(_currentUserName);

        _context.Clear();
        _context.SetContext("user_name", _currentUserName);

        var greetings = new List<string>
        {
            $"Nice to meet you, {_currentUserName}! What would you like to talk about?",
            $"Hello {_currentUserName}! Feel free to share anything with me.",
            $"Great, {_currentUserName}! I'm ready to learn from our conversation.",
            $"Welcome, {_currentUserName}! Tell me about yourself or anything on your mind."
        };

        var random = new Random();
        return greetings[random.Next(greetings.Count)];
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
                var nameTokens = Tokenizer.Tokenize(namePart);
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

    internal bool IsStopWord(string word)
    {
        var stopWords = new HashSet<string> { "a", "an", "the", "is", "am", "are", "was", "were", "be", "been", "being" };
        return stopWords.Contains(word.ToLowerInvariant());
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

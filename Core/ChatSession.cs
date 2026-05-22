using System;
using System.Collections.Generic;
using PokeChat.Core;
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
    private readonly ContextTracker _context;
    private int? _currentUserId;
    private string _currentUserName = string.Empty;
    private List<string> _namePatterns = new();
    private HashSet<string> _botCommands = new(StringComparer.OrdinalIgnoreCase);
    private HashSet<string> _greetingWords = new(StringComparer.OrdinalIgnoreCase);

    public ChatSession()
    {
        _dbContext = new PokeChatDbContext();
        DbSeeder.Seed(_dbContext);

        _knowledgeStore = new KnowledgeStore(_dbContext);
        _context = new ContextTracker();
        _responseEngine = new ResponseEngine(_knowledgeStore, _context);

        var posEntries = _knowledgeStore.GetPosDictionary();
        PosTagger.Initialize(posEntries);

        _namePatterns = _knowledgeStore.GetNamePatterns().Select(p => p.Pattern.ToLowerInvariant()).ToList();
        _botCommands = new HashSet<string>(_knowledgeStore.GetBotCommands().Select(c => c.Command.ToLowerInvariant()), StringComparer.OrdinalIgnoreCase);
        _greetingWords = new HashSet<string>(_knowledgeStore.GetGreetingWords().Select(gw => gw.Word.ToLowerInvariant()), StringComparer.OrdinalIgnoreCase);
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
            Console.WriteLine($"PokeChat: {response}");
        }
    }

    private string ProcessInput(string input)
    {
        if (_currentUserId == null)
        {
            return HandleNameInput(input);
        }

        LearnGreetingWords(input);

        var sentences = SentenceSplitter.Split(input);

        foreach (var sentence in sentences)
        {
            ProcessSentence(sentence);
        }

        return _responseEngine.GenerateResponse(input, _currentUserId);
    }

    private void LearnGreetingWords(string input)
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

    private void ProcessSentence(string sentence)
    {
        var tokens = Tokenizer.Tokenize(sentence);
        var tags = PosTagger.Tag(tokens);
        var triples = SvoExtractor.Extract(tokens, tags);

        foreach (var triple in triples)
        {
            var resolvedSubject = ResolveSubject(triple.Subject);
            var resolvedObject = ResolveObject(triple.Object_);

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

    private string ResolveSubject(string subject)
    {
        var lower = subject.ToLowerInvariant();
        return lower switch
        {
            "i" or "me" or "my" or "myself" => _currentUserName,
            "we" or "us" or "our" => _currentUserName,
            _ => subject
        };
    }

    private string ResolveObject(string obj)
    {
        var lower = obj.ToLowerInvariant();
        return lower switch
        {
            "it" or "this" or "that" => _context.ResolvePronoun(lower),
            _ => obj
        };
    }

    private string ClassifyPredicate(string subject, string verb, string obj)
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

    private string HandleNameInput(string input)
    {
        var tokens = Tokenizer.Tokenize(input);
        var name = ExtractName(input, tokens);

        if (string.IsNullOrEmpty(name))
        {
            return "I didn't catch your name. Could you tell me again?";
        }

        _currentUserName = char.ToUpper(name[0]) + name.Substring(1).ToLowerInvariant();
        _currentUserId = _knowledgeStore.GetOrCreateUser(_currentUserName);

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

    private string ExtractName(string input, List<string> tokens)
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

    private bool IsStopWord(string word)
    {
        var stopWords = new HashSet<string> { "a", "an", "the", "is", "am", "are", "was", "were", "be", "been", "being" };
        return stopWords.Contains(word.ToLowerInvariant());
    }

    private bool ShouldExit(string input)
    {
        var lower = input.ToLowerInvariant().Trim();
        return _botCommands.Contains(lower);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}

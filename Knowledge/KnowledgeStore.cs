using System;
using System.Collections.Generic;
using System.Linq;
using PokeChat.Data;
using PokeChat.Data.Entities;

namespace PokeChat.Knowledge;

public class KnowledgeStore
{
    private readonly PokeChatDbContext _context;

    public KnowledgeStore(PokeChatDbContext context)
    {
        _context = context;
    }

    public void StoreFact(Fact fact)
    {
        var entity = new FactEntity
        {
            UserId = fact.UserId,
            Subject = fact.Subject,
            Verb = fact.Verb,
            Object = fact.Object,
            PredicateType = fact.PredicateType,
            CreatedAt = fact.CreatedAt
        };

        _context.Facts.Add(entity);
        _context.SaveChanges();
    }

    public List<Fact> GetFactsBySubject(string subject)
    {
        return _context.Facts
            .Where(f => f.Subject == subject)
            .Select(f => MapToFact(f))
            .ToList();
    }

    public List<Fact> GetFactsByUser(int userId)
    {
        return _context.Facts
            .Where(f => f.UserId == userId)
            .Select(f => MapToFact(f))
            .ToList();
    }

    public Fact? GetFact(string subject, string verb, string obj)
    {
        var entity = _context.Facts
            .FirstOrDefault(f => f.Subject == subject && f.Verb == verb && f.Object == obj);

        return entity == null ? null : MapToFact(entity);
    }

    public List<Fact> GetAllFacts()
    {
        return _context.Facts
            .Select(f => MapToFact(f))
            .ToList();
    }

    public int? GetOrCreateUser(string name)
    {
        var existingUser = _context.Users.FirstOrDefault(u => u.Name == name);
        if (existingUser != null)
        {
            existingUser.LastSeen = DateTime.UtcNow.ToString("o");
            _context.SaveChanges();
            return existingUser.Id;
        }

        var newUser = new User
        {
            Name = name,
            FirstSeen = DateTime.UtcNow.ToString("o"),
            LastSeen = DateTime.UtcNow.ToString("o")
        };

        _context.Users.Add(newUser);
        _context.SaveChanges();
        return newUser.Id;
    }

    public void StoreConversation(int userId, string userInput, string botResponse)
    {
        var conversation = new Conversation
        {
            UserId = userId,
            UserInput = userInput,
            BotResponse = botResponse,
            Timestamp = DateTime.UtcNow.ToString("o")
        };

        _context.Conversations.Add(conversation);
        _context.SaveChanges();
    }

    public List<Greeting> GetGreetings()
    {
        return _context.Greetings.ToList();
    }

    public void AddGreeting(string text, bool isSystem = false)
    {
        var greeting = new Greeting
        {
            Text = text,
            IsSystem = isSystem,
            CreatedAt = DateTime.UtcNow.ToString("o")
        };

        _context.Greetings.Add(greeting);
        _context.SaveChanges();
    }

    public List<GreetingWord> GetGreetingWords()
    {
        return _context.GreetingWords.ToList();
    }

    public bool IsGreetingWord(string word)
    {
        return _context.GreetingWords.Any(gw => gw.Word == word.ToLowerInvariant());
    }

    public void AddGreetingWord(string word, int? learnedFromUserId = null)
    {
        var greetingWord = new GreetingWord
        {
            Word = word.ToLowerInvariant(),
            LearnedFromUserId = learnedFromUserId,
            CreatedAt = DateTime.UtcNow.ToString("o")
        };

        _context.GreetingWords.Add(greetingWord);
        _context.SaveChanges();
    }

    public List<ResponseRule> GetResponseRules()
    {
        return _context.ResponseRules
            .Where(r => r.IsActive)
            .ToList();
    }

    public List<string> GetResponsesForRule(int ruleId)
    {
        return _context.ResponseRuleResponses
            .Where(r => r.RuleId == ruleId)
            .Select(r => r.ResponseText)
            .ToList();
    }

    public List<PosDictionaryEntry> GetPosDictionary()
    {
        return _context.PosDictionary.ToList();
    }

    public List<NamePattern> GetNamePatterns()
    {
        return _context.NamePatterns.ToList();
    }

    public List<BotCommand> GetBotCommands()
    {
        return _context.BotCommands.ToList();
    }

    private static Fact MapToFact(FactEntity entity)
    {
        return new Fact
        {
            Id = entity.Id,
            UserId = entity.UserId,
            Subject = entity.Subject,
            Verb = entity.Verb,
            Object = entity.Object,
            PredicateType = entity.PredicateType,
            CreatedAt = entity.CreatedAt
        };
    }
}

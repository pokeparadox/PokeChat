using Facet.Extensions;
using PokeChat.Data;
using PokeChat.Data.Entities;

namespace PokeChat.Knowledge;

public class KnowledgeStore(PokeChatDbContext context)
{
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

        context.Facts.Add(entity);
        context.SaveChanges();
    }

    public List<Fact> GetFactsBySubject(string subject)
    {
        return context.Facts
            .Where(f => f.Subject == subject)
            .SelectFacet<Fact>()
            .ToList();
    }

    public List<Fact> GetFactsByUser(int userId)
    {
        return context.Facts
            .Where(f => f.UserId == userId)
            .SelectFacet<Fact>()
            .ToList();
    }

    public Fact? GetFact(string subject, string verb, string obj)
    {
        var entity = context.Facts
            .SelectFacet<Fact>()
            .FirstOrDefault(f => f.Subject == subject && f.Verb == verb && f.Object == obj);

        return entity;
    }

    public List<Fact> GetAllFacts()
    {
        return context.Facts
            .SelectFacet<Fact>()
            .ToList();
    }

    public int? GetOrCreateUser(string name)
    {
        var existingUser = context.Users.FirstOrDefault(u => u.Name == name);
        if (existingUser != null)
        {
            existingUser.LastSeen = DateTime.UtcNow.ToString("o");
            context.SaveChanges();
            return existingUser.Id;
        }

        var newUser = new User
        {
            Name = name,
            FirstSeen = DateTime.UtcNow.ToString("o"),
            LastSeen = DateTime.UtcNow.ToString("o")
        };

        context.Users.Add(newUser);
        context.SaveChanges();
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

        context.Conversations.Add(conversation);
        context.SaveChanges();
    }

    public List<Greeting> GetGreetings()
    {
        return context.Greetings.ToList();
    }

    public void AddGreeting(string text, bool isSystem = false)
    {
        var greeting = new Greeting
        {
            Text = text,
            IsSystem = isSystem,
            CreatedAt = DateTime.UtcNow.ToString("o")
        };

        context.Greetings.Add(greeting);
        context.SaveChanges();
    }

    public List<GreetingWord> GetGreetingWords()
    {
        return context.GreetingWords.ToList();
    }

    public bool IsGreetingWord(string word)
    {
        return context.GreetingWords.Any(gw => gw.Word == word.ToLowerInvariant());
    }

    public void AddGreetingWord(string word, int? learnedFromUserId = null)
    {
        var greetingWord = new GreetingWord
        {
            Word = word.ToLowerInvariant(),
            LearnedFromUserId = learnedFromUserId,
            CreatedAt = DateTime.UtcNow.ToString("o")
        };

        context.GreetingWords.Add(greetingWord);
        context.SaveChanges();
    }

    public List<ResponseRule> GetResponseRules()
    {
        return context.ResponseRules
            .Where(r => r.IsActive)
            .ToList();
    }

    public List<string> GetResponsesForRule(int ruleId)
    {
        return context.ResponseRuleResponses
            .Where(r => r.RuleId == ruleId)
            .Select(r => r.ResponseText)
            .ToList();
    }

    public List<PosDictionaryEntry> GetPosDictionary()
    {
        return context.PosDictionary.ToList();
    }

    public List<NamePattern> GetNamePatterns()
    {
        return context.NamePatterns.ToList();
    }

    public List<BotCommand> GetBotCommands()
    {
        return context.BotCommands.ToList();
    }

    public Dictionary<string, string> GetMisspellings()
    {
        return context.Misspellings
            .ToDictionary(m => m.WrongWord, m => m.Correction, StringComparer.OrdinalIgnoreCase);
    }

    public void AddMisspelling(string misspelling, string correction)
    {
        var entry = new Misspelling
        {
            WrongWord = misspelling.ToLowerInvariant(),
            Correction = correction.ToLowerInvariant(),
            CreatedAt = DateTime.UtcNow.ToString("o")
        };

        context.Misspellings.Add(entry);
        context.SaveChanges();
    }

    public string? GetCorrection(string misspelling)
    {
        return context.Misspellings
            .Where(m => m.WrongWord == misspelling.ToLowerInvariant())
            .Select(m => m.Correction)
            .FirstOrDefault();
    }

    public bool IsWordKnown(string word)
    {
        return context.PosDictionary.Any(p => p.Word == word.ToLowerInvariant());
    }

    public void AddLearnedWord(string word)
    {
        var entry = new PosDictionaryEntry
        {
            Word = word.ToLowerInvariant(),
            WordType = "unknown",
            CreatedAt = DateTime.UtcNow.ToString("o")
        };

        context.PosDictionary.Add(entry);
        context.SaveChanges();
    }
}

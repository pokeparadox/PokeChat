using Facet.Extensions;
using Microsoft.EntityFrameworkCore;
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
            .Where(f => f.Subject == subject && f.Verb == verb && f.Object == obj)
            .SelectFacet<Fact>()
            .FirstOrDefault();

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
    }

    public List<ResponseRule> GetResponseRules()
    {
        return context.ResponseRules
            .Include(r => r.Responses)
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

    public void Save()
    {
        context.SaveChanges();
    }

    public Dictionary<string, List<string>> GetBotResponses()
    {
        return context.BotResponses
            .GroupBy(r => r.Category)
            .ToDictionary(g => g.Key, g => g.Select(r => r.ResponseText).ToList());
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
    }

    public List<WordDefinition> GetDefinitions(string word)
    {
        return context.WordDefinitions
            .Where(d => d.Word == word.ToLowerInvariant())
            .ToList();
    }

    public WordDefinition? GetDefinition(string word)
    {
        return context.WordDefinitions
            .Where(d => d.Word == word.ToLowerInvariant())
            .FirstOrDefault();
    }

    public void SetDefinition(string word, string definition, int? userId = null)
    {
        var entry = new WordDefinition
        {
            Word = word.ToLowerInvariant(),
            Definition = definition,
            DefinedByUserId = userId,
            CreatedAt = DateTime.UtcNow.ToString("o")
        };

        context.WordDefinitions.Add(entry);
    }

    public void AddWordLink(string sourceWord, string targetWord, string linkType, int? userId = null)
    {
        var link = new WordLink
        {
            SourceWord = sourceWord.ToLowerInvariant(),
            TargetWord = targetWord.ToLowerInvariant(),
            LinkType = linkType.ToLowerInvariant(),
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow.ToString("o")
        };

        context.WordLinks.Add(link);
    }

    public List<string> GetRelatedWords(string word, string? linkType = null)
    {
        var query = context.WordLinks
            .Where(l => l.SourceWord == word.ToLowerInvariant());

        if (!string.IsNullOrEmpty(linkType))
            query = query.Where(l => l.LinkType == linkType.ToLowerInvariant());

        return query.Select(l => l.TargetWord).Distinct().ToList();
    }

    public List<string> GetRelatedTo(string word, string? linkType = null)
    {
        var query = context.WordLinks
            .Where(l => l.TargetWord == word.ToLowerInvariant());

        if (!string.IsNullOrEmpty(linkType))
            query = query.Where(l => l.LinkType == linkType.ToLowerInvariant());

        return query.Select(l => l.SourceWord).Distinct().ToList();
    }

    public string? CategoriseNoun(string noun)
    {
        return context.NounCategories
            .Where(n => n.Noun == noun.ToLowerInvariant())
            .Select(n => n.Category)
            .FirstOrDefault();
    }

    public void AddNounCategory(string noun, string category, int? userId = null)
    {
        var entry = new NounCategory
        {
            Noun = noun.ToLowerInvariant(),
            Category = category.ToLowerInvariant(),
            LearnedFromUserId = userId,
            CreatedAt = DateTime.UtcNow.ToString("o")
        };

        context.NounCategories.Add(entry);
    }

    public List<NounCategory> GetNounCategories()
    {
        return context.NounCategories.ToList();
    }

    public string? GetUserBotName(int userId)
    {
        return context.UserBotNames
            .Where(u => u.UserId == userId)
            .Select(u => u.BotName)
            .FirstOrDefault();
    }

    public void SetUserBotName(int userId, string name)
    {
        var existing = context.UserBotNames.FirstOrDefault(u => u.UserId == userId);
        if (existing != null)
        {
            existing.BotName = name;
        }
        else
        {
            context.UserBotNames.Add(new UserBotName
            {
                UserId = userId,
                BotName = name,
                CreatedAt = DateTime.UtcNow.ToString("O")
            });
        }
    }

    public List<string> GetBotRenamePatterns()
    {
        return context.BotRenamePatterns.Select(p => p.Pattern.ToLowerInvariant()).ToList();
    }

    public List<string> SearchDictionary(string partial)
    {
        var lower = partial.ToLowerInvariant();
        return context.PosDictionary
            .Where(p => p.Word.StartsWith(lower))
            .Select(p => p.Word)
            .Distinct()
            .Take(10)
            .ToList();
    }
}

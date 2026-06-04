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
            Sentiment = fact.Sentiment,
            EmotionIntensity = fact.EmotionIntensity,
            TimeContext = fact.TimeContext,
            MentionedAt = fact.MentionedAt,
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

    public void StoreConversation(int userId, string userInput, string botResponse, string? sessionId = null)
    {
        var conversation = new Conversation
        {
            UserId = userId,
            UserInput = userInput,
            BotResponse = botResponse,
            Timestamp = DateTime.UtcNow.ToString("o"),
            SessionId = sessionId
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

    public List<EmotionKeyword> GetEmotionKeywords()
    {
        return context.EmotionKeywords.ToList();
    }

    public (string? Sentiment, int Intensity) AnalyseSentiment(string input)
    {
        var keywords = GetEmotionKeywords();
        if (keywords.Count == 0)
            return (null, 0);

        var lowerInput = input.ToLowerInvariant();
        var words = lowerInput.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var scores = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var intensities = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var keyword in keywords)
        {
            if (lowerInput.Contains(keyword.Word))
            {
                if (!scores.ContainsKey(keyword.Sentiment))
                {
                    scores[keyword.Sentiment] = 0;
                    intensities[keyword.Sentiment] = 0;
                }
                scores[keyword.Sentiment]++;
                if (keyword.Intensity > intensities[keyword.Sentiment])
                    intensities[keyword.Sentiment] = keyword.Intensity;
            }
        }

        if (scores.Count == 0)
            return ("neutral", 0);

        var dominant = scores.MaxBy(kv => kv.Value);
        return (dominant.Key, intensities[dominant.Key]);
    }

    public void UpdateFactSentiment(int factId, string sentiment, int intensity)
    {
        var fact = context.Facts.Find(factId);
        if (fact != null)
        {
            fact.Sentiment = sentiment;
            fact.EmotionIntensity = intensity;
        }
    }

    public void ResetAllUserData()
    {
        context.Database.ExecuteSqlRaw("DELETE FROM ConversationSessions");
        context.Database.ExecuteSqlRaw("DELETE FROM Conversations");
        context.Database.ExecuteSqlRaw("DELETE FROM Facts");
        context.Database.ExecuteSqlRaw("DELETE FROM WordDefinitions");
        context.Database.ExecuteSqlRaw("DELETE FROM WordLinks");
        context.Database.ExecuteSqlRaw("DELETE FROM GreetingWords WHERE LearnedFromUserId IS NOT NULL");
        context.Database.ExecuteSqlRaw("DELETE FROM NounCategories WHERE LearnedFromUserId IS NOT NULL");
        context.Database.ExecuteSqlRaw("DELETE FROM UserBotNames");
        context.Database.ExecuteSqlRaw("DELETE FROM PosDictionary WHERE WordType = 'unknown'");
        context.Database.ExecuteSqlRaw("DELETE FROM Users");
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
                CreatedAt = DateTime.UtcNow.ToString("o")
            });
        }
    }

    public List<string> GetBotRenamePatterns()
    {
        return context.BotRenamePatterns.Select(p => p.Pattern.ToLowerInvariant()).ToList();
    }

    public List<ContractionEntity> GetContractions()
    {
        return context.Contractions.ToList();
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

    public List<TemporalExpression> GetTemporalExpressions()
    {
        return context.TemporalExpressions.ToList();
    }

    public string? ExtractTimeContext(string input)
    {
        var expressions = GetTemporalExpressions();
        var lowerInput = input.ToLowerInvariant();

        string? bestMatch = null;
        var bestAbsOffset = 0;

        foreach (var expr in expressions)
        {
            if (lowerInput.Contains(expr.Expression))
            {
                var absOffset = System.Math.Abs(expr.DaysOffset);
                if (bestMatch == null || absOffset > bestAbsOffset)
                {
                    bestMatch = expr.Expression;
                    bestAbsOffset = absOffset;
                }
            }
        }

        return bestMatch;
    }

    public List<Fact> GetFactsByTimeRange(DateTime from, DateTime to, int? userId = null)
    {
        var fromStr = from.ToString("o");
        var toStr = to.ToString("o");

        var query = context.Facts
            .Where(f => string.Compare(f.CreatedAt, fromStr) >= 0 && string.Compare(f.CreatedAt, toStr) <= 0);

        if (userId.HasValue)
            query = query.Where(f => f.UserId == userId.Value);

        return query.SelectFacet<Fact>().ToList();
    }

    public List<Fact> GetFactsWithTimeContext(int userId, string timeContext)
    {
        return context.Facts
            .Where(f => f.UserId == userId && f.TimeContext == timeContext)
            .SelectFacet<Fact>()
            .ToList();
    }

    public List<string> GetCategoryChain(string word)
    {
        var lower = word.ToLowerInvariant();
        var categories = new List<string>();
        var visited = new HashSet<string>();

        var queue = new Queue<string>();
        queue.Enqueue(lower);
        visited.Add(lower);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            var parents = context.WordLinks
                .Where(l => l.SourceWord == current && l.LinkType == "is_a")
                .Select(l => l.TargetWord)
                .Distinct()
                .ToList();

            foreach (var parent in parents)
            {
                if (visited.Add(parent))
                {
                    categories.Add(parent);
                    queue.Enqueue(parent);
                }
            }
        }

        return categories;
    }

    public List<string> GetAllOfType(string categoryWord)
    {
        var lower = categoryWord.ToLowerInvariant();
        return context.WordLinks
            .Where(l => l.TargetWord == lower && l.LinkType == "is_a")
            .Select(l => l.SourceWord)
            .Distinct()
            .ToList();
    }

    public Fact? InferPreference(int userId, string category)
    {
        var members = GetAllOfType(category);
        if (members.Count == 0) return null;

        var preferenceVerbs = new[] { "like", "love", "enjoy", "prefer", "hate", "dislike" };

        return context.Facts
            .Where(f => f.UserId == userId && preferenceVerbs.Contains(f.Verb))
            .SelectFacet<Fact>()
            .ToList()
            .FirstOrDefault(f => members.Contains(f.Object.ToLowerInvariant()));
    }

    public Fact? DetectContradiction(int userId, string subject, string verb, string obj)
    {
        var lowerVerb = verb.ToLowerInvariant();
        var lowerObj = obj.ToLowerInvariant();

        var oppositeVerbs = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            { "like", new[] { "hate", "dislike" } },
            { "love", new[] { "hate", "dislike" } },
            { "enjoy", new[] { "hate", "dislike" } },
            { "prefer", new[] { "hate", "dislike" } },
            { "hate", new[] { "like", "love", "enjoy", "prefer" } },
            { "dislike", new[] { "like", "love", "enjoy", "prefer" } },
        };

        var verbsToCheck = new List<string> { lowerVerb };
        if (oppositeVerbs.TryGetValue(lowerVerb, out var opposites))
            verbsToCheck.AddRange(opposites);

        var existingFacts = context.Facts
            .Where(f => f.UserId == userId && f.Subject == subject)
            .SelectFacet<Fact>()
            .ToList();

        return existingFacts.FirstOrDefault(f =>
            verbsToCheck.Contains(f.Verb.ToLowerInvariant()) &&
            string.Equals(f.Object, lowerObj, StringComparison.OrdinalIgnoreCase));
    }

    public List<Fact> GetTransitiveFacts(string subject, string relation, int maxDepth)
    {
        var lowerSubject = subject.ToLowerInvariant();
        var lowerRelation = relation.ToLowerInvariant();

        var visited = new HashSet<string>();
        var results = new List<Fact>();

        var queue = new Queue<(string Word, int Depth)>();
        queue.Enqueue((lowerSubject, 0));
        visited.Add(lowerSubject);

        while (queue.Count > 0)
        {
            var (current, depth) = queue.Dequeue();
            if (depth > 0)
            {
                var facts = context.Facts
                    .Where(f => f.Subject == current)
                    .SelectFacet<Fact>()
                    .ToList();

                results.AddRange(facts);
            }

            if (depth < maxDepth)
            {
                var links = context.WordLinks
                    .Where(l => l.SourceWord == current && l.LinkType == lowerRelation)
                    .Select(l => l.TargetWord)
                    .Distinct()
                    .ToList();

                foreach (var target in links)
                {
                    if (visited.Add(target))
                        queue.Enqueue((target, depth + 1));
                }
            }
        }

        return results;
    }

    public void CreateConversationSession(string sessionGuid, int userId)
    {
        var session = new ConversationSession
        {
            SessionGuid = sessionGuid,
            UserId = userId,
            StartedAt = DateTime.UtcNow.ToString("o"),
            TurnCount = 0
        };
        context.ConversationSessions.Add(session);
    }

    public void EndConversationSession(string sessionGuid)
    {
        var session = context.ConversationSessions.FirstOrDefault(s => s.SessionGuid == sessionGuid);
        if (session != null)
        {
            session.EndedAt = DateTime.UtcNow.ToString("o");
        }
    }

    public int GetSessionConversationCount(string sessionGuid)
    {
        return context.Conversations.Count(c => c.SessionId == sessionGuid);
    }

    public string BuildSessionSummary(int userId, string sessionId)
    {
        var conversations = context.Conversations
            .Where(c => c.SessionId == sessionId && c.UserId == userId)
            .OrderBy(c => c.Timestamp)
            .ToList();

        if (conversations.Count == 0)
            return string.Empty;

        var facts = context.Facts
            .Where(f => f.UserId == userId)
            .SelectFacet<Fact>()
            .ToList();

        var sessionFactSignatures = new HashSet<string>();
        foreach (var conv in conversations)
        {
            var lowerInput = conv.UserInput.ToLowerInvariant();
            var matchingFacts = facts.Where(f =>
                lowerInput.Contains(f.Object.ToLowerInvariant()) &&
                (lowerInput.Contains(f.Subject.ToLowerInvariant()) ||
                 lowerInput.Contains(f.Verb.ToLowerInvariant())));
            foreach (var f in matchingFacts)
                sessionFactSignatures.Add($"{f.Subject} {f.Verb} {f.Object}");
        }

        if (sessionFactSignatures.Count == 0)
        {
            foreach (var conv in conversations)
            {
                var lowerInput = conv.UserInput.ToLowerInvariant();
                var objectMatches = facts.Where(f =>
                    lowerInput.Contains(f.Object.ToLowerInvariant()));
                foreach (var f in objectMatches)
                    sessionFactSignatures.Add($"{f.Subject} {f.Verb} {f.Object}");
            }
        }

        var factList = sessionFactSignatures.ToList();

        if (factList.Count == 0)
            return string.Empty;

        if (factList.Count <= 2)
            return string.Join("; ", factList);

        var numbered = factList.Select((f, i) => $"{i + 1}) {f}");
        return string.Join(". ", numbered);
    }
}

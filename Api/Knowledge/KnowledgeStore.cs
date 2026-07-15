using System.Text.RegularExpressions;
using Facet.Extensions;
using Microsoft.EntityFrameworkCore;
using PokeChat.Data;
using PokeChat.Data.Entities;
using PokeChat.Core;
using PokeChat.Responses;
using PokeChat.Stories;
using PokeChat.ML;

namespace PokeChat.Knowledge;

internal static class SummaryFilters
{
    private static readonly HashSet<string> Interrogatives = new(StringComparer.OrdinalIgnoreCase)
        { "what", "who", "where", "when", "why", "how" };

    public static bool IsGarbageFact(Fact fact)
    {
        if (Interrogatives.Contains(fact.Subject))
            return true;
        if (string.Equals(fact.Subject, "you", StringComparison.OrdinalIgnoreCase) &&
            fact.Verb is "be" or "do" or "have" or "is" or "am" or "are" or "was" or "were")
            return true;
        return false;
    }
}

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
            CreatedAt = fact.CreatedAt,
            Confidence = 1.0
        };

        context.Facts.Add(entity);
    }

    public bool TryEndorseFact(string subject, string verb, string obj, int endorsingUserId)
    {
        var existing = context.Facts
            .Where(f => f.Subject == subject && f.Verb == verb && f.Object == obj
                        && f.UserId != null && f.UserId != endorsingUserId)
            .OrderByDescending(f => f.Confidence)
            .FirstOrDefault();

        if (existing == null) return false;

        existing.Confidence = System.Math.Min(5.0, existing.Confidence + 0.5);

        context.FactEndorsements.Add(new FactEndorsement
        {
            FactId = existing.Id,
            UserId = endorsingUserId,
            CreatedAt = DateTime.UtcNow.ToString("o")
        });

        if (existing.Confidence >= 3.0 && existing.UserId.HasValue)
        {
            existing.UserId = null;
        }

        return true;
    }

    public List<FactEndorsement> GetEndorsements(int factId)
    {
        return context.FactEndorsements
            .Where(e => e.FactId == factId)
            .OrderByDescending(e => e.Id)
            .ToList();
    }

    public List<Fact> GetPopularFacts(int minConfidence = 3)
    {
        return context.Facts
            .Where(f => f.Confidence >= minConfidence)
            .SelectFacet<Fact>()
            .OrderByDescending(f => f.Confidence)
            .ToList();
    }

    public int GetEndorsementCount(int factId)
    {
        return context.FactEndorsements.Count(e => e.FactId == factId);
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
        var entities = context.Facts
            .Where(f => f.UserId == userId)
            .ToList();

        var now = DateTime.UtcNow.ToString("o");
        foreach (var e in entities)
        {
            e.LastAccessed = now;
            e.AccessCount++;
        }

        return entities.AsQueryable().SelectFacet<Fact>().ToList();
    }

    public string? GetUserFactsFormatted(int userId)
    {
        var facts = GetFactsByUser(userId);
        if (facts.Count == 0) return null;

        if (facts.Count > 10)
        {
            var grouped = facts
                .GroupBy(f => f.PredicateType)
                .Select(g =>
                {
                    var items = string.Join(", ", g.Select(f => $"{f.Subject} {f.Verb} {f.Object}"));
                    return $"{g.Key}: {items}";
                });
            return string.Join("\n", grouped);
        }

        var numbered = facts.Select((f, i) => $"{i + 1}) {f.Subject} {f.Verb} {f.Object}");
        return string.Join("\n", numbered);
    }

    public string? GetUserStatsFormatted(int userId)
    {
        var facts = context.Facts.Where(f => f.UserId == userId);
        var totalFacts = facts.Count();

        if (totalFacts == 0) return null;

        var totalConversations = context.Conversations.Count(c => c.UserId == userId);

        var totalSessions = context.Conversations
            .Where(c => c.UserId == userId)
            .Select(c => c.SessionId)
            .Distinct()
            .Count();

        var mostTalkedSubject = facts
            .GroupBy(f => f.Subject)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefault();

        var sentiments = facts
            .Where(f => f.Sentiment != null && f.Sentiment != "neutral")
            .GroupBy(f => f.Sentiment)
            .Select(g => $"{g.Key}: {g.Count()}")
            .ToList();
        var sentimentBreakdown = sentiments.Count > 0 ? string.Join(", ", sentiments) : null;

        var allConversations = context.Conversations
            .Where(c => c.UserId == userId)
            .OrderBy(c => c.Timestamp)
            .ToList();

        string? firstDate = null;
        string? lastDate = null;
        if (allConversations.Count > 0)
        {
            if (DateTime.TryParse(allConversations[0].Timestamp, out var first))
                firstDate = first.ToString("MMM dd yyyy");
            if (DateTime.TryParse(allConversations[^1].Timestamp, out var last))
                lastDate = last.ToString("MMM dd yyyy");
        }

        var lines = new List<string>
        {
            $"Total facts: {totalFacts}",
            $"Conversations: {totalConversations}",
            $"Sessions: {totalSessions}",
        };

        if (mostTalkedSubject != null)
            lines.Add($"Most talked about: {mostTalkedSubject}");
        if (sentimentBreakdown != null)
            lines.Add($"Sentiment: {sentimentBreakdown}");
        if (firstDate != null)
            lines.Add($"First chat: {firstDate}");
        if (lastDate != null)
            lines.Add($"Last chat: {lastDate}");

        return string.Join("\n", lines);
    }

    public List<Fact> GetPositiveFacts(int userId)
    {
        var positiveVerbs = new[] { "like", "love", "enjoy", "prefer" };
        return context.Facts
            .Where(f => f.UserId == userId && positiveVerbs.Contains(f.Verb))
            .SelectFacet<Fact>()
            .ToList();
    }

    public Fact? GetRandomPositiveFact(int userId)
    {
        var facts = GetPositiveFacts(userId);
        if (facts.Count == 0) return null;
        return facts[Random.Shared.Next(facts.Count)];
    }

    public List<Fact> GetUserPreferences(int userId)
    {
        var preferenceVerbs = new[] { "like", "love", "enjoy", "prefer" };
        return context.Facts
            .Where(f => f.UserId == userId && preferenceVerbs.Contains(f.Verb))
            .SelectFacet<Fact>()
            .ToList();
    }

    public List<Fact> GetUserDislikes(int userId)
    {
        var dislikeVerbs = new[] { "hate", "dislike" };
        return context.Facts
            .Where(f => f.UserId == userId && dislikeVerbs.Contains(f.Verb))
            .SelectFacet<Fact>()
            .ToList();
    }

    public (string? LikedItem, string? Suggestion, string? Category) GetRecommendation(int userId)
    {
        var likes = GetUserPreferences(userId);
        if (likes.Count < 2)
            return (null, null, null);

        var likedObject = likes[Random.Shared.Next(likes.Count)].Object;

        var categories = GetCategoryChain(likedObject);
        if (categories.Count == 0)
            return (null, null, null);

        var knownObjects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var userFacts = context.Facts
            .Where(f => f.UserId == userId)
            .SelectFacet<Fact>()
            .ToList();
        foreach (var f in userFacts)
            knownObjects.Add(f.Object.ToLowerInvariant());

        foreach (var category in categories.OrderBy(_ => Random.Shared.Next()))
        {
            var members = GetAllOfType(category);
            var unexplored = members
                .Where(m => !string.Equals(m, likedObject, StringComparison.OrdinalIgnoreCase))
                .Where(m => !knownObjects.Contains(m.ToLowerInvariant()))
                .ToList();

            if (unexplored.Count > 0)
                return (likedObject, unexplored[Random.Shared.Next(unexplored.Count)], category);
        }

        return (null, null, null);
    }

    public Fact? GetFact(string subject, string verb, string obj, int? userId = null)
    {
        var entity = context.Facts
            .Where(f => f.Subject == subject && f.Verb == verb && f.Object == obj);

        if (userId.HasValue)
            entity = entity.Where(f => f.UserId == userId.Value);

        var fact = entity.FirstOrDefault();
        if (fact == null) return null;

        TouchFactAccess(fact);
        return new[] { fact }.AsQueryable().SelectFacet<Fact>().First();
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

    public Conversation StoreConversation(int userId, string userInput, string botResponse, string? sessionId = null)
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
        return conversation;
    }

    public Conversation StoreConversation(int userId, string userInput, string botResponse, string? sessionId, string? responseCategory)
    {
        var conversation = new Conversation
        {
            UserId = userId,
            UserInput = userInput,
            BotResponse = botResponse,
            Timestamp = DateTime.UtcNow.ToString("o"),
            SessionId = sessionId,
            ResponseCategory = responseCategory
        };

        context.Conversations.Add(conversation);
        return conversation;
    }

    public List<Greeting> GetGreetings(string? persona = null)
    {
        var query = context.Greetings.AsQueryable();
        if (persona != null)
            query = query.Where(g => g.Persona == null || g.Persona == persona);
        return query.ToList();
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

    public List<ResponseRule> GetResponseRules(string? persona = null)
    {
        var query = context.ResponseRules
            .Include(r => r.Responses)
            .Where(r => r.IsActive);
        if (persona != null)
            query = query.Where(r => r.Persona == null || r.Persona == persona);
        return query.ToList();
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
        context.Database.ExecuteSqlRaw("DELETE FROM TurnRates");
        context.Database.ExecuteSqlRaw("DELETE FROM ResponseFeedbacks");
        context.Database.ExecuteSqlRaw("DELETE FROM LearnedResponseRules");
        context.Database.ExecuteSqlRaw("DELETE FROM ConversationMetrics");
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

    public Dictionary<string, List<string>> GetBotResponses(string? persona = null)
    {
        var query = context.BotResponses.AsQueryable();
        if (persona != null)
            query = query.Where(r => r.Persona == null || r.Persona == persona);
        return query
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

    public void RemoveLearnedWord(string word)
    {
        var lower = word.ToLowerInvariant();
        var entry = context.PosDictionary.Local.FirstOrDefault(e => e.Word == lower)
                    ?? context.PosDictionary.FirstOrDefault(e => e.Word == lower);
        if (entry != null)
            context.PosDictionary.Remove(entry);
    }

    public void UpdateWordType(string word, string wordType)
    {
        var lower = word.ToLowerInvariant();
        var entry = context.PosDictionary.Local.FirstOrDefault(e => e.Word == lower)
                    ?? context.PosDictionary.FirstOrDefault(e => e.Word == lower);
        if (entry != null)
        {
            entry.WordType = wordType;
        }
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

    public List<Fact> GetFactsInDateRange(int userId, DateTime? from, DateTime? to)
    {
        var query = context.Facts.Where(f => f.UserId == userId && f.MentionedAt != null);

        if (from.HasValue)
        {
            var fromStr = from.Value.ToString("o");
            query = query.Where(f => string.Compare(f.MentionedAt, fromStr) >= 0);
        }

        if (to.HasValue)
        {
            var toStr = to.Value.ToString("o");
            query = query.Where(f => string.Compare(f.MentionedAt, toStr) <= 0);
        }

        return query.OrderBy(f => f.MentionedAt).SelectFacet<Fact>().ToList();
    }

    public string BuildTimeline(List<Fact> facts)
    {
        if (facts.Count == 0) return string.Empty;

        var dayNames = new[] { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" };
        var lines = new List<string>();

        foreach (var fact in facts)
        {
            if (string.IsNullOrEmpty(fact.MentionedAt)) continue;

            if (DateTime.TryParse(fact.MentionedAt, out var dt))
            {
                var day = dayNames[(int)dt.DayOfWeek];
                var conjVerb = PokeChat.Responses.ResponseEngine.ConjugateVerb(fact.Verb, fact.Subject);
                var line = fact.PredicateType switch
                {
                    nameof(PredicateType.Preference) => $"{day}: {fact.Subject} liked {fact.Object}.",
                    nameof(PredicateType.Dislike) => $"{day}: {fact.Subject} disliked {fact.Object}.",
                    nameof(PredicateType.Possession) => $"{day}: {fact.Subject} had {fact.Object}.",
                    _ => $"{day}: {fact.Subject} {conjVerb} {fact.Object}."
                };
                lines.Add(line);
            }
        }

        return string.Join("\n", lines);
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

    public Dictionary<string, List<(string Relation, string Target)>> GetEntityGraph(int userId)
    {
        var graph = new Dictionary<string, List<(string, string)>>(StringComparer.OrdinalIgnoreCase);

        var facts = context.Facts
            .Where(f => f.UserId == userId)
            .SelectFacet<Fact>()
            .ToList();

        foreach (var fact in facts)
        {
            if (!graph.ContainsKey(fact.Subject))
                graph[fact.Subject] = new List<(string, string)>();
            graph[fact.Subject].Add((fact.Verb, fact.Object));

            if (!graph.ContainsKey(fact.Object))
                graph[fact.Object] = new List<(string, string)>();
        }

        return graph;
    }

    public string? FindPath(int userId, string fromEntity, string toEntity, int maxDepth = 3)
    {
        var lowerFrom = fromEntity.ToLowerInvariant();
        var lowerTo = toEntity.ToLowerInvariant();
        var graph = GetEntityGraph(userId);

        if (!graph.ContainsKey(lowerFrom) || !graph.ContainsKey(lowerTo))
            return null;

        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<(string Node, List<(string Node, string Relation)> Path)>();
        queue.Enqueue((lowerFrom, new List<(string, string)>()));
        visited.Add(lowerFrom);

        while (queue.Count > 0)
        {
            var (current, path) = queue.Dequeue();
            if (path.Count >= maxDepth * 2) continue;

            if (!graph.TryGetValue(current, out var edges)) continue;

            foreach (var (relation, target) in edges)
            {
                if (string.Equals(target, lowerTo, StringComparison.OrdinalIgnoreCase))
                {
                    var fullPath = new List<(string, string)>(path) { (relation, target) };
                    return FormatPath(fullPath, fromEntity, toEntity);
                }

                if (visited.Add(target))
                {
                    var newPath = new List<(string, string)>(path) { (relation, target) };
                    queue.Enqueue((target, newPath));
                }
            }
        }

        return null;
    }

    private static string FormatPath(List<(string Relation, string Target)> path, string fromEntity, string toEntity)
    {
        var parts = new List<string> { fromEntity };
        foreach (var (rel, target) in path)
        {
            var conjVerb = PokeChat.Responses.ResponseEngine.ConjugateVerb(rel, fromEntity);
            parts.Add(conjVerb);
            parts.Add(target);
        }
        return string.Join(" ", parts);
    }

    public bool CheckRelation(int userId, string subject, string verb, string obj)
    {
        var lowerSubject = subject.ToLowerInvariant();
        var lowerVerb = verb.ToLowerInvariant();
        var lowerObj = obj.ToLowerInvariant();
        return context.Facts
            .Where(f => f.UserId == userId)
            .SelectFacet<Fact>()
            .ToList()
            .Any(f => f.Subject.Equals(lowerSubject, StringComparison.OrdinalIgnoreCase) &&
                      f.Verb.Equals(lowerVerb, StringComparison.OrdinalIgnoreCase) &&
                      f.Object.Equals(lowerObj, StringComparison.OrdinalIgnoreCase));
    }

    public List<string> GetConnectedEntities(int userId, string entity)
    {
        var facts = context.Facts
            .Where(f => f.UserId == userId && (f.Subject == entity || f.Object == entity))
            .SelectFacet<Fact>()
            .ToList();

        var connected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var fact in facts)
        {
            if (fact.Subject != entity)
                connected.Add(fact.Subject);
            if (fact.Object != entity)
                connected.Add(fact.Object);
        }

        return connected.ToList();
    }

    public ConversationSession? GetSessionByGuid(string sessionGuid)
    {
        return context.ConversationSessions.FirstOrDefault(s => s.SessionGuid == sessionGuid);
    }

    public void CreateConversationSession(string sessionGuid, int userId, string? botName = null, string? persona = null)
    {
        var now = DateTime.UtcNow.ToString("o");
        var session = new ConversationSession
        {
            SessionGuid = sessionGuid,
            UserId = userId,
            StartedAt = now,
            LastActiveAt = now,
            TurnCount = 0,
            BotName = botName,
            Persona = persona
        };
        context.ConversationSessions.Add(session);
    }

    public void UpdateSessionActivity(string sessionGuid)
    {
        var session = context.ConversationSessions.FirstOrDefault(s => s.SessionGuid == sessionGuid);
        if (session != null)
        {
            session.LastActiveAt = DateTime.UtcNow.ToString("o");
            session.TurnCount++;
        }
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

        if (DateTime.TryParse(conversations.First().Timestamp, out var sessionStart) &&
            DateTime.TryParse(conversations.Last().Timestamp, out var sessionEnd))
        {
            facts = facts
                .Where(f => DateTime.TryParse(f.CreatedAt, out var created) &&
                            created >= sessionStart.AddMinutes(-1) &&
                            created <= sessionEnd.AddMinutes(1))
                .ToList();
        }

        var signatures = facts
            .Where(f => !SummaryFilters.IsGarbageFact(f))
            .Select(FormatFact)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToList();

        if (signatures.Count == 0)
            return string.Empty;

        if (signatures.Count <= 2)
            return string.Join("; ", signatures);

        var numbered = signatures.Select((f, i) => $"{i + 1}) {f}");
        return string.Join(". ", numbered);
    }

    private static string FormatFact(Fact fact)
    {
        var stemmed = ChatEngine.StemVerb(fact.Verb);
        var conjVerb = ResponseEngine.ConjugateVerb(stemmed, fact.Subject);
        return $"{fact.Subject} {conjVerb} {fact.Object}";
    }

    public int GetFactCountAboutSubject(int userId, string subject)
    {
        return context.Facts.Count(f => f.UserId == userId && f.Subject == subject);
    }

    public void LearnResponseRule(string pattern, string responseTemplate, string inputType, int? userId = null)
    {
        var existing = context.LearnedResponseRules.Local
            .FirstOrDefault(r => r.Pattern == pattern && r.ResponseTemplate == responseTemplate)
            ?? context.LearnedResponseRules
            .FirstOrDefault(r => r.Pattern == pattern && r.ResponseTemplate == responseTemplate);
        if (existing != null)
            return;

        var rule = new LearnedResponseRule
        {
            Pattern = pattern,
            ResponseTemplate = responseTemplate,
            InputType = inputType,
            LearnedFromUserId = userId,
            Confidence = 5,
            IsActive = true,
            CreatedAt = DateTime.UtcNow.ToString("o")
        };
        context.LearnedResponseRules.Add(rule);
    }

    public bool IsLearnedRuleKnown(string pattern)
    {
        return context.LearnedResponseRules.Any(r => r.Pattern == pattern && r.IsActive);
    }

    public List<LearnedResponseRule> GetLearnedRules()
    {
        return context.LearnedResponseRules
            .Where(r => r.IsActive)
            .OrderByDescending(r => r.Confidence)
            .ToList();
    }

    public void AdjustConfidence(int ruleId, int delta, bool isLearned = true)
    {
        if (isLearned)
        {
            var rule = context.LearnedResponseRules.Find(ruleId);
            if (rule == null) return;
            rule.Confidence = System.Math.Clamp(rule.Confidence + delta, 1, 10);
            if (rule.Confidence <= 1)
                rule.IsActive = false;
        }
    }

    public void DeactivateLearnedRule(int ruleId)
    {
        var rule = context.LearnedResponseRules.Find(ruleId);
        if (rule != null)
            rule.IsActive = false;
    }

    public void RecordFeedback(int ruleId, int userId, string feedback, bool isLearned, string? correctionText = null)
    {
        var entry = new ResponseFeedback
        {
            RuleId = ruleId,
            IsLearnedRule = isLearned,
            UserId = userId,
            Feedback = feedback,
            CorrectionText = correctionText,
            CreatedAt = DateTime.UtcNow.ToString("o")
        };
        context.ResponseFeedbacks.Add(entry);
    }

    public void RecordTurnRating(int conversationId, int userId, int rating)
    {
        var entry = new TurnRating
        {
            ConversationId = conversationId,
            UserId = userId,
            Rating = rating,
            CreatedAt = DateTime.UtcNow.ToString("o")
        };
        context.TurnRates.Add(entry);
    }

    public bool HasUserRatedConversation(int conversationId, int userId)
    {
        return context.TurnRates.Any(t => t.ConversationId == conversationId && t.UserId == userId);
    }

    public void RecordSessionMetrics(string sessionId)
    {
        var conversations = context.Conversations
            .Where(c => c.SessionId == sessionId)
            .OrderBy(c => c.Id)
            .ToList();
        if (conversations.Count == 0) return;

        var userId = conversations[0].UserId;
        var turnCount = conversations.Count;

        var userIdVal = userId ?? 0;
        var factsLearned = context.Facts.Count(f => f.UserId == userIdVal);

        var sentiments = context.Facts
            .Where(f => f.UserId == userIdVal && f.Sentiment != null)
            .Select(f => f.Sentiment)
            .ToList();
        var dominantSentiment = sentiments
            .GroupBy(s => s)
            .OrderByDescending(g => g.Count())
            .FirstOrDefault()?.Key;

        var sentimentTrend = "stable";
        if (sentiments.Count >= 3)
        {
            var half = sentiments.Count / 2;
            var firstHalf = sentiments.Take(half).ToList();
            var secondHalf = sentiments.Skip(half).ToList();
            var firstPos = firstHalf.Count(s => s == "positive");
            var secondPos = secondHalf.Count(s => s == "positive");
            var firstNeg = firstHalf.Count(s => s is "negative" or "anger" or "fear");
            var secondNeg = secondHalf.Count(s => s is "negative" or "anger" or "fear");
            if (secondPos > firstPos) sentimentTrend = "improving";
            else if (secondNeg > firstNeg) sentimentTrend = "declining";
        }

        var topicsDiscussed = context.Facts
            .Where(f => f.UserId == userIdVal)
            .Select(f => f.Subject)
            .Distinct()
            .Count();

        var responseGroups = conversations
            .Where(c => !string.IsNullOrEmpty(c.ResponseCategory))
            .GroupBy(c => c.ResponseCategory!)
            .ToDictionary(g => g.Key, g => g.Count());
        var responseStats = System.Text.Json.JsonSerializer.Serialize(responseGroups);

        var avgResponseLength = conversations.Count > 0
            ? (int)conversations.Average(c => c.BotResponse.Length)
            : 0;

        var startedAtStr = conversations[0].Timestamp;
        var endedAtStr = conversations[^1].Timestamp;
        var sessionLength = 0;
        if (DateTime.TryParse(startedAtStr, out var started) && DateTime.TryParse(endedAtStr, out var ended))
            sessionLength = (int)(ended - started).TotalSeconds;

        var metric = new ConversationMetric
        {
            SessionId = sessionId,
            UserId = userId,
            TurnCount = turnCount,
            FactsLearned = factsLearned,
            DominantSentiment = dominantSentiment,
            SentimentTrend = sentimentTrend,
            TopicsDiscussed = topicsDiscussed,
            BotResponseStats = responseStats,
            AvgResponseLength = avgResponseLength,
            SessionLength = sessionLength,
            StartedAt = startedAtStr,
            EndedAt = endedAtStr
        };
        context.ConversationMetrics.Add(metric);
    }

    public void UpdateResponseEffectiveness(string category, bool hadFollowUp)
    {
        var existing = context.ResponseEffectiveness.Local
            .FirstOrDefault(e => e.Category == category)
            ?? context.ResponseEffectiveness
            .FirstOrDefault(e => e.Category == category);
        if (existing == null)
        {
            existing = new ResponseEffectiveness
            {
                Category = category,
                AvgSessionLengthAfter = 1,
                UsedCount = 1,
                FollowUpRate = hadFollowUp ? 1.0 : 0.0,
                LastUsed = DateTime.UtcNow.ToString("o")
            };
            context.ResponseEffectiveness.Add(existing);
        }
        else
        {
            var total = existing.UsedCount + 1;
            var newFollowUpCount = (int)(existing.FollowUpRate * existing.UsedCount) + (hadFollowUp ? 1 : 0);
            existing.FollowUpRate = (double)newFollowUpCount / total;
            existing.UsedCount = total;
            existing.AvgSessionLengthAfter = (existing.AvgSessionLengthAfter + 1) / 2;
            existing.LastUsed = DateTime.UtcNow.ToString("o");
        }
    }

    public double? GetEffectiveness(string category)
    {
        var existing = context.ResponseEffectiveness
            .FirstOrDefault(e => e.Category == category);
        return existing?.FollowUpRate;
    }

    public List<ConversationMetric> GetMetricsForUser(int userId)
    {
        return context.ConversationMetrics
            .Where(m => m.UserId == userId)
            .OrderByDescending(m => m.Id)
            .ToList();
    }

    public List<string> GetBestPerformingCategories(int topN)
    {
        return context.ResponseEffectiveness
            .Where(e => e.UsedCount >= 2)
            .OrderByDescending(e => e.FollowUpRate)
            .ThenByDescending(e => e.UsedCount)
            .Take(topN)
            .Select(e => e.Category)
            .ToList();
    }

    public List<Conversation> GetConversationsBySession(string sessionId)
    {
        return context.Conversations
            .Where(c => c.SessionId == sessionId)
            .OrderBy(c => c.Id)
            .ToList();
    }

    public string? GetRandomWord(string wordType)
    {
        var words = context.PosDictionary
            .Where(p => p.WordType == wordType)
            .Select(p => p.Word)
            .Distinct()
            .ToList();

        if (words.Count == 0) return null;
        return words[Random.Shared.Next(words.Count)];
    }

    public string? GetRandomNounByCategory(string category)
    {
        var nouns = context.NounCategories
            .Where(n => n.Category == category)
            .Select(n => n.Noun)
            .Distinct()
            .ToList();

        if (nouns.Count == 0) return null;
        return nouns[Random.Shared.Next(nouns.Count)];
    }

    public List<StoryTemplate> GetStoryTemplates()
    {
        return context.StoryTemplates.ToList();
    }

    public Fact? GetRandomUserFact(int userId)
    {
        var entities = context.Facts
            .Where(f => f.UserId == userId)
            .ToList();

        if (entities.Count == 0) return null;

        var picked = entities[Random.Shared.Next(entities.Count)];
        TouchFactAccess(picked);
        return new[] { picked }.AsQueryable().SelectFacet<Fact>().First();
    }

    public string? GetRandomName()
    {
        var names = context.Users
            .Select(u => u.Name)
            .Distinct()
            .ToList();

        if (names.Count == 0) return null;
        return names[Random.Shared.Next(names.Count)];
    }

    public List<ConversationSession> GetPreviousSessions(int userId, string currentSessionId)
    {
        return context.ConversationSessions
            .Where(s => s.UserId == userId && s.SessionGuid != currentSessionId)
            .OrderByDescending(s => s.Id)
            .ToList();
    }

    public Fact? GetRandomFactFromSession(int userId, string sessionId)
    {
        var conversations = context.Conversations
            .Where(c => c.SessionId == sessionId && c.UserId == userId)
            .OrderBy(c => c.Timestamp)
            .ToList();

        if (conversations.Count == 0) return null;

        var allUserFactEntities = context.Facts
            .Where(f => f.UserId == userId)
            .ToList();

        var sessionFacts = new List<FactEntity>();
        foreach (var conv in conversations)
        {
            var lowerInput = conv.UserInput.ToLowerInvariant();
            foreach (var entity in allUserFactEntities)
            {
                if (!SummaryFilters.IsGarbageFact(new[] { entity }.AsQueryable().SelectFacet<Fact>().First()) &&
                    lowerInput.Contains(entity.Object.ToLowerInvariant()) &&
                    (lowerInput.Contains(entity.Subject.ToLowerInvariant()) ||
                     lowerInput.Contains(entity.Verb.ToLowerInvariant())))
                {
                    sessionFacts.Add(entity);
                }
            }
        }

        if (sessionFacts.Count == 0) return null;
        var picked = sessionFacts[Random.Shared.Next(sessionFacts.Count)];
        TouchFactAccess(picked);
        return new[] { picked }.AsQueryable().SelectFacet<Fact>().First();
    }

    public MadLibTemplate? GetRandomMadLibTemplate()
    {
        var templates = context.MadLibTemplates.ToList();
        if (templates.Count == 0) return null;
        return templates[Random.Shared.Next(templates.Count)];
    }

    public (Fact?, Fact?) GetTwoRandomUserFacts(int userId)
    {
        var entities = context.Facts
            .Where(f => f.UserId == userId)
            .ToList();

        if (entities.Count < 2) return (null, null);

        var distinct = entities
            .GroupBy(f => f.Object.ToLowerInvariant())
            .Where(g => g.Count() >= 1)
            .Select(g => g.First())
            .ToList();

        if (distinct.Count < 2) return (null, null);

        var first = distinct[Random.Shared.Next(distinct.Count)];
        distinct.Remove(first);
        var second = distinct[Random.Shared.Next(distinct.Count)];

        TouchFactAccess(first);
        TouchFactAccess(second);
        return (
            new[] { first }.AsQueryable().SelectFacet<Fact>().First(),
            new[] { second }.AsQueryable().SelectFacet<Fact>().First()
        );
    }

    public List<Fact> GetRandomFactsForQuiz(int userId, int count)
    {
        var entities = context.Facts
            .Where(f => f.UserId == userId)
            .ToList();

        if (entities.Count == 0) return new List<Fact>();

        var selected = entities.OrderBy(_ => Random.Shared.Next()).Take(count).ToList();
        foreach (var e in selected)
            TouchFactAccess(e);
        return selected.AsQueryable().SelectFacet<Fact>().ToList();
    }

    public Joke? GetRandomJoke()
    {
        var jokes = context.Jokes.ToList();
        if (jokes.Count == 0) return null;
        return jokes[Random.Shared.Next(jokes.Count)];
    }

    public Riddle? GetRandomRiddle(HashSet<string>? excludeQuestions = null)
    {
        var riddles = context.Riddles.ToList();
        if (riddles.Count == 0) return null;

        if (excludeQuestions != null && excludeQuestions.Count > 0)
        {
            var available = riddles.Where(r => !excludeQuestions.Contains(r.Question)).ToList();
            if (available.Count > 0)
                return available[Random.Shared.Next(available.Count)];
        }

        return riddles[Random.Shared.Next(riddles.Count)];
    }

    public List<string> GetAllRhymeGroupWords(string wordType)
    {
        return context.RhymeGroups
            .Where(r => r.WordType == wordType)
            .Select(r => r.Word)
            .Distinct()
            .ToList();
    }

    public List<string> GetRhymeGroupWords(string rhymeKey, string wordType)
    {
        return context.RhymeGroups
            .Where(r => r.RhymeKey == rhymeKey && r.WordType == wordType)
            .Select(r => r.Word)
            .ToList();
    }

    public List<string> GetWordsByTypeAndSyllables(string wordType, int syllableCount)
    {
        var words = context.PosDictionary
            .Where(p => p.WordType == wordType)
            .Select(p => p.Word)
            .ToList();

        return words
            .Where(w => SyllableCounter.Count(w) == syllableCount)
            .ToList();
    }

    public List<PoemTemplate> GetPoemTemplates(string poemType)
    {
        return context.PoemTemplates
            .Where(t => t.PoemType == poemType)
            .ToList();
    }

    public ErrorKnowledgeEntry? MatchError(string input)
    {
        var entries = context.ErrorKnowledgeEntries.ToList();
        foreach (var entry in entries)
        {
            try
            {
                if (Regex.IsMatch(input, entry.Pattern, RegexOptions.IgnoreCase))
                    return entry;
            }
            catch
            {
            }
        }
        return null;
    }

    public void LearnError(string pattern, string suggestion, string language = "general")
    {
        context.ErrorKnowledgeEntries.Add(new ErrorKnowledgeEntry
        {
            Pattern = pattern,
            Suggestion = suggestion,
            Language = language,
            IsLearned = true,
            UsedCount = 0,
            SuccessCount = 0,
            CreatedAt = DateTime.UtcNow.ToString("o")
        });
    }

    public void IncrementErrorUsage(int entryId)
    {
        var entry = context.ErrorKnowledgeEntries.Find(entryId);
        if (entry != null)
            entry.UsedCount++;
    }

    public void IncrementErrorSuccess(int entryId)
    {
        var entry = context.ErrorKnowledgeEntries.Find(entryId);
        if (entry != null)
            entry.SuccessCount++;
    }

    public Reminder? CreateReminder(int userId, string task, DateTime dueAt)
    {
        var reminder = new Reminder
        {
            UserId = userId,
            Task = task,
            DueAt = dueAt.ToString("o"),
            Status = "pending",
            CreatedAt = DateTime.UtcNow.ToString("o")
        };
        context.Reminders.Add(reminder);
        return reminder;
    }

    public List<Reminder> GetPendingReminders(int userId)
    {
        return context.Reminders
            .Where(r => r.UserId == userId && r.Status == "pending")
            .OrderBy(r => r.DueAt)
            .ToList();
    }

    public List<Reminder> GetDueReminders(int userId)
    {
        var now = DateTime.UtcNow.ToString("o");
        return context.Reminders
            .Where(r => r.UserId == userId && r.Status == "pending" && r.DueAt.CompareTo(now) <= 0)
            .OrderBy(r => r.DueAt)
            .ToList();
    }

    public Reminder? MarkReminderDone(int userId, string task)
    {
        var reminder = context.Reminders
            .FirstOrDefault(r => r.UserId == userId && r.Task == task && r.Status == "pending");
        if (reminder != null)
            reminder.Status = "done";
        return reminder;
    }

    public Reminder? CancelReminder(int userId, string task)
    {
        var reminder = context.Reminders
            .FirstOrDefault(r => r.UserId == userId && r.Task == task && r.Status == "pending");
        if (reminder != null)
            reminder.Status = "cancelled";
        return reminder;
    }

    public bool HasReminderForTask(int userId, string task)
    {
        return context.Reminders.Any(r => r.UserId == userId && r.Task == task && r.Status == "pending");
    }

    public DateTime? ParseReminderTime(string input, DateTime? defaultTime = null)
    {
        var lowerInput = input.Trim().ToLowerInvariant();
        var now = defaultTime ?? DateTime.UtcNow;
        int hour = 0, minute = 0;
        var hasTime = false;
        var daysOffset = 0;
        var hasDate = false;

        // Check for "at {time}" — "at 5pm", "at 3:30", "at 14:00"
        var atTimeMatch = Regex.Match(lowerInput, @"at\s+(\d{1,2})(?::(\d{2}))?\s*(am|pm)?");
        if (atTimeMatch.Success)
        {
            hour = int.Parse(atTimeMatch.Groups[1].Value);
            if (atTimeMatch.Groups[3].Value is "pm" && hour != 12)
                hour += 12;
            if (atTimeMatch.Groups[3].Value is "am" && hour == 12)
                hour = 0;
            if (atTimeMatch.Groups[2].Success)
                minute = int.Parse(atTimeMatch.Groups[2].Value);
            hasTime = true;
        }

        // Check for "today", "tomorrow"
        if (lowerInput.Contains("tomorrow"))
        {
            daysOffset = 1;
            hasDate = true;
        }
        else if (lowerInput.Contains("today"))
        {
            hasDate = true;
        }

        // Check for temporal expressions (monday, next week, etc.)
        var temporalExpressions = context.TemporalExpressions.ToList();
        foreach (var expr in temporalExpressions)
        {
            if (lowerInput.Contains(expr.Expression))
            {
                daysOffset = expr.DaysOffset;
                hasDate = true;
                break;
            }
        }

        // Check for "in X days" / "in X hours"
        var inDaysMatch = Regex.Match(lowerInput, @"in (\d+)\s+days?");
        if (inDaysMatch.Success)
        {
            daysOffset = int.Parse(inDaysMatch.Groups[1].Value);
            hasDate = true;
        }

        var inHoursMatch = Regex.Match(lowerInput, @"in (\d+)\s+hours?");
        if (inHoursMatch.Success && !hasDate)
        {
            var baseTime = defaultTime ?? DateTime.UtcNow;
            var result = baseTime.AddHours(int.Parse(inHoursMatch.Groups[1].Value));
            if (hasTime)
                result = new DateTime(result.Year, result.Month, result.Day, hour, minute, 0);
            return result;
        }

        var fallbackTime = defaultTime ?? DateTime.UtcNow;
        if (hasDate || hasTime)
        {
            var date = hasDate ? fallbackTime.AddDays(daysOffset) : fallbackTime;
            return hasTime
                ? new DateTime(date.Year, date.Month, date.Day, hour, minute, 0)
                : new DateTime(date.Year, date.Month, date.Day, 23, 59, 0);
        }

        // No time and no date — default to 1 hour from now
        return fallbackTime.AddHours(1);
    }

    public List<(string Response, ML.ResponseContext Context, float Label)> GetRerankerTrainingData()
    {
        var conversations = context.Conversations
            .OrderByDescending(c => c.Id)
            .Take(500)
            .ToList();

        var effectiveness = context.ResponseEffectiveness
            .ToDictionary(e => e.Category, e => e);

        var results = new List<(string Response, ML.ResponseContext Context, float Label)>();
        foreach (var conv in conversations)
        {
            if (string.IsNullOrEmpty(conv.BotResponse) || string.IsNullOrEmpty(conv.ResponseCategory))
                continue;

            double followUpRate = 0.5;
            if (effectiveness.TryGetValue(conv.ResponseCategory, out var eff))
                followUpRate = eff.FollowUpRate;

            var ctx = new ML.ResponseContext
            {
                Category = conv.ResponseCategory,
                SentimentScore = 0f,
                PreviousResponse = null,
                TurnNumber = 0,
                UserInput = conv.UserInput,
                CategoryFollowUpRate = followUpRate
            };

            results.Add((conv.BotResponse, ctx, (float)followUpRate));
        }
        return results;
    }

    public List<string> GetBotResponseTexts()
    {
        return context.BotResponses
            .Select(b => b.ResponseText)
            .Where(r => !string.IsNullOrEmpty(r) && !r.Contains('{'))
            .Distinct()
            .ToList();
    }

    public record DecayReport(int DeletedFacts, int DeletedRules, int DeletedDefinitions, bool DryRun, long? ReclaimedBytes);

    public void TouchFactAccess(FactEntity fact)
    {
        fact.LastAccessed = DateTime.UtcNow.ToString("o");
        fact.AccessCount++;
    }

    public DecayReport DecayCleanup(bool dryRun = false, int vacuumThreshold = 50)
    {
        var now = DateTime.UtcNow;
        var staleCutoff = now.AddDays(-90);
        var minAge = now.AddHours(-24);

        var deletedFacts = 0;
        var deletedRules = 0;
        var deletedDefs = 0;

        var staleFacts = context.Facts
            .ToList()
            .Where(f => string.Compare(f.CreatedAt, minAge.ToString("o"), StringComparison.Ordinal) < 0
                        && f.AccessCount == 0
                        && f.Confidence < 2.0
                        && (f.LastAccessed == null || string.Compare(f.LastAccessed, staleCutoff.ToString("o"), StringComparison.Ordinal) < 0))
            .ToList();

        deletedFacts = staleFacts.Count;
        if (!dryRun && deletedFacts > 0)
            context.Facts.RemoveRange(staleFacts);

        var staleRules = context.LearnedResponseRules
            .ToList()
            .Where(r => string.Compare(r.CreatedAt, minAge.ToString("o"), StringComparison.Ordinal) < 0
                        && r.AccessCount == 0
                        && r.Confidence < 3
                        && (r.LastAccessed == null || string.Compare(r.LastAccessed, staleCutoff.ToString("o"), StringComparison.Ordinal) < 0))
            .ToList();

        deletedRules = staleRules.Count;
        if (!dryRun && deletedRules > 0)
            context.LearnedResponseRules.RemoveRange(staleRules);

        var staleDefs = context.WordDefinitions
            .ToList()
            .Where(d => string.Compare(d.CreatedAt, minAge.ToString("o"), StringComparison.Ordinal) < 0
                        && d.AccessCount == 0
                        && (d.LastAccessed == null || string.Compare(d.LastAccessed, staleCutoff.ToString("o"), StringComparison.Ordinal) < 0))
            .ToList();

        deletedDefs = staleDefs.Count;
        if (!dryRun && deletedDefs > 0)
            context.WordDefinitions.RemoveRange(staleDefs);

        var totalDeleted = deletedFacts + deletedRules + deletedDefs;

        if (!dryRun && totalDeleted > 0)
            context.SaveChanges();

        long? reclaimedBytes = null;
        if (!dryRun && totalDeleted >= vacuumThreshold)
        {
            var dbPath = context.Database.GetDbConnection().DataSource;
            if (!string.IsNullOrEmpty(dbPath) && File.Exists(dbPath))
            {
                var sizeBefore = new FileInfo(dbPath).Length;
                context.Database.ExecuteSqlRaw("VACUUM");
                var sizeAfter = new FileInfo(dbPath).Length;
                reclaimedBytes = sizeBefore - sizeAfter;
            }
        }

        return new DecayReport(deletedFacts, deletedRules, deletedDefs, dryRun, reclaimedBytes);
    }

    public List<AllowedCommand> GetActiveAllowedCommands(string now)
    {
        return context.AllowedCommands
            .Where(c => c.IsPermanent || (c.ExpiresAt != null && c.ExpiresAt.CompareTo(now) > 0))
            .ToList();
    }

    public void SaveAllowedCommand(string command, bool isPermanent, string now)
    {
        var existing = context.AllowedCommands.FirstOrDefault(c => c.Command == command);
        if (existing != null)
        {
            existing.IsPermanent = isPermanent;
            if (!isPermanent)
                existing.ExpiresAt = DateTime.UtcNow.AddMinutes(5).ToString("o");
            return;
        }

        context.AllowedCommands.Add(new AllowedCommand
        {
            Command = command,
            IsPermanent = isPermanent,
            ExpiresAt = isPermanent ? null : DateTime.UtcNow.AddMinutes(5).ToString("o"),
            CreatedAt = now
        });
    }
}

using PokeChat.Data.Entities;
using PokeChat.Knowledge;
using PokeChat.Tests.Helpers;
using Shouldly;

namespace PokeChat.Tests.Knowledge;

public class KnowledgeStoreTests
{
    [Fact]
    public void StoreFact_And_Retrieve()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);
        var fact = new Fact
        {
            Subject = "Alice",
            Verb = "likes",
            Object = "pizza",
            PredicateType = "preference",
            CreatedAt = DateTime.UtcNow.ToString("o")
        };
        store.StoreFact(fact);
        store.Save();
        var retrieved = store.GetFact("Alice", "likes", "pizza");
        retrieved.ShouldNotBeNull();
        retrieved.Subject.ShouldBe("Alice");
        retrieved.Verb.ShouldBe("likes");
        retrieved.Object.ShouldBe("pizza");
    }

    [Fact]
    public void GetFact_Nonexistent_ReturnsNull()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);
        store.GetFact("nobody", "does", "nothing").ShouldBeNull();
    }

    [Fact]
    public void GetFactsBySubject()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);
        store.StoreFact(new Fact { Subject = "Bob", Verb = "has", Object = "car", PredicateType = "possession", CreatedAt = DateTime.UtcNow.ToString("o") });
        store.StoreFact(new Fact { Subject = "Bob", Verb = "likes", Object = "dogs", PredicateType = "preference", CreatedAt = DateTime.UtcNow.ToString("o") });
        store.Save();
        var facts = store.GetFactsBySubject("Bob");
        facts.Count.ShouldBe(2);
    }

    [Fact]
    public void GetFactsByUser()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);
        var userId = store.GetOrCreateUser("Charlie");
        store.StoreFact(new Fact { UserId = userId, Subject = "Charlie", Verb = "likes", Object = "cats", PredicateType = "preference", CreatedAt = DateTime.UtcNow.ToString("o") });
        store.Save();
        var facts = store.GetFactsByUser(userId!.Value);
        facts.Count.ShouldBe(1);
    }

    [Fact]
    public void GetOrCreateUser_CreatesNew()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);
        var userId = store.GetOrCreateUser("Dave");
        userId.ShouldNotBeNull();
    }

    [Fact]
    public void GetOrCreateUser_ReturnsExisting()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);
        var first = store.GetOrCreateUser("Eve");
        var second = store.GetOrCreateUser("Eve");
        first.ShouldBe(second);
    }

    [Fact]
    public void StoreConversation()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);
        var userId = store.GetOrCreateUser("Frank");
        store.StoreConversation(userId!.Value, "hello", "hi there");
        store.Save();
        var conversations = db.Context.Conversations.ToList();
        conversations.Count.ShouldBe(1);
        conversations[0].UserInput.ShouldBe("hello");
    }

    [Fact]
    public void AddGreeting()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);
        store.AddGreeting("Hello there!");
        store.Save();
        var greetings = store.GetGreetings();
        greetings.Count.ShouldBe(1);
        greetings[0].Text.ShouldBe("Hello there!");
    }

    [Fact]
    public void GetGreetings_Empty_WhenNoneAdded()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);
        store.GetGreetings().ShouldBeEmpty();
    }

    [Fact]
    public void AddGreetingWord()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);
        var userId = store.GetOrCreateUser("Grace");
        store.AddGreetingWord("howdy", userId);
        store.Save();
        store.IsGreetingWord("howdy").ShouldBeTrue();
    }

    [Fact]
    public void GetGreetingWords_Empty_WhenNoneAdded()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);
        store.GetGreetingWords().ShouldBeEmpty();
    }

    [Fact]
    public void GetResponseRules_Empty_WhenNoneSeeded()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);
        store.GetResponseRules().ShouldBeEmpty();
    }

    [Fact]
    public void GetPosDictionary_Empty_WhenNoneSeeded()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);
        store.GetPosDictionary().ShouldBeEmpty();
    }

    [Fact]
    public void GetAllFacts_Empty_WhenNoneStored()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);
        store.GetAllFacts().ShouldBeEmpty();
    }

    [Fact]
    public void AnalyseSentiment_ReturnsPositive_ForHappyInput()
    {
        using var db = new FreshDbContext();
        TestDataHelper.SeedEmotionKeywords(db.Context);
        var store = new KnowledgeStore(db.Context);
        var (sentiment, intensity) = store.AnalyseSentiment("I'm so happy and wonderful today!");
        sentiment.ShouldBe("positive");
        intensity.ShouldBeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public void AnalyseSentiment_ReturnsNegative_ForSadInput()
    {
        using var db = new FreshDbContext();
        TestDataHelper.SeedEmotionKeywords(db.Context);
        var store = new KnowledgeStore(db.Context);
        var (sentiment, intensity) = store.AnalyseSentiment("I feel so sad and unhappy");
        sentiment.ShouldBe("negative");
        intensity.ShouldBeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public void AnalyseSentiment_ReturnsNeutral_WhenNoKeywordsMatch()
    {
        using var db = new FreshDbContext();
        TestDataHelper.SeedEmotionKeywords(db.Context);
        var store = new KnowledgeStore(db.Context);
        var (sentiment, intensity) = store.AnalyseSentiment("the sky is blue");
        sentiment.ShouldBe("neutral");
        intensity.ShouldBe(0);
    }

    [Fact]
    public void AnalyseSentiment_ReturnsDominant_ForMixedInput()
    {
        using var db = new FreshDbContext();
        TestDataHelper.SeedEmotionKeywords(db.Context);
        var store = new KnowledgeStore(db.Context);
        var (sentiment, intensity) = store.AnalyseSentiment("I love this! But I'm also sad about the news");
        sentiment.ShouldBe("positive");
        intensity.ShouldBeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public void AnalyseSentiment_ReturnsNeutral_WhenNoKeywordsSeeded()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);
        var (sentiment, intensity) = store.AnalyseSentiment("I'm so happy!");
        sentiment.ShouldBeNull();
        intensity.ShouldBe(0);
    }

    [Fact]
    public void UpdateFactSentiment_UpdatesExistingFact()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);
        var fact = new FactEntity
        {
            Subject = "test", Verb = "is", Object = "test",
            PredicateType = "GeneralFact", CreatedAt = DateTime.UtcNow.ToString("o")
        };
        db.Context.Facts.Add(fact);
        db.Context.SaveChanges();

        store.UpdateFactSentiment(fact.Id, "positive", 3);
        store.Save();

        var updated = db.Context.Facts.Find(fact.Id);
        updated.ShouldNotBeNull();
        updated.Sentiment.ShouldBe("positive");
        updated.EmotionIntensity.ShouldBe(3);
    }

    [Fact]
    public void ExtractTimeContext_DetectsKnownExpression()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);
        TestDataHelper.SeedTemporalExpressions(db.Context);

        var result = store.ExtractTimeContext("I went to the cinema yesterday");
        result.ShouldBe("yesterday");
    }

    [Fact]
    public void ExtractTimeContext_ReturnsNull_WhenNoMatch()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);
        TestDataHelper.SeedTemporalExpressions(db.Context);

        var result = store.ExtractTimeContext("hello world");
        result.ShouldBeNull();
    }

    [Fact]
    public void ExtractTimeContext_ReturnsMostSpecific_WhenMultipleMatches()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);
        TestDataHelper.SeedTemporalExpressions(db.Context);

        var result = store.ExtractTimeContext("I went there yesterday and also last year");
        result.ShouldBe("last year");
    }

    [Fact]
    public void GetFactsWithTimeContext_ReturnsMatchingFacts()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);

        var user = new User { Name = "TestUser", FirstSeen = DateTime.UtcNow.ToString("o"), LastSeen = DateTime.UtcNow.ToString("o") };
        db.Context.Users.Add(user);
        db.Context.SaveChanges();

        store.StoreFact(new Fact { UserId = user.Id, Subject = "I", Verb = "went", Object = "cinema", PredicateType = "General", TimeContext = "yesterday", MentionedAt = DateTime.UtcNow.ToString("o"), CreatedAt = DateTime.UtcNow.ToString("o") });
        store.StoreFact(new Fact { UserId = user.Id, Subject = "I", Verb = "ate", Object = "pizza", PredicateType = "General", TimeContext = "today", MentionedAt = DateTime.UtcNow.ToString("o"), CreatedAt = DateTime.UtcNow.ToString("o") });
        store.Save();

        var yesterdayFacts = store.GetFactsWithTimeContext(user.Id, "yesterday");
        yesterdayFacts.Count.ShouldBe(1);
        yesterdayFacts[0].Object.ShouldBe("cinema");

        var todayFacts = store.GetFactsWithTimeContext(user.Id, "today");
        todayFacts.Count.ShouldBe(1);
        todayFacts[0].Object.ShouldBe("pizza");
    }

    [Fact]
    public void GetFactsByTimeRange_ReturnsFactsInRange()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);
        var now = DateTime.UtcNow;

        store.StoreFact(new Fact { Subject = "A", Verb = "is", Object = "old", PredicateType = "General", CreatedAt = now.AddDays(-10).ToString("o") });
        store.StoreFact(new Fact { Subject = "B", Verb = "is", Object = "recent", PredicateType = "General", CreatedAt = now.AddDays(-1).ToString("o") });
        store.StoreFact(new Fact { Subject = "C", Verb = "is", Object = "future", PredicateType = "General", CreatedAt = now.AddDays(1).ToString("o") });
        store.Save();

        var from = now.AddDays(-3);
        var to = now.AddDays(3);
        var facts = store.GetFactsByTimeRange(from, to);
        facts.Count.ShouldBe(2);
        facts.Any(f => f.Object == "recent").ShouldBeTrue();
        facts.Any(f => f.Object == "future").ShouldBeTrue();
    }

    [Fact]
    public void GetCategoryChain_Food()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);
        TestDataHelper.SeedInferenceWordLinks(db.Context);

        var chain = store.GetCategoryChain("pizza");
        chain.ShouldContain("food");
    }

    [Fact]
    public void GetCategoryChain_Unknown_ReturnsEmpty()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);
        TestDataHelper.SeedInferenceWordLinks(db.Context);

        var chain = store.GetCategoryChain("unknown");
        chain.ShouldBeEmpty();
    }

    [Fact]
    public void GetAllOfType_Known()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);
        TestDataHelper.SeedInferenceWordLinks(db.Context);

        var members = store.GetAllOfType("food");
        members.ShouldContain("pizza");
        members.ShouldContain("burger");
        members.ShouldContain("pasta");
    }

    [Fact]
    public void InferPreference_KnownCategory()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);
        TestDataHelper.SeedInferenceWordLinks(db.Context);

        var user = new User { Name = "Test", FirstSeen = DateTime.UtcNow.ToString("o"), LastSeen = DateTime.UtcNow.ToString("o") };
        db.Context.Users.Add(user);
        db.Context.SaveChanges();

        store.StoreFact(new Fact { UserId = user.Id, Subject = "Test", Verb = "like", Object = "pizza", PredicateType = "Preference", CreatedAt = DateTime.UtcNow.ToString("o") });
        store.Save();

        var inferred = store.InferPreference(user.Id, "food");
        inferred.ShouldNotBeNull();
        inferred.Object.ShouldBe("pizza");
    }

    [Fact]
    public void InferPreference_NoMatch_ReturnsNull()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);
        TestDataHelper.SeedInferenceWordLinks(db.Context);

        var user = new User { Name = "Test", FirstSeen = DateTime.UtcNow.ToString("o"), LastSeen = DateTime.UtcNow.ToString("o") };
        db.Context.Users.Add(user);
        db.Context.SaveChanges();

        store.StoreFact(new Fact { UserId = user.Id, Subject = "Test", Verb = "like", Object = "pizza", PredicateType = "Preference", CreatedAt = DateTime.UtcNow.ToString("o") });
        store.Save();

        var inferred = store.InferPreference(user.Id, "drink");
        inferred.ShouldBeNull();
    }

    [Fact]
    public void InferPreference_NoFacts_ReturnsNull()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);
        TestDataHelper.SeedInferenceWordLinks(db.Context);

        var inferred = store.InferPreference(999, "food");
        inferred.ShouldBeNull();
    }

    [Fact]
    public void DetectContradiction_FindsOppositePreference()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);

        var user = new User { Name = "Test", FirstSeen = DateTime.UtcNow.ToString("o"), LastSeen = DateTime.UtcNow.ToString("o") };
        db.Context.Users.Add(user);
        db.Context.SaveChanges();

        store.StoreFact(new Fact { UserId = user.Id, Subject = "Test", Verb = "like", Object = "pizza", PredicateType = "Preference", CreatedAt = DateTime.UtcNow.ToString("o") });
        store.Save();

        var contradiction = store.DetectContradiction(user.Id, "Test", "hate", "pizza");
        contradiction.ShouldNotBeNull();
        contradiction.Verb.ShouldBe("like");
        contradiction.Object.ShouldBe("pizza");
    }

    [Fact]
    public void DetectContradiction_SameVerbDifferentObject_ReturnsNull()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);

        var user = new User { Name = "Test", FirstSeen = DateTime.UtcNow.ToString("o"), LastSeen = DateTime.UtcNow.ToString("o") };
        db.Context.Users.Add(user);
        db.Context.SaveChanges();

        store.StoreFact(new Fact { UserId = user.Id, Subject = "Test", Verb = "like", Object = "pizza", PredicateType = "Preference", CreatedAt = DateTime.UtcNow.ToString("o") });
        store.Save();

        var contradiction = store.DetectContradiction(user.Id, "Test", "like", "pasta");
        contradiction.ShouldBeNull();
    }

    [Fact]
    public void DetectContradiction_NoMatch_ReturnsNull()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);

        var contradiction = store.DetectContradiction(999, "Nobody", "like", "pizza");
        contradiction.ShouldBeNull();
    }

    [Fact]
    public void GetTransitiveFacts_FindsDirectLinks()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);

        var user = new User { Name = "Test", FirstSeen = DateTime.UtcNow.ToString("o"), LastSeen = DateTime.UtcNow.ToString("o") };
        db.Context.Users.Add(user);
        db.Context.SaveChanges();

        db.Context.WordLinks.Add(new WordLink { SourceWord = "alice", TargetWord = "bob", LinkType = "friends_with", CreatedAt = DateTime.UtcNow.ToString("o") });
        db.Context.SaveChanges();

        store.StoreFact(new Fact { UserId = user.Id, Subject = "bob", Verb = "likes", Object = "chess", PredicateType = "Preference", CreatedAt = DateTime.UtcNow.ToString("o") });
        store.Save();

        var facts = store.GetTransitiveFacts("alice", "friends_with", 1);
        facts.Count.ShouldBe(1);
        facts[0].Subject.ShouldBe("bob");
    }

    [Fact]
    public void CreateConversationSession_StoresSession()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);
        var user = new User { Name = "Test", FirstSeen = DateTime.UtcNow.ToString("o"), LastSeen = DateTime.UtcNow.ToString("o") };
        db.Context.Users.Add(user);
        db.Context.SaveChanges();

        var sessionGuid = Guid.NewGuid().ToString();
        store.CreateConversationSession(sessionGuid, user.Id);
        store.Save();

        var sessions = db.Context.ConversationSessions.ToList();
        sessions.Count.ShouldBe(1);
        sessions[0].SessionGuid.ShouldBe(sessionGuid);
        sessions[0].UserId.ShouldBe(user.Id);
        sessions[0].EndedAt.ShouldBeNull();
        sessions[0].TurnCount.ShouldBe(0);
    }

    [Fact]
    public void EndConversationSession_SetsEndedAt()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);
        var user = new User { Name = "Test", FirstSeen = DateTime.UtcNow.ToString("o"), LastSeen = DateTime.UtcNow.ToString("o") };
        db.Context.Users.Add(user);
        db.Context.SaveChanges();

        var sessionGuid = Guid.NewGuid().ToString();
        store.CreateConversationSession(sessionGuid, user.Id);
        store.Save();

        store.EndConversationSession(sessionGuid);
        store.Save();

        var session = db.Context.ConversationSessions.First(s => s.SessionGuid == sessionGuid);
        session.EndedAt.ShouldNotBeNull();
    }

    [Fact]
    public void GetSessionConversationCount_ReturnsCorrectCount()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);
        var user = new User { Name = "Test", FirstSeen = DateTime.UtcNow.ToString("o"), LastSeen = DateTime.UtcNow.ToString("o") };
        db.Context.Users.Add(user);
        db.Context.SaveChanges();
        var sessionId = "test-session";
        db.Context.Conversations.Add(new Conversation { UserId = user.Id, UserInput = "a", BotResponse = "b", Timestamp = "t1", SessionId = sessionId });
        db.Context.Conversations.Add(new Conversation { UserId = user.Id, UserInput = "c", BotResponse = "d", Timestamp = "t2", SessionId = sessionId });
        db.Context.Conversations.Add(new Conversation { UserId = user.Id, UserInput = "e", BotResponse = "f", Timestamp = "t3", SessionId = "other" });
        db.Context.SaveChanges();

        var count = store.GetSessionConversationCount(sessionId);
        count.ShouldBe(2);
    }

    [Fact]
    public void LearnResponseRule_StoresAndRetrieves()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);
        var user = new User { Name = "Tutor", FirstSeen = DateTime.UtcNow.ToString("o"), LastSeen = DateTime.UtcNow.ToString("o") };
        db.Context.Users.Add(user);
        db.Context.SaveChanges();

        store.LearnResponseRule(@"\bhello\b", "Hi there!", "Statement", user.Id);
        store.Save();

        var rules = store.GetLearnedRules();
        rules.Count.ShouldBe(1);
        rules[0].Pattern.ShouldBe(@"\bhello\b");
        rules[0].ResponseTemplate.ShouldBe("Hi there!");
        rules[0].Confidence.ShouldBe(5);
        rules[0].IsActive.ShouldBeTrue();
    }

    [Fact]
    public void LearnResponseRule_Duplicate_DoesNotStore()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);

        store.LearnResponseRule(@"\btest\b", "Response A", "Statement");
        store.LearnResponseRule(@"\btest\b", "Response A", "Statement");
        store.Save();

        var rules = store.GetLearnedRules();
        rules.Count.ShouldBe(1);
    }

    [Fact]
    public void RecordFeedback_Positive_IncreasesConfidence()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);
        var user = new User { Name = "User1", FirstSeen = DateTime.UtcNow.ToString("o"), LastSeen = DateTime.UtcNow.ToString("o") };
        db.Context.Users.Add(user);
        db.Context.SaveChanges();

        store.LearnResponseRule(@"\bfoo\b", "Bar", "Statement", user.Id);
        store.Save();
        var rule = store.GetLearnedRules().First();

        store.RecordFeedback(rule.Id, user.Id, "positive", true);
        store.AdjustConfidence(rule.Id, 1, true);
        store.Save();

        var updated = store.GetLearnedRules().First();
        updated.Confidence.ShouldBe(6);
    }

    [Fact]
    public void RecordFeedback_Negative_DecreasesConfidence()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);
        var user = new User { Name = "User2", FirstSeen = DateTime.UtcNow.ToString("o"), LastSeen = DateTime.UtcNow.ToString("o") };
        db.Context.Users.Add(user);
        db.Context.SaveChanges();

        store.LearnResponseRule(@"\bbaz\b", "Qux", "Statement", user.Id);
        store.Save();
        var rule = store.GetLearnedRules().First();

        store.RecordFeedback(rule.Id, user.Id, "negative", true);
        store.AdjustConfidence(rule.Id, -2, true);
        store.Save();

        var updated = store.GetLearnedRules().First();
        updated.Confidence.ShouldBe(3);
    }

    [Fact]
    public void AdjustConfidence_ClampsToRange()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);

        store.LearnResponseRule(@"\bnobody\b", "None", "Statement");
        store.Save();
        var rule = db.Context.LearnedResponseRules.First();

        store.AdjustConfidence(rule.Id, -10, true);
        store.Save();

        var updated = db.Context.LearnedResponseRules.First();
        updated.Confidence.ShouldBe(1);
        updated.IsActive.ShouldBeFalse();
    }

    [Fact]
    public void IsLearnedRuleKnown_ReturnsTrue_WhenExists()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);

        store.LearnResponseRule(@"\bknown\b", "Yes", "Statement");
        store.Save();

        store.IsLearnedRuleKnown(@"\bknown\b").ShouldBeTrue();
        store.IsLearnedRuleKnown(@"\bunknown\b").ShouldBeFalse();
    }

    [Fact]
    public void BuildSessionSummary_ReturnsEmpty_WhenNoConversations()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);
        var result = store.BuildSessionSummary(1, "nonexistent");
        result.ShouldBeEmpty();
    }

    [Fact]
    public void BuildSessionSummary_ReturnsFactsFromSession()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);
        var user = new User { Name = "Alice", FirstSeen = DateTime.UtcNow.ToString("o"), LastSeen = DateTime.UtcNow.ToString("o") };
        db.Context.Users.Add(user);
        db.Context.SaveChanges();
        var userId = user.Id;
        var sessionId = "sum-session";

        var fact = new Fact { UserId = userId, Subject = "Alice", Verb = "likes", Object = "pizza", PredicateType = "preference", CreatedAt = DateTime.UtcNow.ToString("o") };
        store.StoreFact(fact);
        store.Save();

        db.Context.Conversations.Add(new Conversation { UserId = userId, UserInput = "Alice likes pizza", BotResponse = "Nice!", Timestamp = "t1", SessionId = sessionId });
        db.Context.SaveChanges();

        var result = store.BuildSessionSummary(userId, sessionId);
        result.ShouldContain("likes");
        result.ShouldContain("pizza");
    }

    [Fact]
    public void RecordSessionMetrics_StoresCorrectly()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);
        var user = new User { Name = "Alice", FirstSeen = DateTime.UtcNow.ToString("o"), LastSeen = DateTime.UtcNow.ToString("o") };
        db.Context.Users.Add(user);
        db.Context.SaveChanges();
        var userId = user.Id;
        var sessionId = "metric-session";

        store.StoreConversation(userId, "hi", "hello", sessionId, "greeting");
        store.StoreConversation(userId, "I like pizza", "Nice!", sessionId, "existing_fact");
        store.StoreFact(new Fact { UserId = userId, Subject = "Alice", Verb = "likes", Object = "pizza", PredicateType = "preference", Sentiment = "positive", CreatedAt = DateTime.UtcNow.ToString("o") });
        store.Save();

        store.RecordSessionMetrics(sessionId);
        store.Save();

        var metrics = db.Context.ConversationMetrics.ToList();
        metrics.Count.ShouldBe(1);
        metrics[0].SessionId.ShouldBe(sessionId);
        metrics[0].UserId.ShouldBe(userId);
        metrics[0].TurnCount.ShouldBe(2);
        metrics[0].FactsLearned.ShouldBe(1);
        metrics[0].DominantSentiment.ShouldBe("positive");
        metrics[0].TopicsDiscussed.ShouldBe(1);
        metrics[0].BotResponseStats.ShouldNotBeNull();
        metrics[0].BotResponseStats!.ShouldContain("existing_fact");
        metrics[0].AvgResponseLength.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void UpdateResponseEffectiveness_IncrementsCount()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);

        store.UpdateResponseEffectiveness("context_followup", true);
        store.Save();
        var first = db.Context.ResponseEffectiveness.First();
        first.Category.ShouldBe("context_followup");
        first.UsedCount.ShouldBe(1);
        first.FollowUpRate.ShouldBe(1.0);

        store.UpdateResponseEffectiveness("context_followup", false);
        store.Save();
        var updated = db.Context.ResponseEffectiveness.First();
        updated.UsedCount.ShouldBe(2);
        updated.FollowUpRate.ShouldBe(0.5);
    }

    [Fact]
    public void GetBestPerformingCategories_ReturnsOrdered()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);

        store.UpdateResponseEffectiveness("rule_match", true);
        store.UpdateResponseEffectiveness("rule_match", true);
        store.UpdateResponseEffectiveness("context_followup", false);
        store.UpdateResponseEffectiveness("context_followup", false);
        store.Save();

        var best = store.GetBestPerformingCategories(5);
        best.Count.ShouldBe(2);
        best[0].ShouldBe("rule_match");
        best[1].ShouldBe("context_followup");
    }
}

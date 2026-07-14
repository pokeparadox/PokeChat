using Microsoft.EntityFrameworkCore;
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

    [Fact]
    public void GetUserFactsFormatted_ReturnsNumberedList()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);
        var user = new User { Name = "Alice", FirstSeen = DateTime.UtcNow.ToString("o"), LastSeen = DateTime.UtcNow.ToString("o") };
        db.Context.Users.Add(user);
        db.Context.SaveChanges();

        store.StoreFact(new Fact { UserId = user.Id, Subject = "Alice", Verb = "likes", Object = "pizza", PredicateType = "Preference", CreatedAt = DateTime.UtcNow.ToString("o") });
        store.StoreFact(new Fact { UserId = user.Id, Subject = "Alice", Verb = "has", Object = "a cat", PredicateType = "Possession", CreatedAt = DateTime.UtcNow.ToString("o") });
        store.Save();

        var result = store.GetUserFactsFormatted(user.Id);
        result.ShouldNotBeNull();
        result.ShouldContain("1)");
        result.ShouldContain("likes");
        result.ShouldContain("pizza");
        result.ShouldContain("has");
        result.ShouldContain("a cat");
    }

    [Fact]
    public void GetUserFactsFormatted_ReturnsNull_WhenNoFacts()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);

        var result = store.GetUserFactsFormatted(1);
        result.ShouldBeNull();
    }

    [Fact]
    public void GetUserFactsFormatted_GroupsByPredicateType_WhenMoreThan10()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);
        var user = new User { Name = "Bob", FirstSeen = DateTime.UtcNow.ToString("o"), LastSeen = DateTime.UtcNow.ToString("o") };
        db.Context.Users.Add(user);
        db.Context.SaveChanges();

        for (int i = 0; i < 12; i++)
        {
            store.StoreFact(new Fact { UserId = user.Id, Subject = "Bob", Verb = "likes", Object = $"thing{i}", PredicateType = "Preference", CreatedAt = DateTime.UtcNow.ToString("o") });
        }
        store.Save();

        var result = store.GetUserFactsFormatted(user.Id);
        result.ShouldNotBeNull();
        result.ShouldContain("Preference:");
        result.ShouldNotContain("1)");
    }

    [Fact]
    public void GetUserStatsFormatted_ReturnsFormattedStats()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);
        var user = new User { Name = "Charlie", FirstSeen = DateTime.UtcNow.ToString("o"), LastSeen = DateTime.UtcNow.ToString("o") };
        db.Context.Users.Add(user);
        db.Context.SaveChanges();

        store.StoreFact(new Fact { UserId = user.Id, Subject = "Charlie", Verb = "likes", Object = "dogs", PredicateType = "Preference", Sentiment = "positive", CreatedAt = DateTime.UtcNow.ToString("o") });
        store.StoreFact(new Fact { UserId = user.Id, Subject = "Charlie", Verb = "likes", Object = "cats", PredicateType = "Preference", Sentiment = "positive", CreatedAt = DateTime.UtcNow.ToString("o") });
        store.StoreConversation(user.Id, "hello", "hi", "session1");
        store.StoreConversation(user.Id, "I like dogs", "Nice!", "session1");
        store.Save();

        var result = store.GetUserStatsFormatted(user.Id);
        result.ShouldNotBeNull();
        result.ShouldContain("Total facts");
        result.ShouldContain("2");
        result.ShouldContain("Conversations");
        result.ShouldContain("2");
        result.ShouldContain("Sessions");
        result.ShouldContain("1");
        result.ShouldContain("Most talked about");
    }

    [Fact]
    public void GetUserStatsFormatted_ReturnsNull_WhenNoFacts()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);

        var result = store.GetUserStatsFormatted(1);
        result.ShouldBeNull();
    }

    [Fact]
    public void GetPositiveFacts_ReturnsOnlyPositiveVerbs()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);
        var user = new User { Name = "Dave", FirstSeen = DateTime.UtcNow.ToString("o"), LastSeen = DateTime.UtcNow.ToString("o") };
        db.Context.Users.Add(user);
        db.Context.SaveChanges();

        store.StoreFact(new Fact { UserId = user.Id, Subject = "Dave", Verb = "like", Object = "pizza", PredicateType = "Preference", CreatedAt = DateTime.UtcNow.ToString("o") });
        store.StoreFact(new Fact { UserId = user.Id, Subject = "Dave", Verb = "hate", Object = "broccoli", PredicateType = "Dislike", CreatedAt = DateTime.UtcNow.ToString("o") });
        store.StoreFact(new Fact { UserId = user.Id, Subject = "Dave", Verb = "enjoy", Object = "running", PredicateType = "Preference", CreatedAt = DateTime.UtcNow.ToString("o") });
        store.Save();

        var positive = store.GetPositiveFacts(user.Id);
        positive.Count.ShouldBe(2);
        positive.All(f => f.Verb is "like" or "love" or "enjoy" or "prefer").ShouldBeTrue();
    }

    [Fact]
    public void GetRandomPositiveFact_ReturnsFact_WhenExists()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);
        var user = new User { Name = "Eve", FirstSeen = DateTime.UtcNow.ToString("o"), LastSeen = DateTime.UtcNow.ToString("o") };
        db.Context.Users.Add(user);
        db.Context.SaveChanges();

        store.StoreFact(new Fact { UserId = user.Id, Subject = "Eve", Verb = "like", Object = "music", PredicateType = "Preference", CreatedAt = DateTime.UtcNow.ToString("o") });
        store.Save();

        var fact = store.GetRandomPositiveFact(user.Id);
        fact.ShouldNotBeNull();
        fact.Object.ShouldBe("music");
    }

    [Fact]
    public void GetRandomPositiveFact_ReturnsNull_WhenNoPositiveFacts()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);

        var fact = store.GetRandomPositiveFact(1);
        fact.ShouldBeNull();
    }

    [Fact]
    public void GetUserPreferences_ReturnsPreferenceFacts()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);
        var user = new User { Name = "Alice", FirstSeen = DateTime.UtcNow.ToString("o"), LastSeen = DateTime.UtcNow.ToString("o") };
        db.Context.Users.Add(user);
        db.Context.SaveChanges();

        store.StoreFact(new Fact { UserId = user.Id, Subject = "Alice", Verb = "like", Object = "pizza", PredicateType = "Preference", CreatedAt = DateTime.UtcNow.ToString("o") });
        store.StoreFact(new Fact { UserId = user.Id, Subject = "Alice", Verb = "love", Object = "cats", PredicateType = "Preference", CreatedAt = DateTime.UtcNow.ToString("o") });
        store.StoreFact(new Fact { UserId = user.Id, Subject = "Alice", Verb = "hate", Object = "broccoli", PredicateType = "Dislike", CreatedAt = DateTime.UtcNow.ToString("o") });
        store.StoreFact(new Fact { UserId = user.Id, Subject = "Alice", Verb = "enjoy", Object = "running", PredicateType = "Preference", CreatedAt = DateTime.UtcNow.ToString("o") });
        store.Save();

        var prefs = store.GetUserPreferences(user.Id);
        prefs.Count.ShouldBe(3);
        prefs.All(f => f.Verb is "like" or "love" or "enjoy" or "prefer").ShouldBeTrue();
    }

    [Fact]
    public void GetRecommendation_ReturnsSuggestion_WhenUnexploredRelatedItemsExist()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);
        var user = new User { Name = "Bob", FirstSeen = DateTime.UtcNow.ToString("o"), LastSeen = DateTime.UtcNow.ToString("o") };
        db.Context.Users.Add(user);
        TestDataHelper.SeedInferenceWordLinks(db.Context);
        db.Context.SaveChanges();

        store.StoreFact(new Fact { UserId = user.Id, Subject = "Bob", Verb = "like", Object = "pizza", PredicateType = "Preference", CreatedAt = DateTime.UtcNow.ToString("o") });
        store.StoreFact(new Fact { UserId = user.Id, Subject = "Bob", Verb = "like", Object = "pasta", PredicateType = "Preference", CreatedAt = DateTime.UtcNow.ToString("o") });
        store.Save();

        var (liked, suggestion, category) = store.GetRecommendation(user.Id);
        liked.ShouldNotBeNull();
        suggestion.ShouldNotBeNull();
        category.ShouldNotBeNull();
        (suggestion is "burger" or "salad").ShouldBeTrue();
        category.ShouldBe("food");
    }

    [Fact]
    public void GetRecommendation_ReturnsNull_WhenFewerThanTwoPreferences()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);
        var user = new User { Name = "Carol", FirstSeen = DateTime.UtcNow.ToString("o"), LastSeen = DateTime.UtcNow.ToString("o") };
        db.Context.Users.Add(user);
        TestDataHelper.SeedInferenceWordLinks(db.Context);
        db.Context.SaveChanges();

        store.StoreFact(new Fact { UserId = user.Id, Subject = "Carol", Verb = "like", Object = "pizza", PredicateType = "Preference", CreatedAt = DateTime.UtcNow.ToString("o") });
        store.Save();

        var (liked, suggestion, category) = store.GetRecommendation(user.Id);
        liked.ShouldBeNull();
        suggestion.ShouldBeNull();
        category.ShouldBeNull();
    }

    [Fact]
    public void GetRecommendation_SkipsAlreadyKnownFacts()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);
        var user = new User { Name = "Dave", FirstSeen = DateTime.UtcNow.ToString("o"), LastSeen = DateTime.UtcNow.ToString("o") };
        db.Context.Users.Add(user);
        TestDataHelper.SeedInferenceWordLinks(db.Context);
        db.Context.SaveChanges();

        store.StoreFact(new Fact { UserId = user.Id, Subject = "Dave", Verb = "like", Object = "pizza", PredicateType = "Preference", CreatedAt = DateTime.UtcNow.ToString("o") });
        store.StoreFact(new Fact { UserId = user.Id, Subject = "Dave", Verb = "like", Object = "pasta", PredicateType = "Preference", CreatedAt = DateTime.UtcNow.ToString("o") });
        store.StoreFact(new Fact { UserId = user.Id, Subject = "Dave", Verb = "like", Object = "burger", PredicateType = "Preference", CreatedAt = DateTime.UtcNow.ToString("o") });
        store.StoreFact(new Fact { UserId = user.Id, Subject = "Dave", Verb = "like", Object = "salad", PredicateType = "Preference", CreatedAt = DateTime.UtcNow.ToString("o") });
        store.Save();

        var (liked, suggestion, category) = store.GetRecommendation(user.Id);
        liked.ShouldBeNull();
        suggestion.ShouldBeNull();
        category.ShouldBeNull();
    }

    [Fact]
    public void GetFactsInDateRange_ReturnsCorrectFacts()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);
        var user = new User { Name = "Tim", FirstSeen = DateTime.UtcNow.ToString("o"), LastSeen = DateTime.UtcNow.ToString("o") };
        db.Context.Users.Add(user);
        db.Context.SaveChanges();

        var now = DateTime.UtcNow;
        store.StoreFact(new Fact { UserId = user.Id, Subject = "Tim", Verb = "start", Object = "new job", MentionedAt = now.AddDays(-3).ToString("o"), PredicateType = "GeneralFact", CreatedAt = now.ToString("o") });
        store.StoreFact(new Fact { UserId = user.Id, Subject = "Tim", Verb = "like", Object = "team", MentionedAt = now.AddDays(-2).ToString("o"), PredicateType = "Preference", CreatedAt = now.ToString("o") });
        store.StoreFact(new Fact { UserId = user.Id, Subject = "Tim", Verb = "go", Object = "cinema", MentionedAt = now.AddDays(-10).ToString("o"), PredicateType = "GeneralFact", CreatedAt = now.ToString("o") });
        store.Save();

        var weekAgo = now.AddDays(-7);
        var facts = store.GetFactsInDateRange(user.Id, weekAgo, now);
        facts.Count.ShouldBe(2);
        facts.All(f => f.Object is "new job" or "team").ShouldBeTrue();
    }

    [Fact]
    public void BuildTimeline_FormatsFacts_WithDayLabels()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);

        var now = DateTime.UtcNow;
        var facts = new List<Fact>
        {
            new() { Subject = "Tim", Verb = "start", Object = "new job", MentionedAt = new DateTime(2026, 7, 1, 10, 0, 0, DateTimeKind.Utc).ToString("o"), PredicateType = "GeneralFact", CreatedAt = now.ToString("o") },
            new() { Subject = "Tim", Verb = "like", Object = "team", MentionedAt = new DateTime(2026, 7, 2, 14, 0, 0, DateTimeKind.Utc).ToString("o"), PredicateType = "Preference", CreatedAt = now.ToString("o") },
        };

        var timeline = store.BuildTimeline(facts);
        timeline.ShouldContain("Wednesday");
        timeline.ShouldContain("Thursday");
        timeline.ShouldContain("starts");
        timeline.ShouldContain("liked");
    }

    [Fact]
    public void BuildTimeline_ReturnsEmpty_WhenNoFacts()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);

        var timeline = store.BuildTimeline(new List<Fact>());
        timeline.ShouldBeEmpty();
    }

    [Fact]
    public void HandleTimelineRequest_ExplicitTrigger_ReturnsTimeline()
    {
        using var db = new FreshDbContext();
        TestDataHelper.SeedBotResponses(db.Context);
        var store = new KnowledgeStore(db.Context);
        var context = new ContextTracker();
        var engine = CreateEngine(db.Context, context);

        var user = new User { Name = "Jane", FirstSeen = DateTime.UtcNow.ToString("o"), LastSeen = DateTime.UtcNow.ToString("o") };
        db.Context.Users.Add(user);
        db.Context.SaveChanges();

        var now = DateTime.UtcNow;
        store.StoreFact(new Fact { UserId = user.Id, Subject = "Jane", Verb = "start", Object = "new job", MentionedAt = now.AddDays(-2).ToString("o"), PredicateType = "GeneralFact", CreatedAt = now.ToString("o") });
        store.StoreFact(new Fact { UserId = user.Id, Subject = "Jane", Verb = "like", Object = "team", MentionedAt = now.AddDays(-1).ToString("o"), PredicateType = "Preference", CreatedAt = now.ToString("o") });
        store.Save();

        var response = engine.GenerateResponse("what happened this week", user.Id);
        response.ShouldNotBeNullOrEmpty();
        response.ShouldContain("Jane");
    }

    [Fact]
    public void HandleTimelineRequest_EmptyRange_ReturnsEmptyMessage()
    {
        using var db = new FreshDbContext();
        TestDataHelper.SeedBotResponses(db.Context);
        var store = new KnowledgeStore(db.Context);
        var context = new ContextTracker();
        var engine = CreateEngine(db.Context, context);

        var user = new User { Name = "Kate", FirstSeen = DateTime.UtcNow.ToString("o"), LastSeen = DateTime.UtcNow.ToString("o") };
        db.Context.Users.Add(user);
        db.Context.SaveChanges();

        var response = engine.GenerateResponse("what happened this week", user.Id);
        response.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void GetEntityGraph_BuildsFromFacts()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);
        var userId = store.GetOrCreateUser("Alice");

        store.StoreFact(new Fact { UserId = userId, Subject = "Alice", Verb = "likes", Object = "pizza", PredicateType = "preference", CreatedAt = DateTime.UtcNow.ToString("o") });
        store.StoreFact(new Fact { UserId = userId, Subject = "Alice", Verb = "works at", Object = "library", PredicateType = "GeneralFact", CreatedAt = DateTime.UtcNow.ToString("o") });
        store.Save();

        var graph = store.GetEntityGraph(userId!.Value);
        graph.ShouldContainKey("alice");
        graph["alice"].Count.ShouldBe(2);
    }

    [Fact]
    public void FindPath_DirectConnection()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);
        var userId = store.GetOrCreateUser("Bob");

        store.StoreFact(new Fact { UserId = userId, Subject = "Bob", Verb = "likes", Object = "cats", PredicateType = "preference", CreatedAt = DateTime.UtcNow.ToString("o") });
        store.Save();

        var path = store.FindPath(userId!.Value, "Bob", "cats");
        path.ShouldNotBeNull();
        path.ShouldContain("Bob");
        path.ShouldContain("likes");
        path.ShouldContain("cats");
    }

    [Fact]
    public void FindPath_MultiHop()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);
        var userId = store.GetOrCreateUser("Charlie");

        store.StoreFact(new Fact { UserId = userId, Subject = "Charlie", Verb = "likes", Object = "Alice", PredicateType = "preference", CreatedAt = DateTime.UtcNow.ToString("o") });
        store.StoreFact(new Fact { UserId = userId, Subject = "Alice", Verb = "works at", Object = "library", PredicateType = "GeneralFact", CreatedAt = DateTime.UtcNow.ToString("o") });
        store.Save();

        var path = store.FindPath(userId!.Value, "Charlie", "library");
        path.ShouldNotBeNull();
        path.ShouldContain("Charlie");
        path.ShouldContain("likes");
        path.ShouldContain("Alice");
        path.ShouldContain("works at");
        path.ShouldContain("library");
    }

    [Fact]
    public void FindPath_NoConnection_ReturnsNull()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);
        var userId = store.GetOrCreateUser("Dave");

        store.StoreFact(new Fact { UserId = userId, Subject = "Dave", Verb = "likes", Object = "dogs", PredicateType = "preference", CreatedAt = DateTime.UtcNow.ToString("o") });
        store.Save();

        var path = store.FindPath(userId!.Value, "Dave", "library");
        path.ShouldBeNull();
    }

    [Fact]
    public void CheckRelation_ReturnsTrue_WhenEdgeExists()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);
        var userId = store.GetOrCreateUser("Eve");

        store.StoreFact(new Fact { UserId = userId, Subject = "Eve", Verb = "likes", Object = "chocolate", PredicateType = "preference", CreatedAt = DateTime.UtcNow.ToString("o") });
        store.Save();

        store.CheckRelation(userId!.Value, "Eve", "likes", "chocolate").ShouldBeTrue();
        store.CheckRelation(userId!.Value, "Eve", "likes", "broccoli").ShouldBeFalse();
    }

    [Fact]
    public void HandleEntityQuery_ExplicitRelation_ReturnsYesNo()
    {
        using var db = new FreshDbContext();
        TestDataHelper.SeedBotResponses(db.Context);
        var store = new KnowledgeStore(db.Context);
        var context = new ContextTracker();
        var engine = CreateEngine(db.Context, context);
        var userId = store.GetOrCreateUser("Frank");

        store.StoreFact(new Fact { UserId = userId, Subject = "frank", Verb = "like", Object = "pizza", PredicateType = "preference", CreatedAt = DateTime.UtcNow.ToString("o") });
        store.Save();

        var response = engine.GenerateResponse("does frank like pizza", userId!.Value);
        response.ShouldNotBeNullOrEmpty();
        response.ShouldContain("frank");
        response.ShouldContain("like");
        response.ShouldContain("pizza");
    }

    [Fact]
    public void BuildEntityConnectionNotice_DetectsNewLink()
    {
        using var db = new FreshDbContext();
        TestDataHelper.SeedBotResponses(db.Context);
        var store = new KnowledgeStore(db.Context);
        var context = new ContextTracker();
        var engine = CreateEngine(db.Context, context);
        var userId = store.GetOrCreateUser("Grace");

        store.StoreFact(new Fact { UserId = userId, Subject = "grace", Verb = "likes", Object = "music", PredicateType = "preference", CreatedAt = DateTime.UtcNow.ToString("o") });
        store.StoreFact(new Fact { UserId = userId, Subject = "grace", Verb = "plays", Object = "guitar", PredicateType = "skill", CreatedAt = DateTime.UtcNow.ToString("o") });
        store.Save();

        var connected = store.GetConnectedEntities(userId!.Value, "grace");
        connected.ShouldNotBeEmpty();
    }

    [Fact]
    public void GetBotResponse_FiltersByPersona()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);

        var now = DateTime.UtcNow.ToString("o");
        db.Context.BotResponses.Add(new BotResponse { Category = "greeting", ResponseText = "Hello chat!", Persona = "chat", CreatedAt = now });
        db.Context.BotResponses.Add(new BotResponse { Category = "greeting", ResponseText = "Hello coding!", Persona = "coding", CreatedAt = now });
        db.Context.BotResponses.Add(new BotResponse { Category = "greeting", ResponseText = "Hello everyone!", Persona = null, CreatedAt = now });
        db.Context.SaveChanges();

        var chatResponses = store.GetBotResponses("chat");
        chatResponses["greeting"].ShouldContain("Hello chat!");
        chatResponses["greeting"].ShouldContain("Hello everyone!");
        chatResponses["greeting"].ShouldNotContain("Hello coding!");

        var codingResponses = store.GetBotResponses("coding");
        codingResponses["greeting"].ShouldContain("Hello coding!");
        codingResponses["greeting"].ShouldContain("Hello everyone!");
        codingResponses["greeting"].ShouldNotContain("Hello chat!");
    }

    [Fact]
    public void GetResponseRule_FiltersByPersona()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);

        var now = DateTime.UtcNow.ToString("o");
        var chatRule = new ResponseRule { Pattern = "chat pattern", InputType = "Statement", IsActive = true, Persona = "chat", CreatedAt = now };
        var codingRule = new ResponseRule { Pattern = "coding pattern", InputType = "Statement", IsActive = true, Persona = "coding", CreatedAt = now };
        var nullRule = new ResponseRule { Pattern = "null pattern", InputType = "Statement", IsActive = true, Persona = null, CreatedAt = now };
        db.Context.ResponseRules.AddRange(chatRule, codingRule, nullRule);
        db.Context.SaveChanges();

        var chatRules = store.GetResponseRules("chat");
        chatRules.ShouldContain(r => r.Pattern == "chat pattern");
        chatRules.ShouldContain(r => r.Pattern == "null pattern");
        chatRules.ShouldNotContain(r => r.Pattern == "coding pattern");

        var codingRules = store.GetResponseRules("coding");
        codingRules.ShouldContain(r => r.Pattern == "coding pattern");
        codingRules.ShouldContain(r => r.Pattern == "null pattern");
        codingRules.ShouldNotContain(r => r.Pattern == "chat pattern");
    }

    [Fact]
    public void GreetingPool_UsesPersonaGreeting()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);

        var now = DateTime.UtcNow.ToString("o");
        db.Context.Greetings.Add(new Greeting { Text = "Chat hello!", Persona = "chat", IsSystem = true, CreatedAt = now });
        db.Context.Greetings.Add(new Greeting { Text = "Coding hello!", Persona = "coding", IsSystem = true, CreatedAt = now });
        db.Context.Greetings.Add(new Greeting { Text = "Generic hello!", Persona = null, IsSystem = true, CreatedAt = now });
        db.Context.SaveChanges();

        var chatGreetings = store.GetGreetings("chat");
        chatGreetings.ShouldContain(g => g.Text == "Chat hello!");
        chatGreetings.ShouldContain(g => g.Text == "Generic hello!");
        chatGreetings.ShouldNotContain(g => g.Text == "Coding hello!");

        var codingGreetings = store.GetGreetings("coding");
        codingGreetings.ShouldContain(g => g.Text == "Coding hello!");
        codingGreetings.ShouldContain(g => g.Text == "Generic hello!");
        codingGreetings.ShouldNotContain(g => g.Text == "Chat hello!");
    }

    [Fact]
    public void Fallback_ToNullPersona_WhenPersonaHasNoMatch()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);

        var now = DateTime.UtcNow.ToString("o");
        db.Context.BotResponses.Add(new BotResponse { Category = "greeting", ResponseText = "Hello!", Persona = null, CreatedAt = now });
        db.Context.SaveChanges();

        var responses = store.GetBotResponses("coding");
        responses["greeting"].ShouldContain("Hello!");
    }

    [Fact]
    public void MatchError_FindsMatchingError()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);
        store.LearnError("CS1009", "Unrecognised escape sequence", "csharp");
        store.Save();

        var result = store.MatchError("CS1009: unrecognised escape sequence in string literal");
        result.ShouldNotBeNull();
        result.Suggestion.ShouldBe("Unrecognised escape sequence");
        result.Language.ShouldBe("csharp");
    }

    [Fact]
    public void MatchError_ReturnsNull_WhenNoMatch()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);
        store.LearnError("CS1009", "Unrecognised escape sequence", "csharp");
        store.Save();

        var result = store.MatchError("Everything compiles fine today");
        result.ShouldBeNull();
    }

    [Fact]
    public void MatchError_MultipleEntries_ReturnsFirstMatch()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);
        store.LearnError("CS0161.*not all code paths", "Add a return statement");
        store.LearnError("CS0103.*does not exist", "Check spelling and using directives");
        store.Save();

        var result = store.MatchError("CS0161: not all code paths return a value");
        result.ShouldNotBeNull();
        result.Suggestion.ShouldBe("Add a return statement");
    }

    [Fact]
    public void MatchError_CaseInsensitiveMatching()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);
        store.LearnError("NullReferenceException", "Check that you've initialised the object");
        store.Save();

        var result = store.MatchError("nullreferenceexception: object reference not set");
        result.ShouldNotBeNull();
        result.Suggestion.ShouldBe("Check that you've initialised the object");
    }

    [Fact]
    public void MatchError_RejectsVeryShortInput()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);
        store.LearnError("error", "some fix");
        store.Save();

        var result = store.MatchError("hi");
        result.ShouldBeNull();
    }

    [Fact]
    public void LearnError_PersistsEntry()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);
        store.LearnError("CS8618", "Non-nullable property not initialised", "csharp");
        store.Save();

        var entries = db.Context.ErrorKnowledgeEntries.ToList();
        entries.Count.ShouldBe(1);
        entries[0].Pattern.ShouldBe("CS8618");
        entries[0].Suggestion.ShouldBe("Non-nullable property not initialised");
        entries[0].Language.ShouldBe("csharp");
        entries[0].IsLearned.ShouldBeTrue();
    }

    [Fact]
    public void IncrementErrorUsage_IncrementsCount()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);
        store.LearnError("CS1009", "fix");
        store.Save();

        var entry = store.MatchError("CS1009 here");
        entry.ShouldNotBeNull();
        store.IncrementErrorUsage(entry.Id);
        store.Save();

        var reloaded = db.Context.ErrorKnowledgeEntries.Find(entry.Id);
        reloaded.ShouldNotBeNull();
        reloaded.UsedCount.ShouldBe(1);
    }

    [Fact]
    public void IncrementErrorSuccess_IncrementsCount()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);
        store.LearnError("CS1009", "fix");
        store.Save();

        var entry = store.MatchError("CS1009 here");
        entry.ShouldNotBeNull();
        store.IncrementErrorSuccess(entry.Id);
        store.Save();

        var reloaded = db.Context.ErrorKnowledgeEntries.Find(entry.Id);
        reloaded.ShouldNotBeNull();
        reloaded.SuccessCount.ShouldBe(1);
    }

    [Fact]
    public void MatchError_RegexPattern_MultipleCandidates()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);
        store.LearnError("CS1501.*no overload", "Check method signature");
        store.LearnError("CS1502.*best overloaded match", "Check parameter types");
        store.Save();

        var result1 = store.MatchError("CS1501: no overload for method 'Foo' takes 1 argument");
        result1.ShouldNotBeNull();
        result1.Suggestion.ShouldBe("Check method signature");

        var result2 = store.MatchError("CS1502: the best overloaded match has some invalid arguments");
        result2.ShouldNotBeNull();
        result2.Suggestion.ShouldBe("Check parameter types");
    }

    [Fact]
    public void DecayCleanup_DryRun_DoesNotDelete()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);
        var old = DateTime.UtcNow.AddDays(-100).ToString("o");
        store.StoreFact(new Fact { Subject = "old", Verb = "is", Object = "stale", PredicateType = "general", CreatedAt = old });
        store.Save();
        var report = store.DecayCleanup(dryRun: true);
        report.DeletedFacts.ShouldBe(1);
        report.DryRun.ShouldBeTrue();
        db.Context.Facts.Count().ShouldBe(1);
    }

    [Fact]
    public void DecayCleanup_DeleteStaleFacts()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);
        var old = DateTime.UtcNow.AddDays(-100).ToString("o");
        store.StoreFact(new Fact { Subject = "stale", Verb = "is", Object = "forgotten", PredicateType = "general", CreatedAt = old });
        store.Save();
        var report = store.DecayCleanup(dryRun: false);
        report.DeletedFacts.ShouldBe(1);
        db.Context.Facts.Count().ShouldBe(0);
    }

    [Fact]
    public void DecayCleanup_PreservesRecentFacts()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);
        var fresh = DateTime.UtcNow.ToString("o");
        store.StoreFact(new Fact { Subject = "new", Verb = "is", Object = "fresh", PredicateType = "general", CreatedAt = fresh });
        store.Save();
        var report = store.DecayCleanup(dryRun: false);
        report.DeletedFacts.ShouldBe(0);
        db.Context.Facts.Count().ShouldBe(1);
    }

    [Fact]
    public void DecayCleanup_PreservesAccessedFacts()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);
        var old = DateTime.UtcNow.AddDays(-100).ToString("o");
        store.StoreFact(new Fact { Subject = "old", Verb = "but", Object = "accessed", PredicateType = "general", CreatedAt = old });
        store.Save();
        var fact = store.GetFact("old", "but", "accessed");
        fact.ShouldNotBeNull();
        var report = store.DecayCleanup(dryRun: false);
        report.DeletedFacts.ShouldBe(0);
        db.Context.Facts.Count().ShouldBe(1);
    }

    [Fact]
    public void DecayCleanup_PreservesHighConfidenceFacts()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);
        var old = DateTime.UtcNow.AddDays(-100).ToString("o");
        var entity = new FactEntity
        {
            Subject = "trusted", Verb = "is", Object = "important", PredicateType = "general",
            CreatedAt = old, Confidence = 2.5
        };
        db.Context.Facts.Add(entity);
        db.Context.SaveChanges();
        var report = store.DecayCleanup(dryRun: false);
        report.DeletedFacts.ShouldBe(0);
        db.Context.Facts.Count().ShouldBe(1);
    }

    [Fact]
    public void TouchFactAccess_UpdatesCount()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);
        store.StoreFact(new Fact { Subject = "test", Verb = "is", Object = "counted", PredicateType = "general", CreatedAt = DateTime.UtcNow.ToString("o") });
        store.Save();
        var fact = store.GetFact("test", "is", "counted");
        fact.ShouldNotBeNull();
        var entity = db.Context.Facts.First(f => f.Subject == "test");
        entity.AccessCount.ShouldBe(1);
        entity.LastAccessed.ShouldNotBeNull();
    }

    [Fact]
    public void DecayCleanup_VACUUM_ReclaimsSpace()
    {
        var tmpFile = Path.Combine(Path.GetTempPath(), $"pokechat_test_{Guid.NewGuid():N}.db");
        try
        {
            using var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={tmpFile}");
            connection.Open();
            var options = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<PokeChat.Data.PokeChatDbContext>()
                .UseSqlite(connection)
                .Options;
            using var context = new PokeChat.Data.PokeChatDbContext(options);
            context.Database.EnsureCreated();
            var store = new KnowledgeStore(context);
            for (int i = 0; i < 60; i++)
            {
                store.StoreFact(new Fact { Subject = $"bulk{i}", Verb = "is", Object = $"item{i}", PredicateType = "general", CreatedAt = DateTime.UtcNow.AddDays(-200).ToString("o") });
            }
            store.Save();
            var sizeBefore = new FileInfo(tmpFile).Length;
            var report = store.DecayCleanup(dryRun: false, vacuumThreshold: 50);
            report.DeletedFacts.ShouldBe(60);
            report.ReclaimedBytes.ShouldNotBeNull();
            report.ReclaimedBytes!.Value.ShouldBeGreaterThanOrEqualTo(0);
        }
        finally
        {
            if (File.Exists(tmpFile)) File.Delete(tmpFile);
        }
    }

    [Fact]
    public void DecayCleanup_NoVACUUM_BelowThreshold()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);
        for (int i = 0; i < 5; i++)
        {
            store.StoreFact(new Fact { Subject = $"few{i}", Verb = "is", Object = $"item{i}", PredicateType = "general", CreatedAt = DateTime.UtcNow.AddDays(-200).ToString("o") });
        }
        store.Save();
        var report = store.DecayCleanup(dryRun: false, vacuumThreshold: 50);
        report.DeletedFacts.ShouldBe(5);
        report.ReclaimedBytes.ShouldBeNull();
    }

    [Fact]
    public void GetRandomRiddle_ReturnsRiddle_WhenNoExclusions()
    {
        using var db = new FreshDbContext();
        TestDataHelper.SeedRiddles(db.Context);
        var store = new KnowledgeStore(db.Context);
        var riddle = store.GetRandomRiddle();
        riddle.ShouldNotBeNull();
        riddle.Question.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void GetRandomRiddle_ExcludesRecentQuestions()
    {
        using var db = new FreshDbContext();
        TestDataHelper.SeedRiddles(db.Context);
        var store = new KnowledgeStore(db.Context);
        var allRiddles = db.Context.Riddles.ToList();
        var exclude = new HashSet<string>(allRiddles.Take(4).Select(r => r.Question));
        var riddle = store.GetRandomRiddle(exclude);
        riddle.ShouldNotBeNull();
        exclude.ShouldNotContain(riddle.Question);
    }

    [Fact]
    public void GetRandomRiddle_AllExcluded_ReturnsUnfiltered()
    {
        using var db = new FreshDbContext();
        TestDataHelper.SeedRiddles(db.Context);
        var store = new KnowledgeStore(db.Context);
        var allRiddles = db.Context.Riddles.ToList();
        var exclude = new HashSet<string>(allRiddles.Select(r => r.Question));
        var riddle = store.GetRandomRiddle(exclude);
        riddle.ShouldNotBeNull();
    }

    [Fact]
    public void GetRandomRiddle_EmptyExclusionSet_Works()
    {
        using var db = new FreshDbContext();
        TestDataHelper.SeedRiddles(db.Context);
        var store = new KnowledgeStore(db.Context);
        var riddle = store.GetRandomRiddle(new HashSet<string>());
        riddle.ShouldNotBeNull();
    }

    private static PokeChat.Responses.ResponseEngine CreateEngine(PokeChat.Data.PokeChatDbContext db, ContextTracker context)
    {
        TestDataHelper.SeedBotResponses(db);
        var knowledgeStore = new KnowledgeStore(db);
        var spellChecker = new PokeChat.NLP.SpellChecker();
        spellChecker.Initialise(new HashSet<string>(StringComparer.OrdinalIgnoreCase), new Dictionary<string, string>());
        var posTagger = new PokeChat.NLP.PosTagger([]);
        var tokeniser = new PokeChat.NLP.Tokeniser();
        var svoExtractor = new PokeChat.NLP.SvoExtractor();
        return new PokeChat.Responses.ResponseEngine(knowledgeStore, context, spellChecker, posTagger, tokeniser, svoExtractor);
    }
}

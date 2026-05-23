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
            CreatedAt = DateTime.UtcNow.ToString("O")
        };
        store.StoreFact(fact);
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
        store.StoreFact(new Fact { Subject = "Bob", Verb = "has", Object = "car", PredicateType = "possession", CreatedAt = DateTime.UtcNow.ToString("O") });
        store.StoreFact(new Fact { Subject = "Bob", Verb = "likes", Object = "dogs", PredicateType = "preference", CreatedAt = DateTime.UtcNow.ToString("O") });
        var facts = store.GetFactsBySubject("Bob");
        facts.Count.ShouldBe(2);
    }

    [Fact]
    public void GetFactsByUser()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);
        var userId = store.GetOrCreateUser("Charlie");
        store.StoreFact(new Fact { UserId = userId, Subject = "Charlie", Verb = "likes", Object = "cats", PredicateType = "preference", CreatedAt = DateTime.UtcNow.ToString("O") });
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
}

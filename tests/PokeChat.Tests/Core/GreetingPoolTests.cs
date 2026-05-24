using PokeChat.Core;
using PokeChat.Data.Entities;
using PokeChat.Knowledge;
using PokeChat.Tests.Helpers;
using Shouldly;

namespace PokeChat.Tests.Core;

public class GreetingPoolTests
{
    [Fact]
    public void GetRandomGreeting_ReturnsGreeting_WhenGreetingsExist()
    {
        using var db = new FreshDbContext();
        db.Context.Greetings.Add(new Greeting
        {
            Text = "Hello!",
            IsSystem = true,
            CreatedAt = DateTime.UtcNow.ToString("O")
        });
        db.Context.SaveChanges();

        var store = new KnowledgeStore(db.Context);
        var greeting = GreetingPool.GetRandomGreeting(store, "PokeChat");
        greeting.ShouldBe("Hello!");
    }

    [Fact]
    public void GetRandomGreeting_ReturnsFallback_WhenNoGreetings()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);
        var greeting = GreetingPool.GetRandomGreeting(store, "PokeChat");
        greeting.ShouldBe("Hello! I'm PokeChat. What's your name?");
    }

    [Fact]
    public void GetRandomGreeting_UsesBotName()
    {
        using var db = new FreshDbContext();
        db.Context.Greetings.Add(new Greeting
        {
            Text = "Hello! I'm {BOTNAME}. What's your name?",
            IsSystem = true,
            CreatedAt = DateTime.UtcNow.ToString("O")
        });
        db.Context.SaveChanges();

        var store = new KnowledgeStore(db.Context);
        var greeting = GreetingPool.GetRandomGreeting(store, "Jeff");
        greeting.ShouldBe("Hello! I'm Jeff. What's your name?");
    }

    [Fact]
    public void GetRandomGreeting_ReplacesPokeChatLiteral()
    {
        using var db = new FreshDbContext();
        db.Context.Greetings.Add(new Greeting
        {
            Text = "Hello! I'm PokeChat. What's your name?",
            IsSystem = true,
            CreatedAt = DateTime.UtcNow.ToString("O")
        });
        db.Context.SaveChanges();

        var store = new KnowledgeStore(db.Context);
        var greeting = GreetingPool.GetRandomGreeting(store, "Nova");
        greeting.ShouldBe("Hello! I'm Nova. What's your name?");
    }
}

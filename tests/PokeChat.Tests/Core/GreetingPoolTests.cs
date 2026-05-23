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
        var greeting = GreetingPool.GetRandomGreeting(store);
        greeting.ShouldBe("Hello!");
    }

    [Fact]
    public void GetRandomGreeting_ReturnsFallback_WhenNoGreetings()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);
        var greeting = GreetingPool.GetRandomGreeting(store);
        greeting.ShouldBe("Hello! I'm PokeChat. What's your name?");
    }
}

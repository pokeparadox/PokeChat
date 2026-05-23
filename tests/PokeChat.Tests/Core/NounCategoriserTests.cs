using PokeChat.Core;
using PokeChat.Knowledge;
using PokeChat.Tests.Helpers;
using Shouldly;

namespace PokeChat.Tests.Core;

public class NounCategoriserTests
{
    [Fact]
    public void CategoriseNoun_KnownInDb_ReturnsCategory()
    {
        using var db = new FreshDbContext();
        db.Context.NounCategories.Add(new()
        {
            Noun = "london",
            Category = "place",
            CreatedAt = DateTime.UtcNow.ToString("O")
        });
        db.Context.SaveChanges();

        var store = new KnowledgeStore(db.Context);
        var categoriser = new NounCategoriser(store);
        categoriser.CategoriseNoun("london").ShouldBe("place");
    }

    [Fact]
    public void CategoriseNoun_CommonName_InfersPerson()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);
        var categoriser = new NounCategoriser(store);
        categoriser.CategoriseNoun("alice").ShouldBe("person");
    }

    [Fact]
    public void CategoriseNoun_PlaceSuffix_InfersPlace()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);
        var categoriser = new NounCategoriser(store);
        categoriser.CategoriseNoun("riverton").ShouldBe("place");
    }

    [Fact]
    public void CategoriseNoun_Unknown_DefaultsToThing()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);
        var categoriser = new NounCategoriser(store);
        categoriser.CategoriseNoun("guitar").ShouldBe("thing");
    }

    [Fact]
    public void CategoriseNoun_AutoLearns_UncategorisedNoun()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);
        var categoriser = new NounCategoriser(store);

        categoriser.CategoriseNoun("guitar").ShouldBe("thing");
        store.CategoriseNoun("guitar").ShouldBe("thing");
    }

    [Fact]
    public void CategoriseNoun_MultipleLookups_ReturnsSame()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);
        var categoriser = new NounCategoriser(store);

        var first = categoriser.CategoriseNoun("guitar");
        var second = categoriser.CategoriseNoun("guitar");
        first.ShouldBe(second);
    }
}

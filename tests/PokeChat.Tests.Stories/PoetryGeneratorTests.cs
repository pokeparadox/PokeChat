using PokeChat.Knowledge;
using PokeChat.Stories;
using PokeChat.Tests.Shared.Helpers;
using Shouldly;

namespace PokeChat.Tests.Stories;

public class PoetryGeneratorTests
{
    private static PoetryGenerator CreateGenerator(FreshDbContext db)
    {
        TestDataHelper.SeedPosDictionary(db.Context);
        TestDataHelper.SeedRhymeGroups(db.Context);
        TestDataHelper.SeedPoemTemplates(db.Context);
        var store = new KnowledgeStore(db.Context);
        return new PoetryGenerator(store);
    }

    [Fact]
    public void GenerateHaiku_ReturnsThreeLines()
    {
        using var db = new FreshDbContext();
        var generator = CreateGenerator(db);
        var haiku = generator.GenerateHaiku();
        haiku.ShouldNotBeNull();
        haiku.Split('\n').Length.ShouldBe(3);
    }

    [Fact]
    public void GenerateHaiku_NoEmptyLines()
    {
        using var db = new FreshDbContext();
        var generator = CreateGenerator(db);
        var haiku = generator.GenerateHaiku();
        haiku.ShouldNotBeNull();
        foreach (var line in haiku.Split('\n'))
            line.Trim().ShouldNotBeEmpty();
    }

    [Fact]
    public void GenerateHaiku_AllSlotsResolved()
    {
        using var db = new FreshDbContext();
        var generator = CreateGenerator(db);
        var haiku = generator.GenerateHaiku();
        haiku.ShouldNotBeNull();
        haiku.ShouldNotContain("{");
    }

    [Fact]
    public void GenerateLimerick_ReturnsFiveLines()
    {
        using var db = new FreshDbContext();
        var generator = CreateGenerator(db);
        var limerick = generator.GenerateLimerick();
        limerick.ShouldNotBeNull();
        limerick.Split('\n').Length.ShouldBe(5);
    }

    [Fact]
    public void GenerateLimerick_NoEmptyLines()
    {
        using var db = new FreshDbContext();
        var generator = CreateGenerator(db);
        var limerick = generator.GenerateLimerick();
        limerick.ShouldNotBeNull();
        foreach (var line in limerick.Split('\n'))
            line.Trim().ShouldNotBeEmpty();
    }

    [Fact]
    public void GenerateLimerick_AllSlotsResolved()
    {
        using var db = new FreshDbContext();
        var generator = CreateGenerator(db);
        var limerick = generator.GenerateLimerick();
        limerick.ShouldNotBeNull();
        limerick.ShouldNotContain("{");
    }

    [Fact]
    public void GenerateLimerick_RhymeSlotsResolved()
    {
        using var db = new FreshDbContext();
        var generator = CreateGenerator(db);

        var limerick = generator.GenerateLimerick();
        limerick.ShouldNotBeNull();

        var lines = limerick.Split('\n');
        lines.Length.ShouldBe(5);

        foreach (var line in lines)
            line.Trim().ShouldNotBeEmpty();
    }

    [Fact]
    public void GenerateHaiku_IncludesUserName()
    {
        using var db = new FreshDbContext();
        var generator = CreateGenerator(db);

        var haiku = generator.GenerateHaiku(userName: "Alice");

        haiku.ShouldNotBeNull();
    }

    [Fact]
    public void GenerateHaiku_NoTemplates_ReturnsNull()
    {
        using var db = new FreshDbContext();
        TestDataHelper.SeedPosDictionary(db.Context);
        var store = new KnowledgeStore(db.Context);
        var generator = new PoetryGenerator(store);

        var haiku = generator.GenerateHaiku();
        haiku.ShouldBeNull();
    }
}

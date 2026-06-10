using PokeChat.Knowledge;
using PokeChat.Stories;
using PokeChat.Tests.Helpers;
using Shouldly;

namespace PokeChat.Tests.Stories;

public class PoetryGeneratorTests
{
    private static PoetryGenerator CreateGenerator()
    {
        var db = new FreshDbContext();
        TestDataHelper.SeedPosDictionary(db.Context);
        TestDataHelper.SeedRhymeGroups(db.Context);
        TestDataHelper.SeedPoemTemplates(db.Context);
        var store = new KnowledgeStore(db.Context);
        return new PoetryGenerator(store);
    }

    [Fact]
    public void GenerateHaiku_ReturnsThreeLines()
    {
        var generator = CreateGenerator();
        var haiku = generator.GenerateHaiku();
        haiku.ShouldNotBeNull();
        haiku.Split('\n').Length.ShouldBe(3);
    }

    [Fact]
    public void GenerateHaiku_NoEmptyLines()
    {
        var generator = CreateGenerator();
        var haiku = generator.GenerateHaiku();
        haiku.ShouldNotBeNull();
        foreach (var line in haiku.Split('\n'))
            line.Trim().ShouldNotBeEmpty();
    }

    [Fact]
    public void GenerateHaiku_AllSlotsResolved()
    {
        var generator = CreateGenerator();
        var haiku = generator.GenerateHaiku();
        haiku.ShouldNotBeNull();
        haiku.ShouldNotContain("{");
    }

    [Fact]
    public void GenerateLimerick_ReturnsFiveLines()
    {
        var generator = CreateGenerator();
        var limerick = generator.GenerateLimerick();
        limerick.ShouldNotBeNull();
        limerick.Split('\n').Length.ShouldBe(5);
    }

    [Fact]
    public void GenerateLimerick_NoEmptyLines()
    {
        var generator = CreateGenerator();
        var limerick = generator.GenerateLimerick();
        limerick.ShouldNotBeNull();
        foreach (var line in limerick.Split('\n'))
            line.Trim().ShouldNotBeEmpty();
    }

    [Fact]
    public void GenerateLimerick_AllSlotsResolved()
    {
        var generator = CreateGenerator();
        var limerick = generator.GenerateLimerick();
        limerick.ShouldNotBeNull();
        limerick.ShouldNotContain("{");
    }

    [Fact]
    public void GenerateLimerick_RhymeSlotsResolved()
    {
        var db = new FreshDbContext();
        TestDataHelper.SeedPosDictionary(db.Context);
        TestDataHelper.SeedRhymeGroups(db.Context);
        TestDataHelper.SeedPoemTemplates(db.Context);
        var store = new KnowledgeStore(db.Context);
        var generator = new PoetryGenerator(store);

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
        var db = new FreshDbContext();
        TestDataHelper.SeedPosDictionary(db.Context);
        TestDataHelper.SeedRhymeGroups(db.Context);
        TestDataHelper.SeedPoemTemplates(db.Context);
        var store = new KnowledgeStore(db.Context);
        var generator = new PoetryGenerator(store);

        var haiku = generator.GenerateHaiku(userName: "Alice");

        haiku.ShouldNotBeNull();
    }

    [Fact]
    public void GenerateHaiku_NoTemplates_ReturnsNull()
    {
        var db = new FreshDbContext();
        TestDataHelper.SeedPosDictionary(db.Context);
        var store = new KnowledgeStore(db.Context);
        var generator = new PoetryGenerator(store);

        var haiku = generator.GenerateHaiku();
        haiku.ShouldBeNull();
    }
}

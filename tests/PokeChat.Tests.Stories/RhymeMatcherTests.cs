using PokeChat.Knowledge;
using PokeChat.Stories;
using PokeChat.Tests.Shared.Helpers;
using Shouldly;

namespace PokeChat.Tests.Stories;

public class RhymeMatcherTests
{
    private static KnowledgeStore CreateStoreWithRhymes()
    {
        using var db = new FreshDbContext();
        TestDataHelper.SeedRhymeGroups(db.Context);
        return new KnowledgeStore(db.Context);
    }

    [Fact]
    public void ExtractRhymeKey_ReturnsLastVowelGroup()
    {
        var matcher = new RhymeMatcher(CreateStoreWithRhymes());
        matcher.ExtractRhymeKey("cat").ShouldBe("at");
    }

    [Fact]
    public void ExtractRhymeKey_HandlesLongVowel()
    {
        var matcher = new RhymeMatcher(CreateStoreWithRhymes());
        matcher.ExtractRhymeKey("cake").ShouldBe("ake");
    }

    [Fact]
    public void ExtractRhymeKey_HandlesMultipleVowels()
    {
        var matcher = new RhymeMatcher(CreateStoreWithRhymes());
        matcher.ExtractRhymeKey("night").ShouldBe("ight");
    }

    [Fact]
    public void ExtractRhymeKey_SingleLetter_ReturnsIt()
    {
        var matcher = new RhymeMatcher(CreateStoreWithRhymes());
        matcher.ExtractRhymeKey("a").ShouldBe("a");
    }

    [Fact]
    public void FindRhymeWord_ReturnsMatchingWord()
    {
        using var db = new FreshDbContext();
        TestDataHelper.SeedRhymeGroups(db.Context);
        var store = new KnowledgeStore(db.Context);
        var matcher = new RhymeMatcher(store);

        var rhyme = matcher.FindRhymeWord("cat", "noun");

        rhyme.ShouldNotBeNull();
        rhyme.ShouldNotBe("cat");
    }

    [Fact]
    public void FindRhymeWord_RespectsSyllableCount()
    {
        using var db = new FreshDbContext();
        TestDataHelper.SeedRhymeGroups(db.Context);
        var store = new KnowledgeStore(db.Context);
        var matcher = new RhymeMatcher(store);

        var rhyme = matcher.FindRhymeWord("cat", "noun", 1);

        rhyme.ShouldNotBeNull();
        SyllableCounter.Count(rhyme!).ShouldBe(1);
    }

    [Fact]
    public void FindRhymeWord_NoMatch_ReturnsNull()
    {
        using var db = new FreshDbContext();
        TestDataHelper.SeedRhymeGroups(db.Context);
        var store = new KnowledgeStore(db.Context);
        var matcher = new RhymeMatcher(store);

        var rhyme = matcher.FindRhymeWord("xylophone", "noun");

        rhyme.ShouldBeNull();
    }

    [Fact]
    public void FindRhymeWord_DoesNotReturnSelf()
    {
        using var db = new FreshDbContext();
        TestDataHelper.SeedRhymeGroups(db.Context);
        var store = new KnowledgeStore(db.Context);
        var matcher = new RhymeMatcher(store);

        var result = matcher.FindRhymeWord("cat", "noun");
        result.ShouldNotBe("cat");
    }
}

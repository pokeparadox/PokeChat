using PokeChat.NLP;
using Shouldly;

namespace PokeChat.Tests.NLP;

public class SvoExtractorTests
{
    [Fact]
    public void Extract_SimpleSvo()
    {
        var tokens = new List<string> { "cat", "chased", "mouse" };
        var tags = new Dictionary<string, PosTag>
        {
            ["cat"] = PosTag.Noun,
            ["chased"] = PosTag.Verb,
            ["mouse"] = PosTag.Noun,
        };

        var result = SvoExtractor.Extract(tokens, tags);
        result.ShouldHaveSingleItem();
        result[0].Subject.ShouldBe("cat");
        result[0].Verb.ShouldBe("chased");
        result[0].Object.ShouldBe("mouse");
    }

    [Fact]
    public void Extract_NoVerb_ReturnsEmpty()
    {
        var tokens = new List<string> { "the", "cat" };
        var tags = new Dictionary<string, PosTag>
        {
            ["the"] = PosTag.Determiner,
            ["cat"] = PosTag.Noun,
        };

        var result = SvoExtractor.Extract(tokens, tags);
        result.ShouldBeEmpty();
    }

    [Fact]
    public void Extract_MultipleVerbs_ExtractsAll()
    {
        var tokens = new List<string> { "i", "like", "pizza", "and", "hate", "broccoli" };
        var tags = new Dictionary<string, PosTag>
        {
            ["i"] = PosTag.Pronoun,
            ["like"] = PosTag.Verb,
            ["pizza"] = PosTag.Noun,
            ["and"] = PosTag.Conjunction,
            ["hate"] = PosTag.Verb,
            ["broccoli"] = PosTag.Noun,
        };

        var result = SvoExtractor.Extract(tokens, tags);
        result.Count.ShouldBe(2);
        result[0].Subject.ShouldBe("i");
        result[0].Verb.ShouldBe("like");
        result[0].Object.ShouldBe("pizza and");

        // Second verb subject picks up from last Verb backwards: includes "pizza and"
        result[1].Subject.ShouldBe("pizza and");
        result[1].Verb.ShouldBe("hate");
        result[1].Object.ShouldBe("broccoli");
    }

    [Fact]
    public void Extract_SubjectHasMultipleWords()
    {
        var tokens = new List<string> { "the", "big", "cat", "chased", "mouse" };
        var tags = new Dictionary<string, PosTag>
        {
            ["the"] = PosTag.Determiner,
            ["big"] = PosTag.Adjective,
            ["cat"] = PosTag.Noun,
            ["chased"] = PosTag.Verb,
            ["mouse"] = PosTag.Noun,
        };

        var result = SvoExtractor.Extract(tokens, tags);
        result.ShouldHaveSingleItem();
        result[0].Subject.ShouldBe("the big cat");
        result[0].Verb.ShouldBe("chased");
        result[0].Object.ShouldBe("mouse");
    }

    [Fact]
    public void Extract_StopsAtPunctuation()
    {
        var tokens = new List<string> { "hello", ",", "world", "is", "fun" };
        var tags = new Dictionary<string, PosTag>
        {
            ["hello"] = PosTag.Noun,
            [","] = PosTag.Punctuation,
            ["world"] = PosTag.Noun,
            ["is"] = PosTag.Verb,
            ["fun"] = PosTag.Adjective,
        };

        var result = SvoExtractor.Extract(tokens, tags);
        result.ShouldHaveSingleItem();
        result[0].Subject.ShouldBe("world");
        result[0].Verb.ShouldBe("is");
        result[0].Object.ShouldBe("fun");
    }

    [Fact]
    public void Extract_ObjectHasMultipleWords()
    {
        var tokens = new List<string> { "i", "read", "a", "good", "book" };
        var tags = new Dictionary<string, PosTag>
        {
            ["i"] = PosTag.Pronoun,
            ["read"] = PosTag.Verb,
            ["a"] = PosTag.Determiner,
            ["good"] = PosTag.Adjective,
            ["book"] = PosTag.Noun,
        };

        var result = SvoExtractor.Extract(tokens, tags);
        result.ShouldHaveSingleItem();
        result[0].Subject.ShouldBe("i");
        result[0].Verb.ShouldBe("read");
        result[0].Object.ShouldBe("a good book");
    }
}

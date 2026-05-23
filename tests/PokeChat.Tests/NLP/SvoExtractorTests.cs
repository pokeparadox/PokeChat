using PokeChat.NLP;
using Shouldly;

namespace PokeChat.Tests.NLP;

public class SvoExtractorTests
{
    private static readonly SvoExtractor Extractor = new();

    [Fact]
    public void Extract_SimpleSvo()
    {
        var tokens = new List<string> { "cat", "chased", "mouse" };
        var tags = new List<PosTag> { PosTag.Noun, PosTag.Verb, PosTag.Noun };

        var result = Extractor.Extract(tokens, tags);
        result.ShouldHaveSingleItem();
        result[0].Subject.ShouldBe("cat");
        result[0].Verb.ShouldBe("chased");
        result[0].Object.ShouldBe("mouse");
    }

    [Fact]
    public void Extract_NoVerb_ReturnsEmpty()
    {
        var tokens = new List<string> { "the", "cat" };
        var tags = new List<PosTag> { PosTag.Determiner, PosTag.Noun };

        var result = Extractor.Extract(tokens, tags);
        result.ShouldBeEmpty();
    }

    [Fact]
    public void Extract_MultipleVerbs_ExtractsAll()
    {
        var tokens = new List<string> { "i", "like", "pizza", "and", "hate", "broccoli" };
        var tags = new List<PosTag>
        {
            PosTag.Pronoun, PosTag.Verb, PosTag.Noun,
            PosTag.Conjunction, PosTag.Verb, PosTag.Noun
        };

        var result = Extractor.Extract(tokens, tags);
        result.Count.ShouldBe(2);
        result[0].Subject.ShouldBe("i");
        result[0].Verb.ShouldBe("like");
        result[0].Object.ShouldBe("pizza and");

        result[1].Subject.ShouldBe("pizza and");
        result[1].Verb.ShouldBe("hate");
        result[1].Object.ShouldBe("broccoli");
    }

    [Fact]
    public void Extract_SubjectHasMultipleWords()
    {
        var tokens = new List<string> { "the", "big", "cat", "chased", "mouse" };
        var tags = new List<PosTag>
        {
            PosTag.Determiner, PosTag.Adjective, PosTag.Noun,
            PosTag.Verb, PosTag.Noun
        };

        var result = Extractor.Extract(tokens, tags);
        result.ShouldHaveSingleItem();
        result[0].Subject.ShouldBe("the big cat");
        result[0].Verb.ShouldBe("chased");
        result[0].Object.ShouldBe("mouse");
    }

    [Fact]
    public void Extract_StopsAtPunctuation()
    {
        var tokens = new List<string> { "hello", ",", "world", "is", "fun" };
        var tags = new List<PosTag>
        {
            PosTag.Noun, PosTag.Punctuation, PosTag.Noun,
            PosTag.Verb, PosTag.Adjective
        };

        var result = Extractor.Extract(tokens, tags);
        result.ShouldHaveSingleItem();
        result[0].Subject.ShouldBe("world");
        result[0].Verb.ShouldBe("is");
        result[0].Object.ShouldBe("fun");
    }

    [Fact]
    public void Extract_ObjectHasMultipleWords()
    {
        var tokens = new List<string> { "i", "read", "a", "good", "book" };
        var tags = new List<PosTag>
        {
            PosTag.Pronoun, PosTag.Verb, PosTag.Determiner,
            PosTag.Adjective, PosTag.Noun
        };

        var result = Extractor.Extract(tokens, tags);
        result.ShouldHaveSingleItem();
        result[0].Subject.ShouldBe("i");
        result[0].Verb.ShouldBe("read");
        result[0].Object.ShouldBe("a good book");
    }
}

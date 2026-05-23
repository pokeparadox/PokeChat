using PokeChat.NLP;
using Shouldly;

namespace PokeChat.Tests.NLP;

public class SentenceSplitterTests
{
    private static readonly SentenceSplitter Splitter = new();

    [Fact]
    public void Split_SingleSentence_ReturnsOne()
    {
        var result = Splitter.Split("Hello world.");
        result.ShouldBe(new[] { "Hello world." });
    }

    [Fact]
    public void Split_MultipleSentences_ReturnsAll()
    {
        var result = Splitter.Split("Hello. How are you? I'm fine!");
        result.ShouldBe(new[] { "Hello.", "How are you?", "I'm fine!" });
    }

    [Fact]
    public void Split_HandlesUnknownAbbreviation()
    {
        var result = Splitter.Split("Dr. Smith is here. He is a doctor.");
        result.ShouldBe(new[] { "Dr. Smith is here.", "He is a doctor." });
    }

    [Fact]
    public void Split_EmptyString_ReturnsEmpty()
    {
        var result = Splitter.Split("");
        result.ShouldBeEmpty();
    }

    [Fact]
    public void Split_NoPunctuation_ReturnsSingle()
    {
        var result = Splitter.Split("Hello world");
        result.ShouldBe(new[] { "Hello world" });
    }
}

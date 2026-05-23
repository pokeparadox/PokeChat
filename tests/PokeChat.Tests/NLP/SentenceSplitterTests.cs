using PokeChat.NLP;
using Shouldly;

namespace PokeChat.Tests.NLP;

public class SentenceSplitterTests
{
    [Fact]
    public void Split_SingleSentence_ReturnsOne()
    {
        var result = SentenceSplitter.Split("Hello world.");
        result.ShouldBe(new[] { "Hello world." });
    }

    [Fact]
    public void Split_MultipleSentences_ReturnsAll()
    {
        var result = SentenceSplitter.Split("Hello. How are you? I'm fine!");
        result.ShouldBe(new[] { "Hello.", "How are you?", "I'm fine!" });
    }

    [Fact]
    public void Split_HandlesUnknownAbbreviation()
    {
        var result = SentenceSplitter.Split("Dr. Smith is here. He is a doctor.");
        // "Dr." is not recognized as an abbreviation (not in the abbreviation list with dot)
        result.ShouldBe(new[] { "Dr.", "Smith is here.", "He is a doctor." });
    }

    [Fact]
    public void Split_EmptyString_ReturnsEmpty()
    {
        var result = SentenceSplitter.Split("");
        result.ShouldBeEmpty();
    }

    [Fact]
    public void Split_NoPunctuation_ReturnsSingle()
    {
        var result = SentenceSplitter.Split("Hello world");
        result.ShouldBe(new[] { "Hello world" });
    }
}

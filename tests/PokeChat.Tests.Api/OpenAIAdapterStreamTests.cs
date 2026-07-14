using PokeChat.Api.Services;
using Shouldly;

namespace PokeChat.Tests.Api;

public class OpenAIAdapterStreamTests
{
    [Fact]
    public void ChunkBySentences_SingleSentence_ReturnsOneChunk()
    {
        var result = OpenAIAdapter.ChunkBySentences("Hello world.");
        result.Count.ShouldBe(1);
        result[0].ShouldBe("Hello world.");
    }

    [Fact]
    public void ChunkBySentences_MultipleSentences_SplitsCorrectly()
    {
        var result = OpenAIAdapter.ChunkBySentences("Hello! How are you? Fine.");
        result.Count.ShouldBe(3);
        result[0].ShouldBe("Hello! ");
        result[1].ShouldBe("How are you? ");
        result[2].ShouldBe("Fine.");
    }

    [Fact]
    public void ChunkBySentences_NoPunctuation_ReturnsFullText()
    {
        var result = OpenAIAdapter.ChunkBySentences("No punctuation here");
        result.Count.ShouldBe(1);
        result[0].ShouldBe("No punctuation here");
    }

    [Fact]
    public void ChunkBySentences_EmptyString_ReturnsEmptyList()
    {
        var result = OpenAIAdapter.ChunkBySentences("");
        result.ShouldBeEmpty();
    }

    [Fact]
    public void ChunkBySentences_Null_ReturnsEmptyList()
    {
        var result = OpenAIAdapter.ChunkBySentences(null!);
        result.ShouldBeEmpty();
    }

    [Fact]
    public void ChunkBySentences_WhitespaceOnly_ReturnsOneChunk()
    {
        var result = OpenAIAdapter.ChunkBySentences("   ");
        result.Count.ShouldBe(1);
    }

    [Fact]
    public void ChunkBySentences_ExclamationAndQuestion_Splits()
    {
        var result = OpenAIAdapter.ChunkBySentences("Wow! Is that true? Yes.");
        result.Count.ShouldBe(3);
        result[0].ShouldBe("Wow! ");
        result[1].ShouldBe("Is that true? ");
        result[2].ShouldBe("Yes.");
    }

    [Fact]
    public void ChunkBySentences_SentenceWithoutTrailingSpace()
    {
        var result = OpenAIAdapter.ChunkBySentences("One two three.");
        result.Count.ShouldBe(1);
        result[0].ShouldBe("One two three.");
    }

    [Fact]
    public void ChunkBySentences_PreservesPunctuation()
    {
        var result = OpenAIAdapter.ChunkBySentences("Really? Yes!");
        result.Count.ShouldBe(2);
        result[0].ShouldBe("Really? ");
        result[1].ShouldBe("Yes!");
    }
}

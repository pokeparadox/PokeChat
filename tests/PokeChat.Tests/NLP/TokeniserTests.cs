using PokeChat.NLP;
using Shouldly;

namespace PokeChat.Tests.NLP;

public class TokeniserTests
{
    private readonly Tokeniser _tokeniser = new();

    [Fact]
    public void Tokenise_SplitsBasicSentence()
    {
        var result = _tokeniser.Tokenise("hello world");
        result.ShouldBe(new[] { "hello", "world" });
    }

    [Fact]
    public void Tokenise_LowercasesInput()
    {
        var result = _tokeniser.Tokenise("HELLO World");
        result.ShouldBe(new[] { "hello", "world" });
    }

    [Fact]
    public void Tokenise_SeparatesPunctuation()
    {
        var result = _tokeniser.Tokenise("hello, world!");
        result.ShouldBe(new[] { "hello", ",", "world", "!" });
    }

    [Fact]
    public void Tokenise_EmptyString_ReturnsEmpty()
    {
        var result = _tokeniser.Tokenise("");
        result.ShouldBeEmpty();
    }

    [Fact]
    public void Tokenise_WhitespaceString_ReturnsEmpty()
    {
        var result = _tokeniser.Tokenise("   ");
        result.ShouldBeEmpty();
    }

    [Fact]
    public void Tokenise_HandlesContractions()
    {
        var result = _tokeniser.Tokenise("don't");
        result.ShouldBe(new[] { "don't" });
    }

    [Fact]
    public void Tokenise_HandlesMultipleSpaces()
    {
        var result = _tokeniser.Tokenise("hello    world");
        result.ShouldBe(new[] { "hello", "world" });
    }
}

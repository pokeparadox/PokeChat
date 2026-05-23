using PokeChat.NLP;
using Shouldly;

namespace PokeChat.Tests.NLP;

public class TokenizerTests
{
    [Fact]
    public void Tokenize_SplitsBasicSentence()
    {
        var result = Tokenizer.Tokenize("hello world");
        result.ShouldBe(new[] { "hello", "world" });
    }

    [Fact]
    public void Tokenize_LowercasesInput()
    {
        var result = Tokenizer.Tokenize("HELLO World");
        result.ShouldBe(new[] { "hello", "world" });
    }

    [Fact]
    public void Tokenize_SeparatesPunctuation()
    {
        var result = Tokenizer.Tokenize("hello, world!");
        result.ShouldBe(new[] { "hello", ",", "world", "!" });
    }

    [Fact]
    public void Tokenize_EmptyString_ReturnsEmpty()
    {
        var result = Tokenizer.Tokenize("");
        result.ShouldBeEmpty();
    }

    [Fact]
    public void Tokenize_WhitespaceString_ReturnsEmpty()
    {
        var result = Tokenizer.Tokenize("   ");
        result.ShouldBeEmpty();
    }

    [Fact]
    public void Tokenize_HandlesContractions()
    {
        var result = Tokenizer.Tokenize("don't");
        result.ShouldBe(new[] { "don't" });
    }

    [Fact]
    public void Tokenize_HandlesMultipleSpaces()
    {
        var result = Tokenizer.Tokenize("hello    world");
        result.ShouldBe(new[] { "hello", "world" });
    }
}

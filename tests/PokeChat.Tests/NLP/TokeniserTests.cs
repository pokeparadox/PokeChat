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
    public void Tokenise_HandlesContractions_WithoutExpander()
    {
        var result = _tokeniser.Tokenise("don't");
        result.ShouldBe(new[] { "don't" });
    }

    [Fact]
    public void Tokenise_WithExpander_ExpandsContractions()
    {
        var expansions = new Dictionary<string, string> { { "don't", "do not" } };
        var expander = new ContractionExpander(expansions);
        var tokeniser = new Tokeniser(expander);
        var result = tokeniser.Tokenise("don't");
        result.ShouldBe(new[] { "do", "not" });
    }

    [Fact]
    public void Tokenise_WithExpander_MultipleContractions()
    {
        var expansions = new Dictionary<string, string>
        {
            { "i'm", "i am" },
            { "you're", "you are" },
            { "don't", "do not" }
        };
        var expander = new ContractionExpander(expansions);
        var tokeniser = new Tokeniser(expander);
        var result = tokeniser.Tokenise("I'm happy and you're not");
        result.ShouldBe(new[] { "i", "am", "happy", "and", "you", "are", "not" });
    }

    [Fact]
    public void Tokenise_HandlesMultipleSpaces()
    {
        var result = _tokeniser.Tokenise("hello    world");
        result.ShouldBe(new[] { "hello", "world" });
    }
}

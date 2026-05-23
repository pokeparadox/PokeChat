using PokeChat.NLP;
using Shouldly;

namespace PokeChat.Tests.NLP;

public class SpellCheckerTests
{
    private SpellChecker CreateChecker()
    {
        var dict = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "hello", "world", "the", "cat", "dog", "run", "running"
        };
        var misspellings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["teh"] = "the",
            ["recieve"] = "receive"
        };
        var checker = new SpellChecker();
        checker.Initialize(dict, misspellings);
        return checker;
    }

    [Fact]
    public void AutoCorrect_KnownMisspelling()
    {
        var checker = CreateChecker();
        var result = checker.AutoCorrect(["teh", "cat"]);
        result.ShouldBe(new[] { "the", "cat" });
    }

    [Fact]
    public void AutoCorrect_UnknownWord_Unchanged()
    {
        var checker = CreateChecker();
        var result = checker.AutoCorrect(["xyzzy"]);
        result.ShouldBe(new[] { "xyzzy" });
    }

    [Fact]
    public void GetUnknownWords_ReturnsUnknown()
    {
        var checker = CreateChecker();
        var result = checker.GetUnknownWords(["hello", "xyzzy", "cat"]);
        result.ShouldBe(new[] { "xyzzy" });
    }

    [Fact]
    public void GetUnknownWords_DigitsIgnored()
    {
        var checker = CreateChecker();
        var result = checker.GetUnknownWords(["hello", "123"]);
        result.ShouldBeEmpty();
    }

    [Fact]
    public void AddToDictionary_AddsWord()
    {
        var checker = CreateChecker();
        checker.AddToDictionary("newword");
        checker.GetUnknownWords(["newword"]).ShouldBeEmpty();
    }

    [Fact]
    public void HasSuggestions_ReturnsTrueForCloseWord()
    {
        var checker = CreateChecker();
        checker.HasSuggestions("helo").ShouldBeTrue();
    }

    [Fact]
    public void SuggestCorrections_ReturnsSuggestions()
    {
        var checker = CreateChecker();
        var suggestions = checker.SuggestCorrections("helo");
        suggestions.ShouldContain("hello");
    }

    [Fact]
    public void SuggestCorrections_NoMatch_ReturnsEmpty()
    {
        var checker = CreateChecker();
        var suggestions = checker.SuggestCorrections("abcdefghijklmnop");
        suggestions.ShouldBeEmpty();
    }
}

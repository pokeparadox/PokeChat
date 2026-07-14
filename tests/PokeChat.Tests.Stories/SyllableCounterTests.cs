using PokeChat.Stories;
using Shouldly;

namespace PokeChat.Tests.Stories;

public class SyllableCounterTests
{
    [Theory]
    [InlineData("cat", 1)]
    [InlineData("dog", 1)]
    [InlineData("the", 1)]
    [InlineData("are", 1)]
    [InlineData("fire", 1)]
    [InlineData("happy", 2)]
    [InlineData("hello", 2)]
    [InlineData("water", 2)]
    [InlineData("garden", 2)]
    [InlineData("silent", 2)]
    [InlineData("banana", 3)]
    [InlineData("elephant", 3)]
    [InlineData("beautiful", 3)]
    [InlineData("flower", 2)]
    [InlineData("power", 2)]
    [InlineData("idea", 3)]
    [InlineData("radio", 3)]
    [InlineData("area", 3)]
    [InlineData("serious", 3)]
    [InlineData("previous", 3)]
    [InlineData("mysterious", 3)]
    [InlineData("tion", 1)]
    [InlineData("nation", 2)]
    [InlineData("action", 2)]
    [InlineData("simple", 2)]
    [InlineData("table", 2)]
    [InlineData("castle", 2)]
    [InlineData("subtle", 2)]
    [InlineData("bottle", 2)]
    [InlineData("walked", 1)]
    [InlineData("jumped", 1)]
    [InlineData("landed", 2)]
    [InlineData("wanted", 2)]
    [InlineData("needed", 2)]
    [InlineData("based", 1)]
    [InlineData("danced", 1)]
    [InlineData("communism", 4)]
    [InlineData("capitalism", 5)]
    [InlineData("running", 2)]
    [InlineData("swimming", 2)]
    [InlineData("eating", 2)]
    [InlineData("word", 1)]
    [InlineData("syllable", 3)]
    [InlineData("count", 1)]
    [InlineData("algorithm", 3)]
    public void Count_ReturnsCorrectSyllables(string word, int expected)
    {
        SyllableCounter.Count(word).ShouldBe(expected);
    }

    [Fact]
    public void Count_EmptyString_ReturnsZero()
    {
        SyllableCounter.Count("").ShouldBe(0);
    }

    [Fact]
    public void Count_NullString_ReturnsZero()
    {
        SyllableCounter.Count(null!).ShouldBe(0);
    }

    [Fact]
    public void Count_Whitespace_ReturnsZero()
    {
        SyllableCounter.Count("   ").ShouldBe(0);
    }

    [Theory]
    [InlineData("Fire", 1)]
    [InlineData("WATER", 2)]
    [InlineData("ElEpHaNt", 3)]
    public void Count_IsCaseInsensitive(string word, int expected)
    {
        SyllableCounter.Count(word).ShouldBe(expected);
    }

    [Theory]
    [InlineData("one", 1)]
    [InlineData("two", 1)]
    [InlineData("three", 1)]
    [InlineData("four", 1)]
    [InlineData("five", 1)]
    [InlineData("six", 1)]
    [InlineData("seven", 2)]
    [InlineData("eight", 1)]
    [InlineData("nine", 1)]
    [InlineData("ten", 1)]
    [InlineData("eleven", 3)]
    [InlineData("twelve", 1)]
    [InlineData("hundred", 2)]
    [InlineData("thousand", 2)]
    public void Count_Numbers_ReturnsCorrectCount(string word, int expected)
    {
        SyllableCounter.Count(word).ShouldBe(expected);
    }

    [Theory]
    [InlineData("red", 1)]
    [InlineData("blue", 1)]
    [InlineData("green", 1)]
    [InlineData("yellow", 2)]
    [InlineData("orange", 2)]
    [InlineData("purple", 2)]
    [InlineData("brown", 1)]
    [InlineData("black", 1)]
    [InlineData("white", 1)]
    [InlineData("gray", 1)]
    public void Count_Colours_ReturnsCorrectCount(string word, int expected)
    {
        SyllableCounter.Count(word).ShouldBe(expected);
    }
}

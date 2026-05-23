using PokeChat.Data.Entities;
using PokeChat.NLP;
using Shouldly;

namespace PokeChat.Tests.NLP;

public class PosTaggerTests
{
    private readonly List<PosDictionaryEntry> _entries =
    [
        new() { Word = "run", WordType = "verb" },
        new() { Word = "the", WordType = "determiner" },
        new() { Word = "cat", WordType = "noun" },
        new() { Word = "quick", WordType = "adjective" },
        new() { Word = "in", WordType = "preposition" },
        new() { Word = "and", WordType = "conjunction" },
        new() { Word = "he", WordType = "pronoun" },
        new() { Word = "very", WordType = "adverb" },
    ];

    public PosTaggerTests()
    {
        PosTagger.Reset();
    }

    [Fact]
    public void Tag_KnownVerb()
    {
        PosTagger.Initialize(_entries);
        var result = PosTagger.Tag(["run"]);
        result["run"].ShouldBe(PosTag.Verb);
    }

    [Fact]
    public void Tag_KnownNoun()
    {
        PosTagger.Initialize(_entries);
        var result = PosTagger.Tag(["cat"]);
        result["cat"].ShouldBe(PosTag.Noun);
    }

    [Fact]
    public void Tag_UnknownWord_ReturnsUnknown()
    {
        PosTagger.Initialize(_entries);
        var result = PosTagger.Tag(["flurglebarg"]);
        result["flurglebarg"].ShouldBe(PosTag.Unknown);
    }

    [Fact]
    public void Tag_Punctuation()
    {
        PosTagger.Initialize(_entries);
        var result = PosTagger.Tag(["."]);
        result["."].ShouldBe(PosTag.Punctuation);
    }

    [Fact]
    public void Tag_VerbEndingInIng()
    {
        PosTagger.Initialize(_entries);
        var result = PosTagger.Tag(["running"]);
        result["running"].ShouldBe(PosTag.Verb);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        PosTagger.Initialize(_entries);
        PosTagger.Reset();
        var result = PosTagger.Tag(["run"]);
        result["run"].ShouldNotBe(PosTag.Verb);
    }
}

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

    [Fact]
    public void Tag_KnownVerb()
    {
        var tagger = new PosTagger(_entries);
        var result = tagger.Tag(["run"]);
        result[0].ShouldBe(PosTag.Verb);
    }

    [Fact]
    public void Tag_KnownNoun()
    {
        var tagger = new PosTagger(_entries);
        var result = tagger.Tag(["cat"]);
        result[0].ShouldBe(PosTag.Noun);
    }

    [Fact]
    public void Tag_UnknownWord_ReturnsUnknown()
    {
        var tagger = new PosTagger(_entries);
        var result = tagger.Tag(["flurglebarg"]);
        result[0].ShouldBe(PosTag.Unknown);
    }

    [Fact]
    public void Tag_Punctuation()
    {
        var tagger = new PosTagger(_entries);
        var result = tagger.Tag(["."]);
        result[0].ShouldBe(PosTag.Punctuation);
    }

    [Fact]
    public void Tag_VerbEndingInIng()
    {
        var tagger = new PosTagger(_entries);
        var result = tagger.Tag(["running"]);
        result[0].ShouldBe(PosTag.Verb);
    }

    [Fact]
    public void Tag_PluralNoun_TaggedAsNoun()
    {
        var tagger = new PosTagger(_entries);
        var result = tagger.Tag(["cats"]);
        result[0].ShouldBe(PosTag.Noun);
    }

    [Fact]
    public void Tag_PluralVerb_StillVerb()
    {
        var tagger = new PosTagger(_entries);
        var result = tagger.Tag(["runs"]);
        result[0].ShouldBe(PosTag.Verb);
    }
}

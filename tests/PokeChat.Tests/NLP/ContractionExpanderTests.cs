using PokeChat.NLP;
using Shouldly;

namespace PokeChat.Tests.NLP;

public class ContractionExpanderTests
{
    private static ContractionExpander CreateExpander()
    {
        var expansions = new Dictionary<string, string>
        {
            {"i'm", "i am"},
            {"you're", "you are"},
            {"he's", "he is"},
            {"she's", "she is"},
            {"it's", "it is"},
            {"we're", "we are"},
            {"they're", "they are"},
            {"i've", "i have"},
            {"you've", "you have"},
            {"we've", "we have"},
            {"they've", "they have"},
            {"i'll", "i will"},
            {"you'll", "you will"},
            {"he'll", "he will"},
            {"she'll", "she will"},
            {"it'll", "it will"},
            {"we'll", "we will"},
            {"they'll", "they will"},
            {"i'd", "i would"},
            {"you'd", "you would"},
            {"he'd", "he would"},
            {"she'd", "she would"},
            {"we'd", "we would"},
            {"they'd", "they would"},
            {"isn't", "is not"},
            {"aren't", "are not"},
            {"wasn't", "was not"},
            {"weren't", "were not"},
            {"don't", "do not"},
            {"doesn't", "does not"},
            {"didn't", "did not"},
            {"won't", "will not"},
            {"wouldn't", "would not"},
            {"can't", "cannot"},
            {"couldn't", "could not"},
            {"shouldn't", "should not"},
            {"mustn't", "must not"},
            {"needn't", "need not"},
            {"hasn't", "has not"},
            {"haven't", "have not"},
            {"hadn't", "had not"},
            {"let's", "let us"},
            {"gonna", "going to"},
            {"wanna", "want to"},
            {"gotta", "got to"},
        };
        return new ContractionExpander(expansions);
    }

    [Fact]
    public void Expand_ImContraction_ExpandsToIAm()
    {
        var expander = CreateExpander();
        expander.Expand("i'm happy").ShouldBe("i am happy");
    }

    [Fact]
    public void Expand_YoureContraction_ExpandsToYouAre()
    {
        var expander = CreateExpander();
        expander.Expand("you're nice").ShouldBe("you are nice");
    }

    [Fact]
    public void Expand_DontContraction_ExpandsToDoNot()
    {
        var expander = CreateExpander();
        expander.Expand("i don't know").ShouldBe("i do not know");
    }

    [Fact]
    public void Expand_CantContraction_ExpandsToCannot()
    {
        var expander = CreateExpander();
        expander.Expand("i can't do it").ShouldBe("i cannot do it");
    }

    [Fact]
    public void Expand_LetsContraction_ExpandsToLetUs()
    {
        var expander = CreateExpander();
        expander.Expand("let's go").ShouldBe("let us go");
    }

    [Fact]
    public void Expand_MultipleContractionsInOneSentence()
    {
        var expander = CreateExpander();
        expander.Expand("i'm happy and you're not").ShouldBe("i am happy and you are not");
    }

    [Fact]
    public void Expand_NoContraction_ReturnsInputUnchanged()
    {
        var expander = CreateExpander();
        expander.Expand("hello world").ShouldBe("hello world");
    }

    [Fact]
    public void Expand_EmptyString_ReturnsEmpty()
    {
        var expander = CreateExpander();
        expander.Expand("").ShouldBe("");
    }

    [Fact]
    public void Expand_CaseInsensitive()
    {
        var expander = CreateExpander();
        expander.Expand("I'm Happy").ShouldBe("i am Happy");
    }

    [Fact]
    public void Expand_ContractionAtStartOfSentence()
    {
        var expander = CreateExpander();
        expander.Expand("it's a nice day").ShouldBe("it is a nice day");
    }

    [Fact]
    public void Expand_IsntContraction_ExpandsToIsNot()
    {
        var expander = CreateExpander();
        expander.Expand("it isn't working").ShouldBe("it is not working");
    }
}

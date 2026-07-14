using PokeChat.Api.Services;
using Shouldly;

namespace PokeChat.Tests.Api;

public class SystemPromptMapperTests
{
    [Fact]
    public void Parse_NullPrompt_ReturnsNullPersona()
    {
        var result = SystemPromptMapper.Parse(null);
        result.Persona.ShouldBeNull();
        result.ResponseLength.ShouldBeNull();
    }

    [Fact]
    public void Parse_EmptyPrompt_ReturnsNullPersona()
    {
        var result = SystemPromptMapper.Parse("");
        result.Persona.ShouldBeNull();
        result.ResponseLength.ShouldBeNull();
    }

    [Fact]
    public void Parse_WhitespacePrompt_ReturnsNullPersona()
    {
        var result = SystemPromptMapper.Parse("   ");
        result.Persona.ShouldBeNull();
        result.ResponseLength.ShouldBeNull();
    }

    [Theory]
    [InlineData("You are a coding assistant.")]
    [InlineData("You write code for a living.")]
    [InlineData("You are a software engineer.")]
    [InlineData("You are a programmer helping with tasks.")]
    [InlineData("You are a developer.")]
    [InlineData("You are a code helper.")]
    [InlineData("You are a coding copilot.")]
    [InlineData("You are a pair programmer.")]
    public void Parse_CodingKeywords_DetectsCodingPersona(string prompt)
    {
        var result = SystemPromptMapper.Parse(prompt);
        result.Persona.ShouldBe("coding");
    }

    [Theory]
    [InlineData("You are a friendly chat companion.")]
    [InlineData("You are a conversational companion.")]
    [InlineData("You are a chatbot companion.")]
    public void Parse_ChatKeywords_DetectsChatPersona(string prompt)
    {
        var result = SystemPromptMapper.Parse(prompt);
        result.Persona.ShouldBe("chat");
    }

    [Fact]
    public void Parse_CodingKeyword_CaseInsensitive()
    {
        var result = SystemPromptMapper.Parse("You are a CODING ASSISTANT.");
        result.Persona.ShouldBe("coding");
    }

    [Fact]
    public void Parse_CodingInMiddleOfPrompt_DetectsCoding()
    {
        var result = SystemPromptMapper.Parse("You work in a monorepo. You are a coding assistant. Be helpful.");
        result.Persona.ShouldBe("coding");
    }

    [Fact]
    public void Parse_NoMatchingKeywords_ReturnsNullPersona()
    {
        var result = SystemPromptMapper.Parse("You are a helpful assistant. Be kind and polite.");
        result.Persona.ShouldBeNull();
    }

    [Theory]
    [InlineData("Be concise in your answers.")]
    [InlineData("Keep it short.")]
    [InlineData("Give short answers.")]
    [InlineData("Use brief responses.")]
    [InlineData("Be terse.")]
    public void Parse_ConciseKeywords_DetectsConcise(string prompt)
    {
        var result = SystemPromptMapper.Parse(prompt);
        result.ResponseLength.ShouldBe("concise");
    }

    [Theory]
    [InlineData("Be detailed in your explanations.")]
    [InlineData("Be thorough when answering.")]
    [InlineData("Give detailed responses.")]
    [InlineData("Explain in detail.")]
    public void Parse_DetailedKeywords_DetectsDetailed(string prompt)
    {
        var result = SystemPromptMapper.Parse(prompt);
        result.ResponseLength.ShouldBe("detailed");
    }

    [Fact]
    public void Parse_ConciseKeyword_CaseInsensitive()
    {
        var result = SystemPromptMapper.Parse("BE CONCISE");
        result.ResponseLength.ShouldBe("concise");
    }

    [Fact]
    public void Parse_BothPersonaAndConfig_DetectsBoth()
    {
        var result = SystemPromptMapper.Parse("You are a coding assistant. Be concise.");
        result.Persona.ShouldBe("coding");
        result.ResponseLength.ShouldBe("concise");
    }

    [Fact]
    public void Parse_ConciseTakesPrecedenceOverDetailed()
    {
        var result = SystemPromptMapper.Parse("Be concise and detailed.");
        result.ResponseLength.ShouldBe("concise");
    }

    [Fact]
    public void Parse_CodingTakesPrecedenceOverChat()
    {
        var result = SystemPromptMapper.Parse("You are a coding assistant and a friendly chat companion.");
        result.Persona.ShouldBe("coding");
    }

    [Fact]
    public void Parse_PreservesOriginalPrompt()
    {
        var prompt = "You are a coding assistant.";
        var result = SystemPromptMapper.Parse(prompt);
        result.OriginalPrompt.ShouldBe(prompt);
    }

    [Fact]
    public void Parse_LongSystemPrompt_DetectsAllKeywords()
    {
        var result = SystemPromptMapper.Parse(
            "You are a coding assistant working in a monorepo. " +
            "You write clean, well-tested code. " +
            "Be concise in your responses. " +
            "Use British English spelling.");
        result.Persona.ShouldBe("coding");
        result.ResponseLength.ShouldBe("concise");
    }

    [Fact]
    public void Parse_UnrelatedPrompt_ReturnsAllNull()
    {
        var result = SystemPromptMapper.Parse("The weather is nice today. Let's go for a walk.");
        result.Persona.ShouldBeNull();
        result.ResponseLength.ShouldBeNull();
    }
}

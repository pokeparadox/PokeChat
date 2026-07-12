using PokeChat.Api.Models;
using PokeChat.Api.Services;
using Shouldly;

namespace PokeChat.Tests.Api;

public class TitleGeneratorTests
{
    private readonly TitleGenerator _generator = new();

    [Fact]
    public void GenerateTitle_NullContent_ReturnsNewConversation()
    {
        var messages = new List<ChatMessage>
        {
            new() { Role = "user", Content = null }
        };
        var title = _generator.GenerateTitle(messages);
        title.ShouldBe("New Conversation");
    }

    [Fact]
    public void GenerateTitle_EmptyMessages_ReturnsNewConversation()
    {
        var messages = new List<ChatMessage>();
        var title = _generator.GenerateTitle(messages);
        title.ShouldBe("New Conversation");
    }

    [Fact]
    public void GenerateTitle_EmptyContent_ReturnsNewConversation()
    {
        var messages = new List<ChatMessage>
        {
            new() { Role = "user", Content = "" }
        };
        var title = _generator.GenerateTitle(messages);
        title.ShouldBe("New Conversation");
    }

    [Fact]
    public void GenerateTitle_DebuggingQuestion_ReturnsDebugCategory()
    {
        var messages = new List<ChatMessage>
        {
            new() { Role = "user", Content = "I'm getting a NullReferenceException in ChatEngine" }
        };
        var title = _generator.GenerateTitle(messages);
        title.ShouldContain("Debugging");
    }

    [Fact]
    public void GenerateTitle_PlanningQuestion_ReturnsPlanningCategory()
    {
        var messages = new List<ChatMessage>
        {
            new() { Role = "user", Content = "Help me decide what to do with PokeChat" }
        };
        var title = _generator.GenerateTitle(messages);
        title.ShouldContain("Planning");
    }

    [Fact]
    public void GenerateTitle_Question_ReturnsQuestionCategory()
    {
        var messages = new List<ChatMessage>
        {
            new() { Role = "user", Content = "What does the /v1/chat/completions endpoint return?" }
        };
        var title = _generator.GenerateTitle(messages);
        title.ShouldContain("Question");
    }

    [Fact]
    public void GenerateTitle_CodeReview_ReturnsCodeReviewCategory()
    {
        var messages = new List<ChatMessage>
        {
            new() { Role = "user", Content = "Can you review my implementation?" }
        };
        var title = _generator.GenerateTitle(messages);
        title.ShouldContain("Code Review");
    }

    [Fact]
    public void GenerateTitle_Brainstorm_ReturnsBrainstormCategory()
    {
        var messages = new List<ChatMessage>
        {
            new() { Role = "user", Content = "I have an idea for a new feature" }
        };
        var title = _generator.GenerateTitle(messages);
        title.ShouldContain("Brainstorm");
    }

    [Fact]
    public void GenerateTitle_FeatureRequest_ReturnsFeatureCategory()
    {
        var messages = new List<ChatMessage>
        {
            new() { Role = "user", Content = "Implement a dark mode toggle" }
        };
        var title = _generator.GenerateTitle(messages);
        title.ShouldContain("Feature");
    }

    [Fact]
    public void GenerateTitle_SetupQuestion_ReturnsSetupCategory()
    {
        var messages = new List<ChatMessage>
        {
            new() { Role = "user", Content = "How do I configure the database connection?" }
        };
        var title = _generator.GenerateTitle(messages);
        title.ShouldContain("Setup");
    }

    [Fact]
    public void GenerateTitle_Testing_ReturnsTestingCategory()
    {
        var messages = new List<ChatMessage>
        {
            new() { Role = "user", Content = "I need to write unit tests for the NLP pipeline" }
        };
        var title = _generator.GenerateTitle(messages);
        title.ShouldContain("Testing");
    }

    [Fact]
    public void GenerateTitle_GeneralChat_ReturnsChatCategory()
    {
        var messages = new List<ChatMessage>
        {
            new() { Role = "user", Content = "How are you today?" }
        };
        var title = _generator.GenerateTitle(messages);
        title.ShouldContain("Chat");
    }

    [Fact]
    public void GenerateTitle_UsesLastUserMessage()
    {
        var messages = new List<ChatMessage>
        {
            new() { Role = "user", Content = "Hi there" },
            new() { Role = "assistant", Content = "Hello!" },
            new() { Role = "user", Content = "I'm getting an error in the API" },
        };
        var title = _generator.GenerateTitle(messages);
        title.ShouldContain("Debugging");
    }

    [Fact]
    public void GenerateTitle_OnlyAssistantMessages_ReturnsNewConversation()
    {
        var messages = new List<ChatMessage>
        {
            new() { Role = "assistant", Content = "Hello! How can I help?" },
            new() { Role = "assistant", Content = "Let me know if you need anything" },
        };
        var title = _generator.GenerateTitle(messages);
        title.ShouldBe("New Conversation");
    }

    [Fact]
    public void GenerateTitle_DebugWithSubject_ExtractsMeaningfulSubject()
    {
        var messages = new List<ChatMessage>
        {
            new() { Role = "user", Content = "There's a bug in the authentication module" }
        };
        var title = _generator.GenerateTitle(messages);
        title.ShouldBe("Authentication module Debugging");
    }

    [Fact]
    public void GenerateTitle_ShortInput_UsesLastWord()
    {
        var messages = new List<ChatMessage>
        {
            new() { Role = "user", Content = "Hello world" }
        };
        var title = _generator.GenerateTitle(messages);
        title.ShouldBe("World Chat");
    }

    [Fact]
    public void GenerateTitle_CompoundWordException_DetectsDebugging()
    {
        var messages = new List<ChatMessage>
        {
            new() { Role = "user", Content = "There is an InvalidOperationException in the data layer" }
        };
        var title = _generator.GenerateTitle(messages);
        title.ShouldContain("Debugging");
    }

    [Fact]
    public void GenerateTitle_FeatureRefactor_ReturnsFeatureCategory()
    {
        var messages = new List<ChatMessage>
        {
            new() { Role = "user", Content = "Refactor the API routing layer" }
        };
        var title = _generator.GenerateTitle(messages);
        title.ShouldContain("Feature");
    }
}

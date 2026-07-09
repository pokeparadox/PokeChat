using PokeChat.Core;
using PokeChat.ML;
using Shouldly;

namespace PokeChat.Tests.Core;

public class RouterServiceTests
{
    private readonly RouterService _router = new();

    [Fact]
    public void Route_NullInput_ReturnsNone()
    {
        var result = _router.Route(null!);
        result.Handler.ShouldBe(RouteHandler.None);
    }

    [Fact]
    public void Route_EmptyInput_ReturnsNone()
    {
        var result = _router.Route("");
        result.Handler.ShouldBe(RouteHandler.None);
    }

    [Fact]
    public void Route_NoSlash_ReturnsNone()
    {
        var result = _router.Route("hello");
        result.Handler.ShouldBe(RouteHandler.None);
    }

    [Fact]
    public void Route_SingleSlash_ReturnsNone()
    {
        var result = _router.Route("/");
        result.Handler.ShouldBe(RouteHandler.None);
    }

    [Fact]
    public void Route_SlashMaths_ReturnsMathHandler()
    {
        var result = _router.Route("/maths 2 + 2");
        result.Handler.ShouldBe(RouteHandler.Math);
        result.Argument.ShouldBe("2 + 2");
        result.IsSlashCommand.ShouldBeTrue();
    }

    [Fact]
    public void Route_SlashMath_ReturnsMathHandler()
    {
        var result = _router.Route("/math 2+2");
        result.Handler.ShouldBe(RouteHandler.Math);
        result.Argument.ShouldBe("2+2");
        result.IsSlashCommand.ShouldBeTrue();
    }

    [Fact]
    public void Route_SlashRemind_ReturnsRemindHandler()
    {
        var result = _router.Route("/remind me to take out the rubbish at 5pm");
        result.Handler.ShouldBe(RouteHandler.Remind);
        result.Argument.ShouldBe("me to take out the rubbish at 5pm");
        result.IsSlashCommand.ShouldBeTrue();
    }

    [Fact]
    public void Route_SlashRemindNoArgs_ReturnsRemindHandler()
    {
        var result = _router.Route("/remind");
        result.Handler.ShouldBe(RouteHandler.Remind);
        result.Argument.ShouldBeNull();
    }

    [Theory]
    [InlineData("/story", RouteHandler.Story)]
    [InlineData("/poem", RouteHandler.Poem)]
    [InlineData("/haiku", RouteHandler.Haiku)]
    [InlineData("/limerick", RouteHandler.Limerick)]
    [InlineData("/joke", RouteHandler.Joke)]
    [InlineData("/riddle", RouteHandler.Riddle)]
    [InlineData("/quiz", RouteHandler.Quiz)]
    [InlineData("/game", RouteHandler.Game)]
    [InlineData("/hangman", RouteHandler.Hangman)]
    [InlineData("/stats", RouteHandler.Stats)]
    [InlineData("/about", RouteHandler.AboutMe)]
    [InlineData("/reset", RouteHandler.Reset)]
    [InlineData("/help", RouteHandler.Help)]
    public void Route_SlashCommand_ReturnsCorrectHandler(string input, RouteHandler expected)
    {
        var result = _router.Route(input);
        result.Handler.ShouldBe(expected);
        result.IsSlashCommand.ShouldBeTrue();
    }

    [Theory]
    [InlineData("/switch coding", "coding")]
    [InlineData("/switch chat", "chat")]
    [InlineData("/switch", null)]
    public void Route_SlashSwitch_ReturnsSwitchHandlerWithArgument(string input, string? expectedArg)
    {
        var result = _router.Route(input);
        result.Handler.ShouldBe(RouteHandler.SwitchPersona);
        result.Argument.ShouldBe(expectedArg);
        result.IsSlashCommand.ShouldBeTrue();
    }

    [Fact]
    public void Route_SlashUnknown_ReturnsNone()
    {
        var result = _router.Route("/xyzzy");
        result.Handler.ShouldBe(RouteHandler.None);
    }

    [Fact]
    public void Route_OriginalInputPreserved()
    {
        var result = _router.Route("/joke");
        result.OriginalInput.ShouldBe("/joke");
    }

    [Fact]
    public void Route_CaseInsensitive()
    {
        var result = _router.Route("/MATH 42");
        result.Handler.ShouldBe(RouteHandler.Math);
        result.Argument.ShouldBe("42");
    }

    [Fact]
    public void Route_SlashWithLeadingWhitespace_ReturnsNone()
    {
        var result = _router.Route(" /help");
        result.Handler.ShouldBe(RouteHandler.None);
    }

    [Fact]
    public void Route_SlashHelpWithTrailingSpaces_ReturnsHelp()
    {
        var result = _router.Route("/help   ");
        result.Handler.ShouldBe(RouteHandler.Help);
    }

    [Fact]
    public void GetHelpText_ReturnsNonEmpty()
    {
        var help = ChatEngine.GetHelpText();
        help.ShouldNotBeNullOrEmpty();
        help.ShouldContain("/maths");
        help.ShouldContain("/help");
        help.ShouldContain("/remind");
    }

    [Fact]
    public void Route_ClassifierNotReady_ReturnsNone()
    {
        var classifier = new IntentClassifier();
        var result = _router.Route("tell me a joke", classifier);
        result.Handler.ShouldBe(RouteHandler.None);
    }

    [Fact]
    public void Route_ClassifierNull_ReturnsNone()
    {
        var result = _router.Route("tell me a joke", null);
        result.Handler.ShouldBe(RouteHandler.None);
    }

    [Fact]
    public void Route_ClassifierConfidentStory_ReturnsStoryHandler()
    {
        var classifier = new IntentClassifier();
        classifier.Train(new List<(string, string)>
        {
            ("tell me a story", "story_request"),
            ("tell me another story", "story_request"),
            ("make up a story", "story_request"),
            ("narrate a tale", "story_request"),
            ("i like pizza", "preference_statement"),
            ("i hate broccoli", "dislike_statement"),
            ("the sky is blue", "general_fact"),
        });

        var result = _router.Route("tell me a story", classifier);
        result.Handler.ShouldBe(RouteHandler.Story);
        result.IntentCategory.ShouldBe("story_request");
        result.Confidence.ShouldBeGreaterThanOrEqualTo(0.85);
    }

    [Fact]
    public void Route_ClassifierConfidentMath_ReturnsMathHandler()
    {
        var classifier = new IntentClassifier();
        classifier.Train(new List<(string, string)>
        {
            ("what is 2 plus 2", "math_query"),
            ("calculate 5 times 3", "math_query"),
            ("solve 10 divided by 2", "math_query"),
            ("i like pizza", "preference_statement"),
            ("the sky is blue", "general_fact"),
        });

        var result = _router.Route("what is 2 plus 2", classifier);
        result.Handler.ShouldBe(RouteHandler.Math);
        result.IntentCategory.ShouldBe("math_query");
    }

    [Fact]
    public void Route_ClassifierConfidentGame_ReturnsGameHandler()
    {
        var classifier = new IntentClassifier();
        classifier.Train(new List<(string, string)>
        {
            ("let's play a game", "game_start"),
            ("want to play a game", "game_start"),
            ("play a word game with me", "game_start"),
            ("i like pizza", "preference_statement"),
        });

        var result = _router.Route("let's play a game", classifier);
        result.Handler.ShouldBe(RouteHandler.Game);
    }

    [Fact]
    public void Route_ClassifierConfidentHangman_ReturnsHangmanHandler()
    {
        var classifier = new IntentClassifier();
        classifier.Train(new List<(string, string)>
        {
            ("let's play hangman", "hangman_start"),
            ("play hangman with me", "hangman_start"),
            ("i want to play hangman", "hangman_start"),
            ("i like pizza", "preference_statement"),
        });

        var result = _router.Route("let's play hangman", classifier);
        result.Handler.ShouldBe(RouteHandler.Hangman);
    }

    [Fact]
    public void Route_ClassifierConfidentJoke_ReturnsJokeHandler()
    {
        var classifier = new IntentClassifier();
        classifier.Train(new List<(string, string)>
        {
            ("tell me a joke", "joke_request"),
            ("make me laugh", "joke_request"),
            ("tell me a funny joke", "joke_request"),
            ("crack a joke", "joke_request"),
            ("i like pizza", "preference_statement"),
        });

        var result = _router.Route("tell me a joke", classifier);
        result.Handler.ShouldBe(RouteHandler.Joke);
    }

    [Fact]
    public void Route_ClassifierLowConfidence_ReturnsNone()
    {
        var classifier = new IntentClassifier();
        classifier.Train(new List<(string, string)>
        {
            ("tell me a story", "story_request"),
            ("i like pizza", "preference_statement"),
        });

        var result = _router.Route("purple elephants dance silently", classifier);
        result.Handler.ShouldBe(RouteHandler.None);
    }

    [Fact]
    public void Route_ClassifierNotRoutingIntent_ReturnsNone()
    {
        var classifier = new IntentClassifier();
        classifier.Train(new List<(string, string)>
        {
            ("i like pizza", "preference_statement"),
            ("i love cats", "preference_statement"),
            ("i enjoy hiking", "preference_statement"),
            ("i hate broccoli", "dislike_statement"),
        });

        var result = _router.Route("i like pizza", classifier);
        result.Handler.ShouldBe(RouteHandler.None);
    }

    [Fact]
    public void Route_ClassifierSlashOverrides_ReturnsSlashResult()
    {
        var classifier = new IntentClassifier();
        classifier.Train(new List<(string, string)>
        {
            ("tell me a story", "story_request"),
            ("i like pizza", "preference_statement"),
        });

        var result = _router.Route("/story", classifier);
        result.Handler.ShouldBe(RouteHandler.Story);
        result.IsSlashCommand.ShouldBeTrue();
    }

    [Fact]
    public void Route_ClassifierConfidentStats_ReturnsStatsHandler()
    {
        var classifier = new IntentClassifier();
        classifier.Train(new List<(string, string)>
        {
            ("show me my stats", "stats_query"),
            ("what are my statistics", "stats_query"),
            ("tell me some stats", "stats_query"),
            ("i like pizza", "preference_statement"),
        });

        var result = _router.Route("show me my stats", classifier);
        result.Handler.ShouldBe(RouteHandler.Stats);
    }

    [Fact]
    public void Route_ClassifierConfidentAboutMe_ReturnsAboutMeHandler()
    {
        var classifier = new IntentClassifier();
        classifier.Train(new List<(string, string)>
        {
            ("tell me about myself", "about_me_query"),
            ("what do you know about me", "about_me_query"),
            ("what do you remember about me", "about_me_query"),
            ("i like pizza", "preference_statement"),
        });

        var result = _router.Route("tell me about myself", classifier);
        result.Handler.ShouldBe(RouteHandler.AboutMe);
    }

    [Fact]
    public void Route_ClassifierConfidentReset_ReturnsResetHandler()
    {
        var classifier = new IntentClassifier();
        classifier.Train(new List<(string, string)>
        {
            ("reset everything", "reset_request"),
            ("start fresh", "reset_request"),
            ("forget everything", "reset_request"),
            ("i like pizza", "preference_statement"),
        });

        var result = _router.Route("reset everything", classifier);
        result.Handler.ShouldBe(RouteHandler.Reset);
    }
}

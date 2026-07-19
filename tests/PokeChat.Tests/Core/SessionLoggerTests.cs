using PokeChat.Core;
using Shouldly;

namespace PokeChat.Tests.Core;

public class SessionLoggerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _sessionId = Guid.NewGuid().ToString();

    public SessionLoggerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "PokeChatLogTests_" + Guid.NewGuid().ToString()[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    private static SessionLogConfig Config(string mode = "basic", int maxFiles = 10) =>
        new() { Mode = mode, MaxLogFiles = maxFiles, Directory = "logs" };

    [Fact]
    public void SessionLogger_CreatesLogFile()
    {
        using var logger = new SessionLogger(_sessionId, Config(), _tempDir);
        logger.LogTurn("hello", "Hi there!");

        logger.LogPath.ShouldNotBeNull();
        File.Exists(logger.LogPath).ShouldBeTrue();
        var content = File.ReadAllText(logger.LogPath);
        content.ShouldContain("# Chat Session Log");
        content.ShouldContain(_sessionId);
    }

    [Fact]
    public void SessionLogger_WritesUserAndBotContent()
    {
        using var logger = new SessionLogger(_sessionId, Config(), _tempDir);
        logger.LogTurn("hello", "Hi there!");

        var content = File.ReadAllText(logger.LogPath!);
        content.ShouldContain("### User");
        content.ShouldContain("hello");
        content.ShouldContain("### Bot");
        content.ShouldContain("Hi there!");
    }

    [Fact]
    public void SessionLogger_WritesMultipleTurns()
    {
        using var logger = new SessionLogger(_sessionId, Config(), _tempDir);
        logger.LogTurn("hello", "Hi!");
        logger.LogTurn("my name is Alice", "Nice to meet you, Alice!");
        logger.LogTurn("I like pizza", "You like pizza!");

        var content = File.ReadAllText(logger.LogPath!);
        content.ShouldContain("## Turn 1");
        content.ShouldContain("## Turn 2");
        content.ShouldContain("## Turn 3");
        content.ShouldContain("hello");
        content.ShouldContain("my name is Alice");
        content.ShouldContain("I like pizza");
    }

    [Fact]
    public void SessionLogger_VerboseMode_IncludesContextData()
    {
        using var logger = new SessionLogger(_sessionId, Config("verbose"), _tempDir);
        var context = new Dictionary<string, string>
        {
            ["sentiment"] = "positive",
            ["intensity"] = "3",
            ["response_category"] = "empathy_happy",
            ["last_rule_id"] = "42"
        };
        logger.LogTurn("I'm happy!", "That's great!", context);

        var content = File.ReadAllText(logger.LogPath!);
        content.ShouldContain("### Context");
        content.ShouldContain("sentiment: positive");
        content.ShouldContain("intensity: 3");
        content.ShouldContain("response_category: empathy_happy");
        content.ShouldContain("last_rule_id: 42");
    }

    [Fact]
    public void SessionLogger_BasicMode_OmitsContextData()
    {
        using var logger = new SessionLogger(_sessionId, Config("basic"), _tempDir);
        var context = new Dictionary<string, string>
        {
            ["sentiment"] = "positive"
        };
        logger.LogTurn("I'm happy!", "That's great!", context);

        var content = File.ReadAllText(logger.LogPath!);
        content.ShouldNotContain("### Context");
        content.ShouldNotContain("sentiment: positive");
    }

    [Fact]
    public void SessionLogger_LogRotation_RemovesOldest()
    {
        var oldId = Guid.NewGuid().ToString();
        var oldFilePath = Path.Combine(_tempDir, $"session_{oldId}_20260101_000000.log");
        File.WriteAllText(oldFilePath, "old log");

        using var logger = new SessionLogger(_sessionId, Config("basic", maxFiles: 1), _tempDir);
        logger.LogTurn("hello", "Hi!");

        File.Exists(oldFilePath).ShouldBeFalse();
    }

    [Fact]
    public void SessionLogger_LogRotation_KeepsNewestWithinLimit()
    {
        var oldId = Guid.NewGuid().ToString();
        var oldFilePath = Path.Combine(_tempDir, $"session_{oldId}_20260101_000000.log");
        File.WriteAllText(oldFilePath, "old log");

        using var logger = new SessionLogger(_sessionId, Config("basic", maxFiles: 2), _tempDir);
        logger.LogTurn("hello", "Hi!");

        File.Exists(oldFilePath).ShouldBeTrue();
    }

    [Fact]
    public void SessionLogger_VerboseProperty_ReflectsConfig()
    {
        using var verboseLogger = new SessionLogger(_sessionId, Config("verbose"), _tempDir);
        verboseLogger.Verbose.ShouldBeTrue();

        using var basicLogger = new SessionLogger(_sessionId, Config("basic"), _tempDir);
        basicLogger.Verbose.ShouldBeFalse();
    }

    [Fact]
    public void SessionLogger_Disabled_DoesNotCreateFile()
    {
        var config = new SessionLogConfig { Mode = "basic", MaxLogFiles = 10, Enabled = false };
        using var logger = new SessionLogger(_sessionId, config, _tempDir);
        logger.LogTurn("hello", "Hi!");

        logger.LogPath.ShouldBeNull();
        logger.Enabled.ShouldBeFalse();
        Directory.GetFiles(_tempDir, "session_*.log").ShouldBeEmpty();
    }

    [Fact]
    public void SessionLogger_LogSystem_WritesSystemEntry()
    {
        using var logger = new SessionLogger(_sessionId, Config(), _tempDir);
        logger.LogSystem("Welcome to PokeChat!");

        var content = File.ReadAllText(logger.LogPath!);
        content.ShouldContain("## System");
        content.ShouldContain("Welcome to PokeChat!");
    }

    [Fact]
    public void SessionLogger_LogSystem_InterleavesWithTurns()
    {
        using var logger = new SessionLogger(_sessionId, Config(), _tempDir);
        logger.LogSystem("Welcome!");
        logger.LogTurn("hello", "Hi!");
        logger.LogSystem("Goodbye!");

        var content = File.ReadAllText(logger.LogPath!);
        content.ShouldContain("## System");
        content.ShouldContain("Welcome!");
        content.ShouldContain("## Turn 1");
        content.ShouldContain("hello");
        content.ShouldContain("## System");
        content.ShouldContain("Goodbye!");
    }

    [Fact]
    public void SessionLogger_Dispose_WritesSessionEnded()
    {
        var logger = new SessionLogger(_sessionId, Config(), _tempDir);
        logger.LogTurn("hello", "hi there"); // need at least one turn
        logger.Dispose();

        var content = File.ReadAllText(logger.LogPath!);
        content.ShouldContain("## Session Ended");
    }

    [Fact]
    public void SessionLogger_Dispose_DeletesFileWhenNoTurns()
    {
        var logger = new SessionLogger(_sessionId, Config(), _tempDir);
        var path = logger.LogPath;
        logger.Dispose();

        File.Exists(path).ShouldBeFalse();
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }
        catch
        {
            // best effort cleanup
        }
    }
}

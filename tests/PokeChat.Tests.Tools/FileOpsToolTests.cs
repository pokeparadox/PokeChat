using PokeChat.Tools;
using Shouldly;

namespace PokeChat.Tests.Tools;

public class FileOpsToolTests : IDisposable
{
    private readonly string _testDir;
    private readonly FileOpsTool _tool;
    private readonly FileOpsTool _restrictedTool;

    public FileOpsToolTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "PokeChat_FileOpsTests_" + Guid.NewGuid());
        Directory.CreateDirectory(_testDir);
        _tool = new FileOpsTool(new[] { _testDir });
        _restrictedTool = new FileOpsTool(new[] { _testDir });
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    [Fact]
    public void Execute_EmptyArgs_ReturnsFailure()
    {
        var result = _tool.Execute(Array.Empty<string>());
        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldContain("No command provided");
    }

    [Fact]
    public void Execute_UnknownCommand_ReturnsFailure()
    {
        var result = _tool.Execute(new[] { "xyz" });
        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldContain("Unknown command");
    }

    [Fact]
    public void Read_NonExistentFile_ReturnsFailure()
    {
        var result = _tool.Execute(new[] { "read", Path.Combine(_testDir, "nonexistent.txt") });
        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldContain("File not found");
    }

    [Fact]
    public void Read_ExistingFile_ReturnsContent()
    {
        var filePath = Path.Combine(_testDir, "hello.txt");
        File.WriteAllText(filePath, "Hello, world!");

        var result = _tool.Execute(new[] { "read", filePath });
        result.Success.ShouldBeTrue();
        result.Output.ShouldBe("Hello, world!");
    }

    [Fact]
    public void Read_PathTraversal_Blocked()
    {
        var result = _tool.Execute(new[] { "read", "/etc/passwd" });
        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldContain("Access denied");
    }

    [Fact]
    public void Read_PathTraversalWithDots_Blocked()
    {
        var result = _tool.Execute(new[] { "read", "../../../etc/passwd" });
        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldContain("Access denied");
    }

    [Fact]
    public void Read_MissingPathArg_ReturnsFailure()
    {
        var result = _tool.Execute(new[] { "read" });
        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldContain("No file path");
    }

    [Fact]
    public void Write_CreatesNewFile()
    {
        var filePath = Path.Combine(_testDir, "newfile.txt");

        var result = _tool.Execute(new[] { "write", filePath, "fresh content" });
        result.Success.ShouldBeTrue();
        result.Output.ShouldContain("Written");

        File.ReadAllText(filePath).ShouldBe("fresh content");
    }

    [Fact]
    public void Write_BlockedExtension_ReturnsFailure()
    {
        var filePath = Path.Combine(_testDir, "evil.exe");

        var result = _tool.Execute(new[] { "write", filePath, "bad stuff" });
        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldContain("not allowed");
    }

    [Fact]
    public void Write_BlockedPathTraversal_ReturnsFailure()
    {
        var result = _tool.Execute(new[] { "write", "/tmp/evil.txt", "bad" });
        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldContain("Access denied");
    }

    [Fact]
    public void Write_MissingArgs_ReturnsFailure()
    {
        var result = _tool.Execute(new[] { "write", "onlypath.txt" });
        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldContain("Missing arguments");
    }

    [Fact]
    public void List_ExistingDirectory_ReturnsEntries()
    {
        File.WriteAllText(Path.Combine(_testDir, "a.txt"), "a");
        Directory.CreateDirectory(Path.Combine(_testDir, "sub"));
        File.WriteAllText(Path.Combine(_testDir, "sub", "b.txt"), "b");

        var result = _tool.Execute(new[] { "list", _testDir });
        result.Success.ShouldBeTrue();
        result.Output.ShouldContain("sub/");
        result.Output.ShouldContain("a.txt");
    }

    [Fact]
    public void List_NonExistentDirectory_ReturnsFailure()
    {
        var result = _tool.Execute(new[] { "list", Path.Combine(_testDir, "nonexistent_dir") });
        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldContain("Directory not found");
    }

    [Fact]
    public void List_BlockedPath_ReturnsFailure()
    {
        var result = _tool.Execute(new[] { "list", "/etc" });
        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldContain("Access denied");
    }

    [Fact]
    public void Search_FindsPattern_ReturnsMatches()
    {
        File.WriteAllText(Path.Combine(_testDir, "data.txt"), "apple\nbanana\napple pie\ncherry");

        var result = _tool.Execute(new[] { "search", _testDir, "apple" });
        result.Success.ShouldBeTrue();
        result.Output.ShouldContain("apple");
        result.Output.ShouldContain("apple pie");
    }

    [Fact]
    public void Search_NoMatches_ReturnsEmpty()
    {
        File.WriteAllText(Path.Combine(_testDir, "data.txt"), "something else");

        var result = _tool.Execute(new[] { "search", _testDir, "xyz" });
        result.Success.ShouldBeTrue();
        result.Output.ShouldContain("No matches");
    }

    [Fact]
    public void Search_BlockedPath_ReturnsFailure()
    {
        var result = _tool.Execute(new[] { "search", "/etc", "root" });
        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldContain("Access denied");
    }

    [Fact]
    public void Search_MissingArgs_ReturnsFailure()
    {
        var result = _tool.Execute(new[] { "search" });
        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldContain("Missing arguments");
    }

    [Fact]
    public void Search_InvalidRegex_ReturnsFailure()
    {
        var result = _tool.Execute(new[] { "search", _testDir, "[invalid" });
        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldContain("Invalid regex");
    }

    [Fact]
    public void Read_WithDefaultAllowedPath_Succeeds()
    {
        var filePath = Path.Combine(_testDir, "test.txt");
        File.WriteAllText(filePath, "content");
        var defaultTool = new FileOpsTool(new[] { _testDir });
        var result = defaultTool.Execute(new[] { "read", filePath });
        result.Success.ShouldBeTrue();
        result.Output.ShouldBe("content");
    }

    [Fact]
    public void Write_CreatesDirectoryStructure()
    {
        var nestedDir = Path.Combine(_testDir, "a", "b");
        var filePath = Path.Combine(nestedDir, "nested.txt");

        var result = _tool.Execute(new[] { "write", filePath, "nested content" });
        result.Success.ShouldBeTrue();

        File.ReadAllText(filePath).ShouldBe("nested content");
    }
}

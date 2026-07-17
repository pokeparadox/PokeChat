using PokeChat.Api.Services;
using Shouldly;

namespace PokeChat.Tests.Tools;

public class ProjectDetectorTests : IDisposable
{
    private readonly string _testDir;

    public ProjectDetectorTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"pokechat_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    [Fact]
    public void Detect_DotnetProject_ReturnsDotnet()
    {
        File.WriteAllText(Path.Combine(_testDir, "Test.csproj"), "<Project></Project>");
        var detector = new ProjectDetector();
        var results = detector.Detect(_testDir);
        results.ShouldContain(p => p.Language == "dotnet");
    }

    [Fact]
    public void Detect_NodeProject_ReturnsNode()
    {
        File.WriteAllText(Path.Combine(_testDir, "package.json"), "{\"name\": \"test\"}");
        var detector = new ProjectDetector();
        var results = detector.Detect(_testDir);
        results.ShouldContain(p => p.Language == "node");
    }

    [Fact]
    public void Detect_RustProject_ReturnsRust()
    {
        File.WriteAllText(Path.Combine(_testDir, "Cargo.toml"), "[package]\nname = \"test\"");
        var detector = new ProjectDetector();
        var results = detector.Detect(_testDir);
        results.ShouldContain(p => p.Language == "rust");
    }

    [Fact]
    public void Detect_GoProject_ReturnsGo()
    {
        File.WriteAllText(Path.Combine(_testDir, "go.mod"), "module test");
        var detector = new ProjectDetector();
        var results = detector.Detect(_testDir);
        results.ShouldContain(p => p.Language == "go");
    }

    [Fact]
    public void Detect_PythonProject_ReturnsPython()
    {
        File.WriteAllText(Path.Combine(_testDir, "pyproject.toml"), "[project]\nname = \"test\"");
        var detector = new ProjectDetector();
        var results = detector.Detect(_testDir);
        results.ShouldContain(p => p.Language == "python");
    }

    [Fact]
    public void Detect_EmptyDirectory_ReturnsEmpty()
    {
        var detector = new ProjectDetector();
        var results = detector.Detect(_testDir);
        results.ShouldBeEmpty();
    }

    [Fact]
    public void Detect_MultipleLanguages_ReturnsMultiple()
    {
        File.WriteAllText(Path.Combine(_testDir, "package.json"), "{}");
        File.WriteAllText(Path.Combine(_testDir, "requirements.txt"), "");
        var detector = new ProjectDetector();
        var results = detector.Detect(_testDir);
        results.Count.ShouldBeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public void Detect_CSFileWithoutProject_StillDetected()
    {
        for (int i = 0; i < 3; i++)
            File.WriteAllText(Path.Combine(_testDir, $"File{i}.cs"), "Console.WriteLine();");
        var detector = new ProjectDetector();
        var results = detector.Detect(_testDir);
        results.ShouldContain(p => p.Language == "dotnet");
    }

    [Fact]
    public void GetGitignoreName_CSharp_ReturnsVisualStudio()
    {
        var name = ProjectDetector.GetGitignoreName("csharp");
        name.ShouldBe("VisualStudio");
    }

    [Fact]
    public void GetGitignoreName_Dotnet_ReturnsVisualStudio()
    {
        var name = ProjectDetector.GetGitignoreName("dotnet");
        name.ShouldBe("VisualStudio");
    }

    [Fact]
    public void GetGitignoreName_Node_ReturnsNode()
    {
        var name = ProjectDetector.GetGitignoreName("node");
        name.ShouldBe("Node");
    }

    [Fact]
    public void GetGitignoreName_Rust_ReturnsRust()
    {
        var name = ProjectDetector.GetGitignoreName("rust");
        name.ShouldBe("Rust");
    }

    [Fact]
    public void GetGitignoreName_Python_ReturnsPython()
    {
        var name = ProjectDetector.GetGitignoreName("python");
        name.ShouldBe("Python");
    }

    [Fact]
    public void Detect_ConfidenceScore_IsBetween0And1()
    {
        File.WriteAllText(Path.Combine(_testDir, "package.json"), "{}");
        var detector = new ProjectDetector();
        var results = detector.Detect(_testDir);
        foreach (var result in results)
            result.Confidence.ShouldBeInRange(0.0, 1.0);
    }

    [Fact]
    public void Detect_Reason_IsNotEmpty()
    {
        File.WriteAllText(Path.Combine(_testDir, "Cargo.toml"), "[package]\nname = \"test\"");
        var detector = new ProjectDetector();
        var results = detector.Detect(_testDir);
        results.ShouldAllBe(p => !string.IsNullOrWhiteSpace(p.Reason));
    }
}

using PokeChat.Api.Services;
using Shouldly;

namespace PokeChat.Tests.Tools;

public class AgentsMdGeneratorTests : IDisposable
{
    private readonly string _testDir;

    public AgentsMdGeneratorTests()
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
    public void Generate_DotnetProject_ContainsDotnetCommands()
    {
        File.WriteAllText(Path.Combine(_testDir, "Test.csproj"), "<Project></Project>");
        var generator = new AgentsMdGenerator();
        var projects = new List<ProjectMatch> { new("dotnet", "VisualStudio", 0.9, "test") };
        var result = generator.Generate(_testDir, projects);
        result.ShouldContain("dotnet build");
        result.ShouldContain("dotnet run");
        result.ShouldContain("dotnet test");
    }

    [Fact]
    public void Generate_NodeProject_ContainsNodeCommands()
    {
        File.WriteAllText(Path.Combine(_testDir, "package.json"), "{}");
        var generator = new AgentsMdGenerator();
        var projects = new List<ProjectMatch> { new("node", "Node", 0.9, "test") };
        var result = generator.Generate(_testDir, projects);
        result.ShouldContain("npm install");
        result.ShouldContain("npm run dev");
    }

    [Fact]
    public void Generate_RustProject_ContainsCargoCommands()
    {
        File.WriteAllText(Path.Combine(_testDir, "Cargo.toml"), "[package]\nname = \"test\"");
        var generator = new AgentsMdGenerator();
        var projects = new List<ProjectMatch> { new("rust", "Rust", 0.9, "test") };
        var result = generator.Generate(_testDir, projects);
        result.ShouldContain("cargo build");
        result.ShouldContain("cargo run");
        result.ShouldContain("cargo test");
    }

    [Fact]
    public void Generate_PythonProject_ContainsPythonCommands()
    {
        File.WriteAllText(Path.Combine(_testDir, "pyproject.toml"), "[project]\nname = \"test\"");
        var generator = new AgentsMdGenerator();
        var projects = new List<ProjectMatch> { new("python", "Python", 0.9, "test") };
        var result = generator.Generate(_testDir, projects);
        result.ShouldContain("pip install");
        result.ShouldContain("pytest");
    }

    [Fact]
    public void Generate_ContainsProjectName()
    {
        var generator = new AgentsMdGenerator();
        var projects = new List<ProjectMatch>();
        var result = generator.Generate(_testDir, projects);
        result.ShouldContain("# " + Path.GetFileName(_testDir));
    }

    [Fact]
    public void Generate_ContainsArchitectureSection()
    {
        var generator = new AgentsMdGenerator();
        var projects = new List<ProjectMatch>();
        var result = generator.Generate(_testDir, projects);
        result.ShouldContain("## Architecture");
    }

    [Fact]
    public void Generate_ContainsKeyFilesSection()
    {
        var generator = new AgentsMdGenerator();
        var projects = new List<ProjectMatch>();
        var result = generator.Generate(_testDir, projects);
        result.ShouldContain("## Key Files");
    }

    [Fact]
    public void Generate_WithCsproj_ListsCsprojInKeyFiles()
    {
        File.WriteAllText(Path.Combine(_testDir, "Test.csproj"), "<Project></Project>");
        var generator = new AgentsMdGenerator();
        var projects = new List<ProjectMatch> { new("dotnet", "VisualStudio", 0.9, "test") };
        var result = generator.Generate(_testDir, projects);
        result.ShouldContain("Test.csproj");
    }

    [Fact]
    public void Generate_WithPackageJson_ListsPackageJsonInKeyFiles()
    {
        File.WriteAllText(Path.Combine(_testDir, "package.json"), "{}");
        var generator = new AgentsMdGenerator();
        var projects = new List<ProjectMatch> { new("node", "Node", 0.9, "test") };
        var result = generator.Generate(_testDir, projects);
        result.ShouldContain("package.json");
    }

    [Fact]
    public void Generate_DirectoryTree_ContainsDirectoryStructure()
    {
        Directory.CreateDirectory(Path.Combine(_testDir, "src"));
        File.WriteAllText(Path.Combine(_testDir, "src", "main.cs"), "");
        var generator = new AgentsMdGenerator();
        var projects = new List<ProjectMatch>();
        var result = generator.Generate(_testDir, projects);
        result.ShouldContain("src/");
        result.ShouldContain("main.cs");
    }

    [Fact]
    public void Generate_DirectoryTree_ExcludesNodeModules()
    {
        Directory.CreateDirectory(Path.Combine(_testDir, "node_modules"));
        File.WriteAllText(Path.Combine(_testDir, "node_modules", "test.js"), "");
        Directory.CreateDirectory(Path.Combine(_testDir, "src"));
        File.WriteAllText(Path.Combine(_testDir, "src", "main.cs"), "");
        var generator = new AgentsMdGenerator();
        var projects = new List<ProjectMatch>();
        var result = generator.Generate(_testDir, projects);
        result.ShouldNotContain("node_modules");
        result.ShouldContain("src/");
    }

    [Fact]
    public void Generate_EmptyProjects_StillContainsSections()
    {
        var generator = new AgentsMdGenerator();
        var projects = new List<ProjectMatch>();
        var result = generator.Generate(_testDir, projects);
        result.ShouldContain("## Project");
        result.ShouldContain("## Architecture");
        result.ShouldContain("## Key Files");
    }
}

using PokeChat.Api.Services;
using Shouldly;

namespace PokeChat.Tests.Tools;

public class GitignoreServiceTests
{
    private readonly GitignoreService _service;

    public GitignoreServiceTests()
    {
        _service = new GitignoreService();
    }

    private static async Task<bool> CanReachGitHubApi()
    {
        try
        {
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(3);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("PokeChat-Tests/1.0");
            var response = await client.GetAsync("https://api.github.com/gitignore/templates/Node");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    [Fact]
    public async Task FetchTemplateAsync_ValidName_ReturnsContent()
    {
        if (!await CanReachGitHubApi()) return;
        var result = await _service.FetchTemplateAsync("Node");
        result.ShouldNotBeNull();
        result.ShouldContain("node_modules");
    }

    [Fact]
    public async Task FetchTemplateAsync_InvalidName_ReturnsNull()
    {
        if (!await CanReachGitHubApi()) return;
        var result = await _service.FetchTemplateAsync("NonExistentLanguage12345");
        result.ShouldBeNull();
    }

    [Fact]
    public async Task FetchTemplateAsync_CSharp_ReturnsVisualStudioEntries()
    {
        if (!await CanReachGitHubApi()) return;
        var result = await _service.FetchTemplateAsync("VisualStudio");
        result.ShouldNotBeNull();
        result.ShouldContain("[Dd]ebug/");
    }

    [Fact]
    public async Task BuildGitignoreAsync_SingleTemplate_ReturnsContent()
    {
        if (!await CanReachGitHubApi()) return;
        var result = await _service.BuildGitignoreAsync(new List<string> { "Node" });
        result.ShouldContain("node_modules");
        result.ShouldContain("# === Node ===");
    }

    [Fact]
    public async Task BuildGitignoreAsync_DuplicateTemplates_OnlyFetchesOnce()
    {
        if (!await CanReachGitHubApi()) return;
        var result = await _service.BuildGitignoreAsync(new List<string> { "Node", "Node" });
        result.ShouldContain("node_modules");
        result.Split("# === Node ===").Length.ShouldBe(2);
    }

    [Fact]
    public async Task BuildGitignoreAsync_MultipleTemplates_ContainsAllHeaders()
    {
        if (!await CanReachGitHubApi()) return;
        var result = await _service.BuildGitignoreAsync(new List<string> { "Node", "Python" });
        result.ShouldContain("# === Node ===");
        result.ShouldContain("# === Python ===");
    }

    [Fact]
    public async Task BuildGitignoreAsync_EmptyList_ReturnsEmpty()
    {
        var result = await _service.BuildGitignoreAsync(new List<string>());
        result.ShouldBeEmpty();
    }

    [Fact]
    public void ShouldMerge_NonExistentFile_ReturnsCreate()
    {
        var path = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.gitignore");
        GitignoreService.ShouldMerge(path).ShouldBe(MergeAction.Create);
    }

    [Fact]
    public void ShouldMerge_EmptyFile_ReturnsOverwrite()
    {
        var path = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.gitignore");
        File.WriteAllText(path, "");
        try
        {
            GitignoreService.ShouldMerge(path).ShouldBe(MergeAction.Overwrite);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ShouldMerge_ExistingContent_ReturnsMerge()
    {
        var path = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.gitignore");
        File.WriteAllText(path, "# existing\n*.log\n");
        try
        {
            GitignoreService.ShouldMerge(path).ShouldBe(MergeAction.Merge);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void MergeGitignore_ExistingContent_PreservesOriginal()
    {
        var existing = "# existing\n*.log\n";
        var newContent = "# === Node ===\nnode_modules/\n";
        var result = GitignoreService.MergeGitignore(existing, newContent);
        result.ShouldContain("*.log");
        result.ShouldContain("node_modules/");
    }

    [Fact]
    public void MergeGitignore_DuplicateSection_SkipsDuplicate()
    {
        var existing = "# === Node ===\nnode_modules/\n\n# custom\n*.log\n";
        var newContent = "# === Node ===\nnode_modules/\n# === Python ===\n__pycache__/\n";
        var result = GitignoreService.MergeGitignore(existing, newContent);
        result.ShouldContain("*.log");
        result.ShouldContain("node_modules/");
        result.ShouldContain("__pycache__/");
        result.Split("# === Node ===").Length.ShouldBe(2);
        result.Split("# === Python ===").Length.ShouldBe(2);
    }
}

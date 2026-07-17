using System.Diagnostics;
using System.Text;

namespace PokeChat.Api.Services;

public class GitignoreService
{
    private readonly HttpClient _httpClient;
    private const string GitHubApiBase = "https://api.github.com/gitignore/templates";
    private const string GitignoreRepoUrl = "https://github.com/github/gitignore.git";
    private static readonly TimeSpan StalenessThreshold = TimeSpan.FromDays(7);

    private string CacheDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".cache", "pokechat", "gitignore");

    private string RepoDir => Path.Combine(CacheDir, "gitignore");

    private Action<string>? _status;

    public GitignoreService(HttpClient? httpClient = null, Action<string>? status = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("PokeChat/1.0");
        _httpClient.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github.raw+json");
        _status = status;
    }

    private void ReportStatus(string msg) => _status?.Invoke(msg);

    public async Task<string?> FetchTemplateAsync(string name)
    {
        var local = ReadFromCache(name);
        if (local != null)
            return local;

        ReportStatus("Gitignore: updating local cache...");
        await EnsureCacheAsync();
        local = ReadFromCache(name);
        if (local != null)
            return local;

        ReportStatus("Gitignore: fetching from GitHub API...");
        return await FetchFromApiAsync(name);
    }

    private string? ReadFromCache(string name)
    {
        var path = Path.Combine(RepoDir, $"{name}.gitignore");
        if (!File.Exists(path))
            return null;

        try
        {
            return File.ReadAllText(path);
        }
        catch
        {
            return null;
        }
    }

    private async Task<string?> FetchFromApiAsync(string name)
    {
        try
        {
            var url = $"{GitHubApiBase}/{Uri.EscapeDataString(name)}";
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
                return null;
            return await response.Content.ReadAsStringAsync();
        }
        catch
        {
            return null;
        }
    }

    private async Task EnsureCacheAsync()
    {
        try
        {
            if (Directory.Exists(Path.Combine(RepoDir, ".git")))
            {
                if (IsCacheFresh())
                    return;

                await RunGitAsync("pull", RepoDir);
                UpdateCacheTimestamp();
            }
            else
            {
                Directory.CreateDirectory(CacheDir);
                var result = await RunGitAsync($"clone --depth 1 {GitignoreRepoUrl}", CacheDir);
                if (result.ExitCode != 0)
                    return;
                UpdateCacheTimestamp();
            }
        }
        catch
        {
            // Git not available or network issue — fall back to API
        }
    }

    private bool IsCacheFresh()
    {
        var stampPath = Path.Combine(CacheDir, ".last_update");
        if (!File.Exists(stampPath))
            return false;

        try
        {
            var text = File.ReadAllText(stampPath).Trim();
            if (DateTimeOffset.TryParse(text, out var lastUpdate))
                return DateTimeOffset.UtcNow - lastUpdate < StalenessThreshold;
        }
        catch { }

        return false;
    }

    private void UpdateCacheTimestamp()
    {
        try
        {
            Directory.CreateDirectory(CacheDir);
            File.WriteAllText(Path.Combine(CacheDir, ".last_update"), DateTimeOffset.UtcNow.ToString("o"));
        }
        catch { }
    }

    private static async Task<(int ExitCode, string Output)> RunGitAsync(string arguments, string workingDir)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = arguments,
                WorkingDirectory = workingDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var process = Process.Start(psi);
            if (process == null)
                return (-1, "process is null");
            await process.WaitForExitAsync();
            var output = await process.StandardOutput.ReadToEndAsync();
            var errors = await process.StandardError.ReadToEndAsync();
            return (process.ExitCode, string.IsNullOrEmpty(errors) ? output : errors);
        }
        catch (Exception ex)
        {
            return (-1, ex.Message);
        }
    }

    public async Task<string> BuildGitignoreAsync(List<string> templateNames)
    {
        var sb = new StringBuilder();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var name in templateNames)
        {
            if (!seen.Add(name))
                continue;

            var content = await FetchTemplateAsync(name);
            if (content != null)
            {
                if (sb.Length > 0)
                    sb.AppendLine();
                sb.AppendLine($"# === {name} ===");
                sb.AppendLine(content.TrimEnd());
            }
        }

        return sb.ToString();
    }

    public static MergeAction ShouldMerge(string gitignorePath)
    {
        if (!File.Exists(gitignorePath))
            return MergeAction.Create;

        var existing = File.ReadAllText(gitignorePath);
        if (string.IsNullOrWhiteSpace(existing))
            return MergeAction.Overwrite;

        return MergeAction.Merge;
    }

    public static string MergeGitignore(string existing, string newContent)
    {
        var result = new StringBuilder(existing.TrimEnd());
        var lines = newContent.Split('\n');
        var currentSection = new StringBuilder();

        foreach (var line in lines)
        {
            if (line.StartsWith("# === ") && currentSection.Length > 0)
            {
                ProcessSection(result, currentSection, existing);
                currentSection.Clear();
            }
            if (currentSection.Length > 0)
                currentSection.Append('\n');
            currentSection.Append(line);
        }

        if (currentSection.Length > 0)
            ProcessSection(result, currentSection, existing);

        return result.ToString();
    }

    private static void ProcessSection(StringBuilder result, StringBuilder section, string existing)
    {
        var header = section.ToString().Split('\n').FirstOrDefault(l => l.StartsWith("# === "));
        if (header != null)
        {
            var name = header.Replace("# === ", "").Replace(" ===", "").Trim();
            if (existing.Contains($"# === {name} ===", StringComparison.OrdinalIgnoreCase))
                return;
        }

        result.AppendLine();
        result.AppendLine();
        result.Append(section.ToString().TrimEnd());
    }
}

public enum MergeAction
{
    Create,
    Merge,
    Overwrite
}

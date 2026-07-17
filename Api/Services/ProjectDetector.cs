namespace PokeChat.Api.Services;

public record ProjectMatch(string Language, string GitignoreName, double Confidence, string Reason);

public class ProjectDetector
{
    private static readonly Dictionary<string, (string[] Manifests, string[] Extensions, string GitignoreName, double Confidence)> ProjectTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["dotnet"] = (
            new[] { "*.csproj", "*.sln", "*.slnx", "packages.config", "Directory.Build.props", "*.fsproj", "*.vbproj" },
            new[] { "*.cs", "*.razor", "*.fs", "*.vb" },
            "VisualStudio",
            0.95),
        ["node"] = (
            new[] { "package.json", "tsconfig.json", "pnpm-workspace.yaml", "lerna.json" },
            new[] { "*.js", "*.ts", "*.jsx", "*.tsx", "*.mjs", "*.cjs" },
            "Node",
            0.90),
        ["rust"] = (
            new[] { "Cargo.toml", "Cargo.lock" },
            new[] { "*.rs" },
            "Rust",
            0.95),
        ["go"] = (
            new[] { "go.mod", "go.sum" },
            new[] { "*.go" },
            "Go",
            0.95),
        ["python"] = (
            new[] { "pyproject.toml", "requirements.txt", "setup.py", "setup.cfg", "Pipfile", "poetry.lock", "conda.yml" },
            new[] { "*.py", "*.pyi", "*.pyx" },
            "Python",
            0.90),
        ["java"] = (
            new[] { "pom.xml", "build.gradle", "build.gradle.kts", "settings.gradle", "settings.gradle.kts" },
            new[] { "*.java" },
            "Java",
            0.90),
        ["ruby"] = (
            new[] { "Gemfile", "Gemfile.lock", "*.gemspec", "Rakefile" },
            new[] { "*.rb" },
            "Ruby",
            0.90),
        ["php"] = (
            new[] { "composer.json", "composer.lock" },
            new[] { "*.php" },
            "PHP",
            0.90),
        ["swift"] = (
            new[] { "Package.swift", "*.xcodeproj", "*.xcworkspace" },
            new[] { "*.swift" },
            "Swift",
            0.90),
    };

    public List<ProjectMatch> Detect(string directory)
    {
        if (!Directory.Exists(directory))
            return new List<ProjectMatch>();

        var results = new List<ProjectMatch>();
        var files = Directory.GetFiles(directory).Select(Path.GetFileName).Where(f => f != null).Cast<string>().ToList();
        var extensions = Directory.GetFiles(directory, "*.*", SearchOption.TopDirectoryOnly)
            .Select(Path.GetExtension)
            .Where(e => !string.IsNullOrEmpty(e))
            .Select(e => e!.ToLowerInvariant())
            .GroupBy(e => e)
            .ToDictionary(g => g.Key, g => g.Count());

        foreach (var (typeName, config) in ProjectTypes)
        {
            double score = 0;
            string reason = "";

            foreach (var manifest in config.Manifests)
            {
                if (manifest.Contains('*'))
                {
                    var pattern = manifest.Replace("*.", "");
                    if (files.Any(f => f.EndsWith(pattern, StringComparison.OrdinalIgnoreCase)))
                    {
                        score = config.Confidence;
                        reason = $"found {manifest}";
                        break;
                    }
                }
                else if (files.Contains(manifest, StringComparer.OrdinalIgnoreCase))
                {
                    score = config.Confidence;
                    reason = $"found {manifest}";
                    break;
                }
            }

            if (score == 0)
            {
                int extCount = 0;
                foreach (var ext in config.Extensions)
                {
                    var extLower = ext.Replace("*.", "");
                    if (extensions.TryGetValue("." + extLower, out var count))
                        extCount += count;
                }
                if (extCount >= 3)
                {
                    score = 0.6;
                    reason = $"{extCount} source files ({config.Extensions[0]})";
                }
            }

            if (score > 0)
                results.Add(new ProjectMatch(typeName, config.GitignoreName, score, reason));
        }

        return results.OrderByDescending(r => r.Confidence).ToList();
    }

    private static readonly Dictionary<string, string> LanguageAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["csharp"] = "dotnet",
        ["c#"] = "dotnet",
    };

    public static string GetGitignoreName(string language)
    {
        var lookup = language;
        if (LanguageAliases.TryGetValue(language, out var alias))
            lookup = alias;

        if (ProjectTypes.TryGetValue(lookup, out var byKey))
            return byKey.GitignoreName;

        var byValue = ProjectTypes.Values.FirstOrDefault(c =>
            c.GitignoreName.Equals(language, StringComparison.OrdinalIgnoreCase));
        if (byValue != default)
            return byValue.GitignoreName;

        return language;
    }
}

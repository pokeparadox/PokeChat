using System.Text;

namespace PokeChat.Api.Services;

public class AgentsMdGenerator
{
    public string Generate(string directory, List<ProjectMatch> detectedProjects)
    {
        var projectName = Path.GetFileName(directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var sb = new StringBuilder();

        sb.AppendLine($"# {projectName} — Agent Notes");
        sb.AppendLine();

        sb.AppendLine("## Project");
        foreach (var proj in detectedProjects)
        {
            sb.AppendLine($"- {proj.Language} project");
            sb.AppendLine($"- Detected via: {proj.Reason}");
        }

        var buildCommands = DetectBuildCommands(directory, detectedProjects);
        if (buildCommands.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Commands");
            sb.AppendLine("```bash");
            foreach (var cmd in buildCommands)
                sb.AppendLine(cmd);
            sb.AppendLine("```");
        }

        sb.AppendLine();
        sb.AppendLine("## Architecture");
        var tree = BuildDirectoryTree(directory);
        sb.AppendLine("```");
        sb.AppendLine(tree);
        sb.AppendLine("```");

        sb.AppendLine();
        sb.AppendLine("## Key Files");
        var keyFiles = FindKeyFiles(directory, detectedProjects);
        foreach (var file in keyFiles)
            sb.AppendLine($"- {file}");

        return sb.ToString();
    }

    private static List<string> DetectBuildCommands(string directory, List<ProjectMatch> projects)
    {
        var commands = new List<string>();
        var files = Directory.GetFiles(directory).Select(Path.GetFileName).Where(f => f != null).Cast<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var proj in projects)
        {
            switch (proj.Language.ToLowerInvariant())
            {
                case "dotnet" or "csharp":
                    commands.Add("dotnet build          # build");
                    commands.Add("dotnet run            # run");
                    commands.Add("dotnet test           # test");
                    break;
                case "node":
                    if (files.Contains("package.json"))
                    {
                        commands.Add("npm install           # install deps");
                        commands.Add("npm run dev           # dev server");
                        commands.Add("npm test              # test");
                    }
                    break;
                case "rust":
                    commands.Add("cargo build           # build");
                    commands.Add("cargo run             # run");
                    commands.Add("cargo test            # test");
                    break;
                case "go":
                    commands.Add("go build ./...        # build");
                    commands.Add("go run .              # run");
                    commands.Add("go test ./...         # test");
                    break;
                case "python":
                    if (files.Contains("pyproject.toml"))
                        commands.Add("pip install -e .      # install");
                    else if (files.Contains("requirements.txt"))
                        commands.Add("pip install -r requirements.txt");
                    if (files.Contains("Makefile"))
                        commands.Add("make                  # build (via Makefile)");
                    commands.Add("python -m pytest      # test");
                    break;
                case "ruby":
                    commands.Add("bundle install        # install deps");
                    commands.Add("bundle exec rspec     # test");
                    break;
                case "php":
                    if (files.Contains("composer.json"))
                    {
                        commands.Add("composer install      # install deps");
                        commands.Add("composer test         # test");
                    }
                    break;
            }
        }

        if (File.Exists(Path.Combine(directory, "Makefile")))
            commands.Add("make                  # build (via Makefile)");

        return commands.Distinct().ToList();
    }

    private static string BuildDirectoryTree(string directory)
    {
        var sb = new StringBuilder();
        var dir = new DirectoryInfo(directory);
        var excludeDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "node_modules", ".git", "bin", "obj", "dist", "build",
            ".vs", ".vscode", "__pycache__", ".idea", "target",
            "vendor", ".next", ".nuxt", "coverage"
        };

        sb.AppendLine($"{dir.Name}/");
        WriteDirectoryTree(dir, sb, "", excludeDirs, 0);

        return sb.ToString();
    }

    private static void WriteDirectoryTree(DirectoryInfo dir, StringBuilder sb, string indent, HashSet<string> excludeDirs, int depth)
    {
        if (depth > 2)
            return;

        var dirs = dir.GetDirectories()
            .Where(d => !excludeDirs.Contains(d.Name) && !d.Name.StartsWith('.'))
            .OrderBy(d => d.Name)
            .Take(15)
            .ToList();

        var files = dir.GetFiles()
            .Where(f => !f.Name.StartsWith('.') && f.Name != "*.pyc")
            .OrderBy(f => f.Name)
            .Take(10)
            .ToList();

        for (int i = 0; i < dirs.Count; i++)
        {
            var isLast = i == dirs.Count - 1 && files.Count == 0;
            var connector = isLast ? "└── " : "├── ";
            var subIndent = isLast ? "    " : "│   ";

            sb.AppendLine($"{indent}{connector}{dirs[i].Name}/");
            WriteDirectoryTree(dirs[i], sb, indent + subIndent, excludeDirs, depth + 1);
        }

        for (int i = 0; i < files.Count; i++)
        {
            var isLast = i == files.Count - 1;
            var connector = isLast ? "└── " : "├── ";
            sb.AppendLine($"{indent}{connector}{files[i].Name}");
        }
    }

    private static List<string> FindKeyFiles(string directory, List<ProjectMatch> projects)
    {
        var keyFiles = new List<string>();
        var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var manifestFiles = new[]
        {
            "package.json", "tsconfig.json", "Cargo.toml", "go.mod",
            "pyproject.toml", "requirements.txt", "setup.py", "Makefile",
            "Dockerfile", "docker-compose.yml", "docker-compose.yaml",
            ".env.example", "README.md", "LICENSE", "AGENTS.md"
        };

        foreach (var file in manifestFiles)
        {
            var path = Path.Combine(directory, file);
            if (File.Exists(path) && known.Add(file))
                keyFiles.Add(file);
        }

        foreach (var proj in projects)
        {
            switch (proj.Language.ToLowerInvariant())
            {
                case "dotnet" or "csharp":
                    AddFiles(directory, keyFiles, known, "*.csproj", "*.sln", "*.slnx", "appsettings.json", "Program.cs");
                    break;
                case "node":
                    AddFiles(directory, keyFiles, known, "package.json", "tsconfig.json", "vite.config.*", "next.config.*");
                    break;
                case "python":
                    AddFiles(directory, keyFiles, known, "pyproject.toml", "requirements.txt", "setup.py", "manage.py", "main.py", "app.py");
                    break;
            }
        }

        return keyFiles.Take(15).ToList();
    }

    private static void AddFiles(string directory, List<string> keyFiles, HashSet<string> known, params string[] patterns)
    {
        foreach (var pattern in patterns)
        {
            if (pattern.Contains('*'))
            {
                var files = Directory.GetFiles(directory, pattern).Take(3);
                foreach (var file in files)
                {
                    var name = Path.GetFileName(file);
                    if (known.Add(name))
                        keyFiles.Add(name);
                }
            }
            else
            {
                var path = Path.Combine(directory, pattern);
                if (File.Exists(path) && known.Add(pattern))
                    keyFiles.Add(pattern);
            }
        }
    }
}

using System.Text.RegularExpressions;

namespace PokeChat.Tools;

public class FileOpsTool : ITool
{
    public string Name => "file_ops";
    public string Description => "Reads, writes, lists, or searches files within allowed directories";

    private static readonly HashSet<string> AllowedWriteExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".md", ".json", ".xml", ".yaml", ".yml", ".csv", ".log",
        ".cs", ".csproj", ".slnx", ".props", ".targets",
        ".html", ".css", ".js", ".ts", ".sh", ".ps1", ".py", ".rb"
    };

    private const int MaxReadBytes = 100 * 1024;
    private const int MaxSearchResults = 20;

    private readonly List<string> _allowedPaths;

    public FileOpsTool(IEnumerable<string>? allowedPaths = null)
    {
        _allowedPaths = allowedPaths?.ToList() ?? new List<string>();
        if (_allowedPaths.Count == 0)
        {
            var root = Data.ProjectPathHelper.FindProjectRoot(AppContext.BaseDirectory);
            if (root != null)
                _allowedPaths.Add(root);
        }
    }

    public ToolResult Execute(string[] args)
    {
        if (args.Length == 0 || string.IsNullOrWhiteSpace(args[0]))
            return new ToolResult { Success = false, ErrorMessage = "No command provided. Use: read, write, list, or search." };

        var command = args[0].Trim().ToLowerInvariant();
        return command switch
        {
            "read" => HandleRead(args),
            "write" => HandleWrite(args),
            "list" => HandleList(args),
            "search" => HandleSearch(args),
            _ => new ToolResult { Success = false, ErrorMessage = $"Unknown command '{command}'. Use: read, write, list, or search." }
        };
    }

    private ToolResult HandleRead(string[] args)
    {
        if (args.Length < 2 || string.IsNullOrWhiteSpace(args[1]))
            return new ToolResult { Success = false, ErrorMessage = "No file path provided. Usage: read <path>" };

        var path = ResolvePath(args[1]);
        if (path == null)
            return new ToolResult { Success = false, ErrorMessage = "Access denied: path is outside allowed directories." };

        if (!File.Exists(path))
            return new ToolResult { Success = false, ErrorMessage = $"File not found: {args[1]}" };

        try
        {
            var fileInfo = new FileInfo(path);
            if (fileInfo.Length > MaxReadBytes)
                return new ToolResult { Success = false, ErrorMessage = $"File is too large ({fileInfo.Length / 1024}KB). Maximum is {MaxReadBytes / 1024}KB." };

            var content = File.ReadAllText(path);
            return new ToolResult { Success = true, Output = content };
        }
        catch (Exception ex)
        {
            return new ToolResult { Success = false, ErrorMessage = $"Error reading file: {ex.Message}" };
        }
    }

    private ToolResult HandleWrite(string[] args)
    {
        if (args.Length < 3 || string.IsNullOrWhiteSpace(args[1]) || string.IsNullOrWhiteSpace(args[2]))
            return new ToolResult { Success = false, ErrorMessage = "Missing arguments. Usage: write <path> <content>" };

        var path = ResolvePath(args[1]);
        if (path == null)
            return new ToolResult { Success = false, ErrorMessage = "Access denied: path is outside allowed directories." };

        var ext = Path.GetExtension(path);
        if (!AllowedWriteExtensions.Contains(ext))
            return new ToolResult { Success = false, ErrorMessage = $"Writing .{ext} files is not allowed. Allowed: {string.Join(", ", AllowedWriteExtensions)}" };

        try
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(path, args[2]);
            return new ToolResult { Success = true, Output = $"Written {new FileInfo(path).Length} bytes to {args[1]}" };
        }
        catch (Exception ex)
        {
            return new ToolResult { Success = false, ErrorMessage = $"Error writing file: {ex.Message}" };
        }
    }

    private ToolResult HandleList(string[] args)
    {
        var dirPath = args.Length > 1 && !string.IsNullOrWhiteSpace(args[1]) ? args[1] : ".";

        var resolved = ResolvePath(dirPath);
        if (resolved == null)
            return new ToolResult { Success = false, ErrorMessage = "Access denied: path is outside allowed directories." };

        if (!Directory.Exists(resolved))
            return new ToolResult { Success = false, ErrorMessage = $"Directory not found: {dirPath}" };

        try
        {
            var entries = Directory.GetFileSystemEntries(resolved)
                .Select(e => new
                {
                    Name = Path.GetFileName(e),
                    IsDir = Directory.Exists(e)
                })
                .OrderByDescending(e => e.IsDir)
                .ThenBy(e => e.Name)
                .ToList();

            if (entries.Count == 0)
                return new ToolResult { Success = true, Output = $"(empty directory)" };

            var lines = entries.Select(e => e.IsDir ? $"  {e.Name}/" : $"  {e.Name}");
            return new ToolResult { Success = true, Output = string.Join("\n", lines) };
        }
        catch (Exception ex)
        {
            return new ToolResult { Success = false, ErrorMessage = $"Error listing directory: {ex.Message}" };
        }
    }

    private ToolResult HandleSearch(string[] args)
    {
        if (args.Length < 3 || string.IsNullOrWhiteSpace(args[1]) || string.IsNullOrWhiteSpace(args[2]))
            return new ToolResult { Success = false, ErrorMessage = "Missing arguments. Usage: search <dir> <pattern>" };

        var resolved = ResolvePath(args[1]);
        if (resolved == null)
            return new ToolResult { Success = false, ErrorMessage = "Access denied: path is outside allowed directories." };

        if (!Directory.Exists(resolved))
            return new ToolResult { Success = false, ErrorMessage = $"Directory not found: {args[1]}" };

        try
        {
            var pattern = args[2];
            var regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
            var results = new List<string>();
            var files = Directory.GetFiles(resolved, "*", SearchOption.AllDirectories);

            foreach (var file in files)
            {
                if (results.Count >= MaxSearchResults) break;

                try
                {
                    var lines = File.ReadLines(file);
                    int lineNum = 0;
                    foreach (var line in lines)
                    {
                        lineNum++;
                        if (results.Count >= MaxSearchResults) break;
                        if (regex.IsMatch(line))
                        {
                            var relative = Path.GetRelativePath(resolved, file);
                            results.Add($"{relative}:{lineNum}: {line.Trim()}");
                        }
                    }
                }
                catch
                {
                }
            }

            if (results.Count == 0)
                return new ToolResult { Success = true, Output = $"No matches found for pattern '{pattern}'." };

            return new ToolResult { Success = true, Output = string.Join("\n", results.Take(MaxSearchResults)) };
        }
        catch (RegexParseException)
        {
            return new ToolResult { Success = false, ErrorMessage = $"Invalid regex pattern: {args[2]}" };
        }
        catch (Exception ex)
        {
            return new ToolResult { Success = false, ErrorMessage = $"Error searching: {ex.Message}" };
        }
    }

    private string? ResolvePath(string input)
    {
        try
        {
            var fullPath = Path.GetFullPath(input, Directory.GetCurrentDirectory());

            if (File.Exists(fullPath))
                fullPath = Path.GetFullPath(Path.GetDirectoryName(fullPath) ?? fullPath, Directory.GetCurrentDirectory());
            else if (Directory.Exists(fullPath))
                fullPath = Path.GetFullPath(fullPath);

            if (_allowedPaths.Count == 0)
                return Path.GetFullPath(input, Directory.GetCurrentDirectory());

            foreach (var allowed in _allowedPaths)
            {
                var allowedFull = Path.GetFullPath(allowed);
                if (fullPath.StartsWith(allowedFull, StringComparison.OrdinalIgnoreCase))
                    return Path.GetFullPath(input, Directory.GetCurrentDirectory());
            }

            return null;
        }
        catch
        {
            return null;
        }
    }
}

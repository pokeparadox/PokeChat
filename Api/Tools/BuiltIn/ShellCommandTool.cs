using System.Diagnostics;
using System.Text.RegularExpressions;

namespace PokeChat.Tools;

public class ShellCommandTool : ITool
{
    public string Name => "shell_command";
    public string Description => "Executes a whitelisted shell command";

    private static readonly HashSet<string> DefaultAllowedCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "ls", "pwd", "whoami", "date", "uptime", "uname", "echo", "cat", "wc", "du", "df", "which", "env",
        "dotnet", "git", "docker", "npm", "npx",
        "grep", "kill", "lsof", "zip", "unzip", "tree", "ip", "free",
        "pip", "python", "cargo", "rustc", "go", "node", "java", "javac",
        "ssh", "scp", "rsync", "curl", "wget"
    };

    private static readonly Regex DangerousChars = new(@"[;&|`$()<>\n\r]", RegexOptions.Compiled);

    private readonly HashSet<string> _allowedCommands;
    private readonly HashSet<string> _tempAllowed = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTime> _tempExpiry = new(StringComparer.OrdinalIgnoreCase);

    public ShellCommandTool(IEnumerable<string>? allowedCommands = null)
    {
        _allowedCommands = allowedCommands != null
            ? new HashSet<string>(allowedCommands, StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(DefaultAllowedCommands, StringComparer.OrdinalIgnoreCase);
    }

    public bool IsCommandAllowed(string command)
    {
        return _allowedCommands.Contains(command) || _tempAllowed.Contains(command);
    }

    public void TempAllow(string command, TimeSpan duration)
    {
        _tempAllowed.Add(command);
        _tempExpiry[command] = DateTime.UtcNow + duration;
    }

    public void PermAllow(string command)
    {
        _allowedCommands.Add(command);
        _tempAllowed.Remove(command);
        _tempExpiry.Remove(command);
    }

    private void PruneExpiredTemp()
    {
        var now = DateTime.UtcNow;
        var expired = _tempExpiry.Where(kvp => kvp.Value <= now).Select(kvp => kvp.Key).ToList();
        foreach (var cmd in expired)
        {
            _tempAllowed.Remove(cmd);
            _tempExpiry.Remove(cmd);
        }
    }

    public ToolResult Execute(string[] args)
    {
        if (args.Length == 0 || string.IsNullOrWhiteSpace(args[0]))
            return new ToolResult { Success = false, ErrorMessage = "No command provided." };

        PruneExpiredTemp();

        var command = args[0].Trim();
        if (!IsCommandAllowed(command))
        {
            return new ToolResult
            {
                Success = false,
                ErrorMessage = $"Command '{command}' is not in the allowed list.",
                IsBlocked = true,
                BlockedCommand = command
            };
        }

        var commandArgs = args.Length > 1 ? string.Join(" ", args.Skip(1)) : "";

        if (!string.IsNullOrEmpty(commandArgs) && DangerousChars.IsMatch(commandArgs))
            return new ToolResult { Success = false, ErrorMessage = "Command arguments contain prohibited characters." };

        try
        {
            var psi = new ProcessStartInfo(command, commandArgs)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = psi };
            process.Start();

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                var msg = string.IsNullOrWhiteSpace(error) ? $"Command exited with code {process.ExitCode}." : error.Trim();
                return new ToolResult { Success = false, ErrorMessage = msg };
            }

            var result = output.Trim();
            if (string.IsNullOrEmpty(result))
                result = $"Command '{command}' completed successfully (no output).";

            return new ToolResult { Success = true, Output = result };
        }
        catch (Exception ex)
        {
            return new ToolResult { Success = false, ErrorMessage = ex.Message };
        }
    }
}

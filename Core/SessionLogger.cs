using PokeChat.Data;

namespace PokeChat.Core;

public class SessionLogger : IDisposable
{
    private readonly string _sessionId;
    private readonly bool _verbose;
    private readonly int _maxLogs;
    private readonly string? _logPath;
    private readonly StreamWriter? _writer;
    private readonly bool _enabled;
    private int _turnCount;

    public SessionLogger(string sessionId, SessionLogConfig? config = null, string? logDirOverride = null)
    {
        _sessionId = sessionId;
        config ??= SessionLogConfig.Load();
        _enabled = config.Enabled;
        _verbose = config.IsVerbose;
        _maxLogs = config.MaxLogFiles;

        if (!_enabled)
            return;

        var logDir = logDirOverride ?? ResolveLogDir(config.Directory);
        Directory.CreateDirectory(logDir);

        var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
        _logPath = Path.Combine(logDir, $"session_{_sessionId}_{timestamp}.log");
        _writer = new StreamWriter(_logPath, append: true) { AutoFlush = true };

        WriteHeader();
        RotateLogs(logDir, _maxLogs);
    }

    public string? LogPath => _logPath;
    public bool Verbose => _verbose;
    public bool Enabled => _enabled;

    public void LogTurn(string userInput, string botResponse, Dictionary<string, string>? contextData = null)
    {
        if (!_enabled || _writer == null) return;

        _turnCount++;
        var timestamp = DateTime.UtcNow.ToString("O");

        _writer.WriteLine($"## Turn {_turnCount}");
        _writer.WriteLine($"- Timestamp: {timestamp}");
        _writer.WriteLine($"- Session: {_sessionId}");
        _writer.WriteLine();
        _writer.WriteLine("### User");
        _writer.WriteLine(userInput);
        _writer.WriteLine();
        _writer.WriteLine("### Bot");
        _writer.WriteLine(botResponse);
        _writer.WriteLine();

        if (_verbose && contextData != null && contextData.Count > 0)
        {
            _writer.WriteLine("### Context");
            foreach (var kvp in contextData)
            {
                _writer.WriteLine($"- {kvp.Key}: {kvp.Value}");
            }
            _writer.WriteLine();
        }

        _writer.WriteLine("---");
        _writer.WriteLine();
    }

    public void LogSystem(string message)
    {
        if (!_enabled || _writer == null || string.IsNullOrEmpty(message)) return;

        var timestamp = DateTime.UtcNow.ToString("O");
        _writer.WriteLine("## System");
        _writer.WriteLine($"- Timestamp: {timestamp}");
        _writer.WriteLine($"- Session: {_sessionId}");
        _writer.WriteLine();
        _writer.WriteLine("### Bot");
        _writer.WriteLine(message);
        _writer.WriteLine();
        _writer.WriteLine("---");
        _writer.WriteLine();
    }

    private void WriteHeader()
    {
        if (_writer == null) return;

        _writer.WriteLine($"# Chat Session Log");
        _writer.WriteLine($"- Session ID: {_sessionId}");
        _writer.WriteLine($"- Started: {DateTime.UtcNow:O}");
        _writer.WriteLine($"- Mode: {(_verbose ? "Verbose" : "Basic")}");
        _writer.WriteLine($"---");
        _writer.WriteLine();
    }

    private static void RotateLogs(string logDir, int maxLogs)
    {
        var logFiles = Directory.GetFiles(logDir, "session_*.log")
            .OrderBy(f => File.GetCreationTimeUtc(f))
            .ToList();

        while (logFiles.Count > maxLogs)
        {
            var oldest = logFiles[0];
            try { File.Delete(oldest); }
            catch { /* best effort */ }
            logFiles.RemoveAt(0);
        }
    }

    private static string ResolveLogDir(string directory)
    {
        var root = ProjectPathHelper.FindProjectRoot(AppContext.BaseDirectory);
        return root != null ? Path.Combine(root, directory) : Path.Combine(AppContext.BaseDirectory, directory);
    }

    public void Dispose()
    {
        if (_enabled && _writer != null)
        {
            var timestamp = DateTime.UtcNow.ToString("O");
            _writer.WriteLine("## Session Ended");
            _writer.WriteLine($"- Timestamp: {timestamp}");
            _writer.WriteLine();
        }
        _writer?.Dispose();
    }
}

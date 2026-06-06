using PokeChat.Data;

namespace PokeChat.Core;

public class SessionLogger : IDisposable
{
    private readonly string _sessionId;
    private readonly bool _verbose;
    private readonly int _maxLogs;
    private readonly string _logDir;
    private readonly string _logPath;
    private readonly StreamWriter _writer;
    private int _turnCount;

    public SessionLogger(string sessionId, bool? verbose = null, int? maxLogs = null, string? logDirOverride = null)
    {
        _sessionId = sessionId;
        _verbose = verbose ?? Environment.GetEnvironmentVariable("POKECHAT_VERBOSE_LOG")?.ToLowerInvariant() == "true";
        _maxLogs = maxLogs ?? (int.TryParse(Environment.GetEnvironmentVariable("POKECHAT_LOG_RETENTION"), out var r) ? r : 50);

        _logDir = logDirOverride ?? ResolveLogDir();
        Directory.CreateDirectory(_logDir);

        var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
        _logPath = Path.Combine(_logDir, $"session_{_sessionId}_{timestamp}.log");
        _writer = new StreamWriter(_logPath, append: true) { AutoFlush = true };

        WriteHeader();
        RotateLogs();
    }

    public string LogPath => _logPath;
    public string LogDirectory => _logDir;
    public bool Verbose => _verbose;

    public void LogTurn(string userInput, string botResponse, Dictionary<string, string>? contextData = null)
    {
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

    private void WriteHeader()
    {
        _writer.WriteLine($"# Chat Session Log");
        _writer.WriteLine($"- Session ID: {_sessionId}");
        _writer.WriteLine($"- Started: {DateTime.UtcNow:O}");
        _writer.WriteLine($"- Mode: {(_verbose ? "Verbose" : "Basic")}");
        _writer.WriteLine($"---");
        _writer.WriteLine();
    }

    private void RotateLogs()
    {
        var logFiles = Directory.GetFiles(_logDir, "session_*.log")
            .OrderBy(f => File.GetCreationTimeUtc(f))
            .ToList();

        while (logFiles.Count > _maxLogs)
        {
            var oldest = logFiles[0];
            try { File.Delete(oldest); }
            catch { /* best effort */ }
            logFiles.RemoveAt(0);
        }
    }

    private static string ResolveLogDir()
    {
        var root = ProjectPathHelper.FindProjectRoot(AppContext.BaseDirectory);
        return root != null ? Path.Combine(root, "logs") : Path.Combine(AppContext.BaseDirectory, "logs");
    }

    public void Dispose()
    {
        _writer?.Dispose();
    }
}

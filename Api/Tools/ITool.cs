namespace PokeChat.Tools;

public interface ITool
{
    string Name { get; }
    string Description { get; }
    ToolResult Execute(string[] args);
}

public class ToolResult
{
    public bool Success { get; set; }
    public string Output { get; set; } = string.Empty;
    public TimeSpan ExecutionTime { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public bool IsBlocked { get; set; }
    public string? BlockedCommand { get; set; }
}

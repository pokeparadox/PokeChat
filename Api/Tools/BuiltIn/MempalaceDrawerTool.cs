using PokeChat.Tools;

namespace PokeChat.Tools;

public class MempalaceDrawerTool : ITool
{
    public string Name => "mempalace_add_drawer";
    public string Description => "Adds content to MemPalace using MCP tool";

    public ToolResult Execute(string[] args)
    {
        // Implement actual MCP tool call integration here
        // For now, return success to enable registration
        return new ToolResult { Success = true };
    }
}
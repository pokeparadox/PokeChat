namespace PokeChat.Mcp;

public static class McpAutoTriggers
{
    public static McpToolTrigger GenerateCatchAll(string toolName)
    {
        var escapedName = System.Text.RegularExpressions.Regex.Escape(toolName);
        return new McpToolTrigger
        {
            Pattern = $@"(use|call|run|execute) (the )?({escapedName}) for (.+)",
            InputType = "Statement",
            Responses = new List<string>
            {
                $"Running tool. {{tool:{toolName}:{{$4}}}}"
            }
        };
    }
}

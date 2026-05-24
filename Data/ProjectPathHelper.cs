namespace PokeChat.Data;

internal static class ProjectPathHelper
{
    public static string? FindProjectRoot(string startDirectory)
    {
        var current = startDirectory;
        while (!string.IsNullOrEmpty(current))
        {
            if (File.Exists(Path.Combine(current, "PokeChat.csproj")))
                return current;
            current = Path.GetDirectoryName(current);
        }
        return null;
    }
}

using System.Net.Http;
using System.Text.Json;

namespace PokeChat.Tools;

public class WebSearchTool : ITool
{
    public string Name => "web_search";
    public string Description => "Searches the web via a configurable endpoint";

    public ToolResult Execute(string[] args)
    {
        var query = string.Join(" ", args);
        if (string.IsNullOrWhiteSpace(query))
        {
            return new ToolResult
            {
                Success = false,
                ErrorMessage = "No search query provided"
            };
        }

        try
        {
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(10);
            var encoded = Uri.EscapeDataString(query);
            var endpoint = $"https://api.duckduckgo.com/?q={encoded}&format=json";

            var response = client.GetAsync(endpoint).Result;
            response.EnsureSuccessStatusCode();
            var json = response.Content.ReadAsStringAsync().Result;

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var snippet = "";

            if (root.TryGetProperty("AbstractText", out var abs) && abs.GetString() is { Length: > 0 } absText)
                snippet = absText;
            else if (root.TryGetProperty("RelatedTopics", out var topics) && topics.GetArrayLength() > 0)
            {
                foreach (var topic in topics.EnumerateArray())
                {
                    if (topic.TryGetProperty("Text", out var text) && text.GetString() is { Length: > 0 } topicText)
                    {
                        snippet = topicText;
                        break;
                    }
                }
            }

            if (string.IsNullOrEmpty(snippet))
                snippet = $"I found some results for '{query}' but no snippet was available.";

            return new ToolResult
            {
                Success = true,
                Output = snippet
            };
        }
        catch (Exception ex)
        {
            return new ToolResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }
}

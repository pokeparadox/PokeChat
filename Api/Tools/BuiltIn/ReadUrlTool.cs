using System.Net.Http;
using System.Text.RegularExpressions;

namespace PokeChat.Tools;

public class ReadUrlTool : ITool
{
    public string Name => "read_url";
    public string Description => "Fetches plaintext content from a URL";

    public ToolResult Execute(string[] args)
    {
        var url = string.Join("", args);
        if (string.IsNullOrWhiteSpace(url))
        {
            return new ToolResult
            {
                Success = false,
                ErrorMessage = "No URL provided"
            };
        }

        if (!url.StartsWith("http://") && !url.StartsWith("https://"))
            url = "https://" + url;

        try
        {
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(15);
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (compatible; PokeChatBot/1.0)");

            var response = client.GetAsync(url).Result;
            response.EnsureSuccessStatusCode();
            var html = response.Content.ReadAsStringAsync().Result;

            var text = Regex.Replace(html, @"<[^>]+>", " ");
            text = Regex.Replace(text, @"\s+", " ");
            text = text.Trim();

            var maxLength = 1000;
            if (text.Length > maxLength)
                text = text[..maxLength] + "...";

            return new ToolResult
            {
                Success = true,
                Output = text
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

using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace PokeChat.LLM;

public class OllamaProvider : ILLMProvider, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _model;
    private readonly int _timeoutMs;

    public OllamaProvider(string endpoint, string model, int timeoutMs = 30000)
    {
        _httpClient = new HttpClient();
        _httpClient.BaseAddress = new Uri(endpoint.TrimEnd('/') + "/");
        _model = model;
        _timeoutMs = timeoutMs;
    }

    public string? GenerateResponse(string userInput, string? systemPrompt = null)
    {
        var messages = new List<object>();
        if (!string.IsNullOrEmpty(systemPrompt))
            messages.Add(new { role = "system", content = systemPrompt });
        messages.Add(new { role = "user", content = userInput });

        var requestBody = new
        {
            model = _model,
            messages,
            stream = false
        };

        try
        {
            using var cts = new CancellationTokenSource(_timeoutMs);
            var response = _httpClient.PostAsJsonAsync("v1/chat/completions", requestBody, cts.Token)
                .Result;
            response.EnsureSuccessStatusCode();
            var json = response.Content.ReadFromJsonAsync<OllamaChatResponse>(cts.Token).Result;
            return json?.Choices?.FirstOrDefault()?.Message?.Content;
        }
        catch
        {
            return null;
        }
    }

    public void Dispose() => _httpClient.Dispose();
}

internal class OllamaChatResponse
{
    [JsonPropertyName("choices")]
    public List<OllamaChoice>? Choices { get; set; }
}

internal class OllamaChoice
{
    [JsonPropertyName("message")]
    public OllamaMessage? Message { get; set; }
}

internal class OllamaMessage
{
    [JsonPropertyName("content")]
    public string? Content { get; set; }
}

using System.Net.Http.Json;
using System.Text.Json;
using PokeChat.Api.Models;

namespace PokeChat.Api.Services;

public class UpstreamLLMClient
{
    private readonly HttpClient _http;
    private readonly UpstreamOptions _options;

    public UpstreamLLMClient(HttpClient http, UpstreamOptions options)
    {
        _http = http;
        _options = options;
    }

    public async Task<ChatCompletionResponse?> ForwardAsync(ChatCompletionRequest request, CancellationToken ct = default)
    {
        if (!_options.Enabled)
            return null;

        var upstreamRequest = new
        {
            model = _options.Model,
            messages = request.Messages,
            temperature = request.Temperature,
            max_tokens = request.MaxTokens ?? request.MaxCompletionTokens,
            stream = false
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, _options.Endpoint)
        {
            Content = JsonContent.Create(upstreamRequest)
        };

        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
            httpRequest.Headers.TryAddWithoutValidation("Authorization", $"Bearer {_options.ApiKey}");

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(_options.TimeoutMs);

            var response = await _http.SendAsync(httpRequest, cts.Token);
            response.EnsureSuccessStatusCode();

            var upstreamResponse = await response.Content.ReadFromJsonAsync<ChatCompletionResponse>(
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, cts.Token);

            if (upstreamResponse != null)
            {
                upstreamResponse.Model = _options.Model;
                upstreamResponse.RouteInfo = new RouteInfo
                {
                    Category = "upstream_llm",
                    EngineHandled = false,
                    Description = $"routed to upstream LLM ({_options.Model})"
                };
            }

            return upstreamResponse;
        }
        catch
        {
            return null;
        }
    }
}

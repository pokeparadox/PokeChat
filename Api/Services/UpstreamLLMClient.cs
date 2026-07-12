using System.Net.Http.Json;
using System.Text;
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

    public async Task<bool> ForwardStreamingAsync(ChatCompletionRequest request,
        Func<ChatCompletionChunk, Task> onChunk, Func<Task> onDone, CancellationToken ct = default)
    {
        if (!_options.Enabled)
            return false;

        var upstreamRequest = new
        {
            model = _options.Model,
            messages = request.Messages,
            temperature = request.Temperature,
            max_tokens = request.MaxTokens ?? request.MaxCompletionTokens,
            stream = true
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

            var response = await _http.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cts.Token);
            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
            using var reader = new StreamReader(stream, Encoding.UTF8);

            while (true)
            {
                cts.Token.ThrowIfCancellationRequested();

                var line = await reader.ReadLineAsync(cts.Token);
                if (line == null)
                    break;
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                if (line.StartsWith(":"))
                    continue;

                if (line == "data: [DONE]")
                    break;

                if (!line.StartsWith("data: "))
                    continue;

                var json = line[6..];
                ChatCompletionChunk? chunk;
                try
                {
                    chunk = JsonSerializer.Deserialize<ChatCompletionChunk>(json,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }
                catch
                {
                    continue;
                }

                if (chunk == null)
                    continue;

                chunk.Model = _options.Model;

                await onChunk(chunk);
            }

            await onDone();
            return true;
        }
        catch
        {
            return false;
        }
    }
}

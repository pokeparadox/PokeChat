using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using PokeChat.Api.Models;

namespace PokeChat.Api.Services;

public class UpstreamLLMClient
{
    private readonly HttpClient _http;
    private readonly UpstreamOptions _options;
    private const int MaxRetries = 3;
    private static readonly int[] RetryDelaysMs = [1000, 2000, 4000];

    public UpstreamLLMClient(HttpClient http, UpstreamOptions options)
    {
        _http = http;
        _options = options;
    }

    public async Task<ChatCompletionResponse?> ForwardAsync(ChatCompletionRequest request, CancellationToken ct = default)
    {
        if (!_options.Enabled)
            return null;

        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            var upstreamRequest = BuildUpstreamBody(request, stream: false);

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
                if (attempt < MaxRetries - 1)
                    await Task.Delay(RetryDelaysMs[attempt], ct);
            }
        }

        return null;
    }

    public async Task<bool> ForwardStreamingAsync(ChatCompletionRequest request,
        Func<ChatCompletionChunk, Task> onChunk, Func<Task> onDone, CancellationToken ct = default)
    {
        if (!_options.Enabled)
            return false;

        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            var upstreamRequest = BuildUpstreamBody(request, stream: true);

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
                if (attempt < MaxRetries - 1)
                    await Task.Delay(RetryDelaysMs[attempt], ct);
            }
        }

        return false;
    }

    private object BuildUpstreamBody(ChatCompletionRequest request, bool stream)
    {
        var messages = new List<object?>(request.Messages);

        var hasSystemMessage = messages.Any(m =>
        {
            if (m is ChatMessage cm)
                return string.Equals(cm.Role, "system", StringComparison.OrdinalIgnoreCase);
            return false;
        });

        if (!hasSystemMessage)
        {
            var defaultSystem = request.Tools?.Count > 0
                ? "You are a helpful coding assistant. Use the provided tools when the user asks for file operations or shell commands."
                : "You are a helpful coding assistant with access to file and shell tools. " +
                    "When the user asks you to read, open, show, update, edit, improve, or work on a file, " +
                    "respond with a tool marker in this exact format: {tool:file_ops:read:FILENAME} to read a file, " +
                    "{tool:file_ops:write:FILENAME:CONTENT} to write a file, " +
                    "{tool:file_ops:list:PATH} to list files, " +
                    "{tool:file_ops:search:PATH:QUERY} to search in files, " +
                    "{tool:shell_command:COMMAND:ARGS} to run a shell command. " +
                    "Use the file_ops tool for file operations and shell_command for running commands. " +
                    "Always use tool markers when the user asks you to perform file or shell operations.";

            messages.Insert(0, new ChatMessage
            {
                Role = "system",
                Content = defaultSystem
            });
        }

        var body = new Dictionary<string, object?>
        {
            ["model"] = _options.Model,
            ["messages"] = messages,
            ["temperature"] = request.Temperature,
            ["stream"] = stream
        };

        if (request.Tools?.Count > 0)
            body["tools"] = request.Tools;

        var effectiveMaxTokens = request.MaxTokens ?? request.MaxCompletionTokens;
        if (effectiveMaxTokens != null)
            body["max_tokens"] = effectiveMaxTokens;

        if (request.Seed != null)
            body["seed"] = request.Seed;

        var stops = OpenAIAdapter.NormalizeStopArray(request.Stop);
        if (stops.Length > 0)
            body["stop"] = stops.Length == 1 ? stops[0] : stops;

        return body;
    }
}

using System.Text.Json;
using PokeChat.Api.Models;
using PokeChat.Core;

namespace PokeChat.Api.Services;

public class OpenAIAdapter
{
    private readonly SessionManager _sessionManager;
    private readonly UpstreamLLMClient? _upstream;

    public OpenAIAdapter(SessionManager sessionManager, UpstreamLLMClient? upstream = null)
    {
        _sessionManager = sessionManager;
        _upstream = upstream;
    }

    public async Task<ChatCompletionResponse> ProcessAsync(ChatCompletionRequest request, string sessionId)
    {
        var engine = _sessionManager.GetOrCreate(sessionId, messages: request.Messages);

        var userMessage = request.Messages.LastOrDefault(m => m.Role == "user");
        var input = userMessage?.Content ?? "";

        var responseText = engine.ProcessInput(input);

        var engineHandled = !engine.LastResponseIsDeadEnd;
        var routeInfo = new RouteInfo
        {
            Category = engine.LastResponseCategory,
            EngineHandled = engineHandled,
            Description = DescribeRoute(engine.LastResponseCategory)
        };

        if (!engineHandled && _upstream != null)
        {
            var upstreamResult = await _upstream.ForwardAsync(request);
            if (upstreamResult != null)
            {
                upstreamResult.RouteInfo = new RouteInfo
                {
                    Category = "upstream_llm",
                    EngineHandled = false,
                    Description = $"routed to upstream LLM"
                };
                return upstreamResult;
            }

            routeInfo.Description = "engine dead-end, upstream unavailable (returning engine fallback)";
        }

        return new ChatCompletionResponse
        {
            Model = request.Model,
            Choices =
            [
                new ChatCompletionChoice
                {
                    Index = 0,
                    Message = new ChatMessage { Role = "assistant", Content = responseText },
                    FinishReason = "stop"
                }
            ],
            Usage = new Usage
            {
                PromptTokens = request.Messages.Sum(m => (m.Content?.Length ?? 0) / 4),
                CompletionTokens = responseText.Length / 4,
                TotalTokens = (request.Messages.Sum(m => (m.Content?.Length ?? 0)) + responseText.Length) / 4
            },
            RouteInfo = routeInfo
        };
    }

    public async Task StreamResponseAsync(ChatCompletionRequest request, string sessionId,
        Func<ChatCompletionChunk, Task> onChunk, Func<Task> onDone)
    {
        var engine = _sessionManager.GetOrCreate(sessionId, messages: request.Messages);

        var userMessage = request.Messages.LastOrDefault(m => m.Role == "user");
        var input = userMessage?.Content ?? "";

        var responseText = engine.ProcessInput(input);

        var chunkId = $"chatcmpl-{Guid.NewGuid().ToString("N")[..12]}";
        var created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // Send role chunk
        await onChunk(new ChatCompletionChunk
        {
            Id = chunkId,
            Created = created,
            Model = request.Model,
            Choices = [new ChunkChoice { Index = 0, Delta = new Delta { Role = "assistant" } }]
        });

        // Send content chunks word by word
        if (responseText.Length > 0)
        {
            var words = responseText.Split(' ');
            for (var i = 0; i < words.Length; i++)
            {
                var content = i < words.Length - 1 ? words[i] + " " : words[i];
                await onChunk(new ChatCompletionChunk
                {
                    Id = chunkId,
                    Created = created,
                    Model = request.Model,
                    Choices = [new ChunkChoice { Index = 0, Delta = new Delta { Content = content } }]
                });
            }
        }

        // Send final chunk with finish_reason
        await onChunk(new ChatCompletionChunk
        {
            Id = chunkId,
            Created = created,
            Model = request.Model,
            Choices =
            [
                new ChunkChoice
                {
                    Index = 0,
                    Delta = new Delta(),
                    FinishReason = "stop"
                }
            ],
            Usage = new Usage
            {
                PromptTokens = request.Messages.Sum(m => m.Content?.Length ?? 0) / 4,
                CompletionTokens = responseText.Length / 4,
                TotalTokens = ((request.Messages.Sum(m => m.Content?.Length ?? 0)) + responseText.Length) / 4
            }
        });

        await onDone();
    }

    private static string? DescribeRoute(string? category)
    {
        return category switch
        {
            "rule_match" => "matched response rule",
            "math_result" => "evaluated math expression",
            "greeting" => "greeting response",
            "llm_response" => "forwarded to internal LLM",
            "llm_unavailable" => "internal LLM unavailable, used fallback",
            null => null,
            _ => category?.StartsWith("proactive_") == true ? "proactive question (low confidence)" : "engine response"
        };
    }
}

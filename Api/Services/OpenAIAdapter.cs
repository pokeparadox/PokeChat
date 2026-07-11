using System.Text.Json;
using PokeChat.Api.Models;
using PokeChat.Core;

namespace PokeChat.Api.Services;

public class OpenAIAdapter
{
    private readonly SessionManager _sessionManager;
    private readonly UpstreamLLMClient? _upstream;
    private readonly ITokenBucketStore _tokenBucket;
    private readonly TokenBucketOptions _tokenOptions;

    public OpenAIAdapter(SessionManager sessionManager, ITokenBucketStore tokenBucket, TokenBucketOptions tokenOptions, UpstreamLLMClient? upstream = null)
    {
        _sessionManager = sessionManager;
        _tokenBucket = tokenBucket;
        _tokenOptions = tokenOptions;
        _upstream = upstream;
    }

    public async Task<ChatCompletionResponse> ProcessAsync(ChatCompletionRequest request, string sessionId, string persona = "chat", string? rateLimitKey = null)
    {
        var engine = _sessionManager.GetOrCreate(sessionId, userName: request.User, messages: request.Messages, persona: persona);

        var userMessage = request.Messages.LastOrDefault(m => m.Role == "user");
        var input = userMessage?.Content ?? "";

        var rateKey = rateLimitKey ?? "unknown";
        var nlpAllowed = _tokenBucket.TryDeduct(rateKey, _tokenOptions.NlpCost);

        if (!nlpAllowed)
        {
            return RateLimitedResponse(request, _tokenOptions.NlpCost, rateKey);
        }

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
            var upstreamAllowed = _tokenBucket.TryDeduct(rateKey, _tokenOptions.UpstreamCost);

            if (!upstreamAllowed)
            {
                routeInfo.Category = "upstream_rate_limited";
                routeInfo.Description = "upstream LLM skipped — insufficient tokens";
            }
            else
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
                    AddRateLimitHeaders(upstreamResult, rateKey);
                    return upstreamResult;
                }

                routeInfo.Description = "engine dead-end, upstream unavailable (returning engine fallback)";
            }
        }

        var response = new ChatCompletionResponse
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

        AddRateLimitHeaders(response, rateKey);
        return response;
    }

    public async Task StreamResponseAsync(ChatCompletionRequest request, string sessionId,
        Func<ChatCompletionChunk, Task> onChunk, Func<Task> onDone, string persona = "chat", string? rateLimitKey = null)
    {
        var engine = _sessionManager.GetOrCreate(sessionId, userName: request.User, messages: request.Messages, persona: persona);

        var userMessage = request.Messages.LastOrDefault(m => m.Role == "user");
        var input = userMessage?.Content ?? "";

        var rateKey = rateLimitKey ?? "unknown";
        var nlpAllowed = _tokenBucket.TryDeduct(rateKey, _tokenOptions.StreamNlpCost);

        if (!nlpAllowed)
        {
            var errChunkId = $"chatcmpl-{Guid.NewGuid().ToString("N")[..12]}";
            var errCreated = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            await onChunk(new ChatCompletionChunk
            {
                Id = errChunkId, Created = errCreated, Model = request.Model,
                Choices = [new ChunkChoice { Index = 0, Delta = new Delta { Role = "assistant" } }]
            });
            await onChunk(new ChatCompletionChunk
            {
                Id = errChunkId, Created = errCreated, Model = request.Model,
                Choices = [new ChunkChoice { Index = 0, Delta = new Delta { Content = "Rate limited — insufficient tokens. Try again shortly." } }]
            });
            await onChunk(new ChatCompletionChunk
            {
                Id = errChunkId, Created = errCreated, Model = request.Model,
                Choices = [new ChunkChoice { Index = 0, Delta = new Delta(), FinishReason = "stop" }]
            });
            await onDone();
            return;
        }

        var responseText = engine.ProcessInput(input);

        var engineHandled = !engine.LastResponseIsDeadEnd;

        if (!engineHandled && _upstream != null)
        {
            var upstreamAllowed = _tokenBucket.TryDeduct(rateKey, _tokenOptions.StreamUpstreamCost);
            if (upstreamAllowed)
            {
                var upstreamResult = await _upstream.ForwardAsync(request);
                if (upstreamResult?.Choices.Count > 0)
                {
                    responseText = upstreamResult.Choices[0].Message?.Content ?? responseText;
                }
            }
        }

        var chunkId = $"chatcmpl-{Guid.NewGuid().ToString("N")[..12]}";
        var created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        await onChunk(new ChatCompletionChunk
        {
            Id = chunkId, Created = created, Model = request.Model,
            Choices = [new ChunkChoice { Index = 0, Delta = new Delta { Role = "assistant" } }]
        });

        if (responseText.Length > 0)
        {
            var words = responseText.Split(' ');
            for (var i = 0; i < words.Length; i++)
            {
                var content = i < words.Length - 1 ? words[i] + " " : words[i];
                await onChunk(new ChatCompletionChunk
                {
                    Id = chunkId, Created = created, Model = request.Model,
                    Choices = [new ChunkChoice { Index = 0, Delta = new Delta { Content = content } }]
                });
            }
        }

        await onChunk(new ChatCompletionChunk
        {
            Id = chunkId, Created = created, Model = request.Model,
            Choices = [new ChunkChoice { Index = 0, Delta = new Delta(), FinishReason = "stop" }],
            Usage = new Usage
            {
                PromptTokens = request.Messages.Sum(m => m.Content?.Length ?? 0) / 4,
                CompletionTokens = responseText.Length / 4,
                TotalTokens = ((request.Messages.Sum(m => m.Content?.Length ?? 0)) + responseText.Length) / 4
            }
        });

        await onDone();
    }

    private ChatCompletionResponse RateLimitedResponse(ChatCompletionRequest request, int needed, string rateKey)
    {
        var response = new ChatCompletionResponse
        {
            Model = request.Model,
            Choices =
            [
                new ChatCompletionChoice
                {
                    Index = 0,
                    Message = new ChatMessage
                    {
                        Role = "assistant",
                        Content = $"I'm receiving too many requests. Please wait a moment and try again."
                    },
                    FinishReason = "stop"
                }
            ],
            Usage = new Usage(),
            RouteInfo = new RouteInfo
            {
                Category = "rate_limited",
                EngineHandled = false,
                Description = $"rate limited — needed {needed} tokens"
            }
        };

        AddRateLimitHeaders(response, rateKey);
        return response;
    }

    private void AddRateLimitHeaders(ChatCompletionResponse response, string rateKey)
    {
        response.RateLimitRemaining = _tokenBucket.GetRemaining(rateKey);
        response.RateLimitReset = _tokenBucket.GetResetSeconds(rateKey);
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

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using PokeChat.Api.Models;
using PokeChat.Core;

namespace PokeChat.Api.Services;

public class OpenAIAdapter
{
    private const int DefaultHistoryTurnCap = 10;
    private static readonly HashSet<string> RebuildSkipRoles = new(StringComparer.OrdinalIgnoreCase) { "system", "tool", "tool_call" };

    private readonly SessionManager _sessionManager;
    private readonly UpstreamLLMClient? _upstream;
    private readonly ITokenBucketStore _tokenBucket;
    private readonly TokenBucketOptions _tokenOptions;
    private readonly SessionQuotaOptions _quotas;

    public OpenAIAdapter(SessionManager sessionManager, ITokenBucketStore tokenBucket, TokenBucketOptions tokenOptions, SessionQuotaOptions quotas, UpstreamLLMClient? upstream = null)
    {
        _sessionManager = sessionManager;
        _tokenBucket = tokenBucket;
        _tokenOptions = tokenOptions;
        _quotas = quotas;
        _upstream = upstream;
    }

    public async Task<ChatCompletionResponse> ProcessAsync(ChatCompletionRequest request, string sessionId, string persona = "chat", string? rateLimitKey = null)
    {
        var engine = _sessionManager.GetOrCreate(sessionId, userName: request.User, messages: request.Messages, persona: persona);

        var systemMessage = request.Messages.FirstOrDefault(m => m.Role == "system");
        if (systemMessage?.Content != null)
            SystemPromptMapper.Apply(engine, systemMessage.Content);

        RebuildHistory(engine, request.Messages);

        var userMessage = request.Messages.LastOrDefault(m => m.Role == "user");
        var input = userMessage?.Content ?? "";

        var rateKey = rateLimitKey ?? "unknown";
        var nlpAllowed = _tokenBucket.TryDeduct(rateKey, _tokenOptions.NlpCost);

        if (!nlpAllowed)
        {
            return RateLimitedResponse(request, _tokenOptions.NlpCost, rateKey);
        }

        if (_sessionManager.IsTurnQuotaExceeded(sessionId))
        {
            return new ChatCompletionResponse
            {
                Model = request.Model,
                Choices =
                [
                    new ChatCompletionChoice
                    {
                        Index = 0,
                        Message = new ChatMessage { Role = "assistant", Content = "Session turn limit reached. Please start a new session." },
                        FinishReason = "stop"
                    }
                ],
                Usage = new Usage(),
                RouteInfo = new RouteInfo
                {
                    Category = "session_turn_quota_exceeded",
                    EngineHandled = false,
                    Description = "session turn quota exceeded"
                }
            };
        }

        var rawResponseText = engine.ProcessInput(input);

        var engineHandled = !engine.LastResponseIsDeadEnd;
        var routeInfo = new RouteInfo
        {
            Category = engine.LastResponseCategory,
            EngineHandled = engineHandled,
            Description = DescribeRoute(engine.LastResponseCategory),
            UserId = request.User
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
                if (!_sessionManager.TryConsumeUpstreamCall(sessionId))
                {
                    routeInfo.Category = "upstream_quota_exceeded";
                    routeInfo.Description = "upstream LLM call quota exceeded for this session";
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
                            Description = "routed to upstream LLM",
                            UserId = request.User
                        };
                        AddRateLimitHeaders(upstreamResult, rateKey);
                        return upstreamResult;
                    }

                    routeInfo.Description = "engine dead-end, upstream unavailable (returning engine fallback)";
                }
            }
        }

        var responseText = rawResponseText;
        var finishReason = "stop";

        var (stopText, stopTruncated) = ApplyStopSequences(responseText, request.Stop);
        if (stopTruncated)
            responseText = stopText;

        var (maxText, maxTruncated) = ApplyMaxTokens(responseText, request.MaxTokens ?? request.MaxCompletionTokens);
        if (maxTruncated)
        {
            responseText = maxText;
            finishReason = "length";
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
                    FinishReason = finishReason
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
        Func<ChatCompletionChunk, Task> onChunk, Func<Task> onDone,
        string persona = "chat", string? rateLimitKey = null, CancellationToken ct = default)
    {
        var engine = _sessionManager.GetOrCreate(sessionId, userName: request.User, messages: request.Messages, persona: persona);

        var systemMessage = request.Messages.FirstOrDefault(m => m.Role == "system");
        if (systemMessage?.Content != null)
            SystemPromptMapper.Apply(engine, systemMessage.Content);

        RebuildHistory(engine, request.Messages);

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

        if (_sessionManager.IsTurnQuotaExceeded(sessionId))
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
                Choices = [new ChunkChoice { Index = 0, Delta = new Delta { Content = "Session turn limit reached. Please start a new session." } }]
            });
            await onChunk(new ChatCompletionChunk
            {
                Id = errChunkId, Created = errCreated, Model = request.Model,
                Choices = [new ChunkChoice { Index = 0, Delta = new Delta(), FinishReason = "stop" }]
            });
            await onDone();
            return;
        }

        var chunkId = $"chatcmpl-{Guid.NewGuid().ToString("N")[..12]}";
        var created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        await onChunk(new ChatCompletionChunk
        {
            Id = chunkId, Created = created, Model = request.Model,
            Choices = [new ChunkChoice { Index = 0, Delta = new Delta { Role = "assistant" } }]
        });

        var statusLock = new object();
        var statusBuffer = new List<ChatCompletionChunk>();
        var statusFlushed = false;

        Action<string>? statusCallback = msg =>
        {
            if (string.IsNullOrEmpty(msg) || msg == "clear")
                return;

            var statusChunk = new ChatCompletionChunk
            {
                Id = chunkId, Created = created, Model = request.Model,
                Choices = [new ChunkChoice { Index = 0, Delta = new Delta { Content = $"[{msg}]" } }]
            };

            lock (statusLock)
            {
                if (statusFlushed)
                    onChunk(statusChunk).GetAwaiter().GetResult();
                else
                    statusBuffer.Add(statusChunk);
            }
        };

        engine.OnStatusUpdate = statusCallback;

        string responseText;
        bool engineHandled;

        try
        {
            var engineTask = Task.Run(() => engine.ProcessInput(input), ct);
            responseText = await engineTask;
        }
        finally
        {
            engine.OnStatusUpdate = null;

            lock (statusLock)
            {
                statusFlushed = true;
                foreach (var chunk in statusBuffer)
                    onChunk(chunk).GetAwaiter().GetResult();
                statusBuffer.Clear();
            }
        }

        engineHandled = !engine.LastResponseIsDeadEnd;

        if (!engineHandled && _upstream != null)
        {
            var upstreamAllowed = _tokenBucket.TryDeduct(rateKey, _tokenOptions.StreamUpstreamCost);
            if (upstreamAllowed && _sessionManager.TryConsumeUpstreamCall(sessionId))
            {
                var streamed = await _upstream.ForwardStreamingAsync(request, onChunk, onDone, ct);
                if (streamed)
                    return;
            }
        }

        var finishReason = "stop";

        var (stopText, stopTruncated) = ApplyStopSequences(responseText, request.Stop);
        if (stopTruncated)
            responseText = stopText;

        var (maxText, maxTruncated) = ApplyMaxTokens(responseText, request.MaxTokens ?? request.MaxCompletionTokens);
        if (maxTruncated)
        {
            responseText = maxText;
            finishReason = "length";
        }

        if (responseText.Length > 0)
        {
            var sentences = ChunkBySentences(responseText);
            for (var i = 0; i < sentences.Count; i++)
            {
                await onChunk(new ChatCompletionChunk
                {
                    Id = chunkId, Created = created, Model = request.Model,
                    Choices = [new ChunkChoice { Index = 0, Delta = new Delta { Content = sentences[i] } }]
                });
            }
        }

        await onChunk(new ChatCompletionChunk
        {
            Id = chunkId, Created = created, Model = request.Model,
            Choices = [new ChunkChoice { Index = 0, Delta = new Delta(), FinishReason = finishReason }],
            Usage = new Usage
            {
                PromptTokens = request.Messages.Sum(m => m.Content?.Length ?? 0) / 4,
                CompletionTokens = responseText.Length / 4,
                TotalTokens = ((request.Messages.Sum(m => m.Content?.Length ?? 0)) + responseText.Length) / 4
            }
        });

        await onDone();
    }

    internal static List<string> ChunkBySentences(string text)
    {
        if (string.IsNullOrEmpty(text))
            return [];

        var chunks = new List<string>();
        var matches = Regex.Matches(text, @"[^.!?]+[.!?]+[\s]?|[^.!?]+$");

        foreach (Match match in matches)
        {
            var value = match.Value;
            if (!string.IsNullOrWhiteSpace(value))
                chunks.Add(value);
        }

        if (chunks.Count == 0)
            chunks.Add(text);

        return chunks;
    }

    internal static (string Text, bool WasTruncated) ApplyStopSequences(string text, object? stop)
    {
        if (string.IsNullOrEmpty(text) || stop == null)
            return (text, false);

        var stops = NormalizeStopArray(stop);
        if (stops.Length == 0)
            return (text, false);

        var earliestIndex = int.MaxValue;
        foreach (var s in stops)
        {
            if (string.IsNullOrEmpty(s))
                continue;
            var idx = text.IndexOf(s, StringComparison.Ordinal);
            if (idx >= 0 && idx < earliestIndex)
                earliestIndex = idx;
        }

        if (earliestIndex == int.MaxValue)
            return (text, false);

        return (text[..earliestIndex].TrimEnd(), true);
    }

    internal static (string Text, bool WasTruncated) ApplyMaxTokens(string text, int? maxTokens)
    {
        if (string.IsNullOrEmpty(text) || maxTokens == null || maxTokens <= 0)
            return (text, false);

        var estimatedTokens = text.Length / 4;
        if (estimatedTokens <= maxTokens)
            return (text, false);

        var targetChars = maxTokens.Value * 4;
        if (targetChars >= text.Length)
            return (text, false);

        var truncated = text[..targetChars];
        var lastSpace = truncated.LastIndexOf(' ');
        if (lastSpace > targetChars / 2)
            truncated = truncated[..lastSpace];

        return (truncated.TrimEnd(), true);
    }

    internal static string[] NormalizeStopArray(object? stop)
    {
        if (stop == null)
            return [];

        return stop switch
        {
            string s => [s],
            System.Text.Json.JsonElement e when e.ValueKind == System.Text.Json.JsonValueKind.String => [e.GetString()!],
            System.Text.Json.JsonElement e when e.ValueKind == System.Text.Json.JsonValueKind.Array => e.EnumerateArray()
                .Where(x => x.ValueKind == System.Text.Json.JsonValueKind.String)
                .Select(x => x.GetString()!)
                .ToArray(),
            string[] arr => arr,
            object[] arr => arr.OfType<string>().ToArray(),
            _ => []
        };
    }

    internal static void RebuildHistory(ChatEngine engine, List<ChatMessage> messages)
    {
        var userMessages = new List<ChatMessage>();
        foreach (var msg in messages)
        {
            if (msg.Role == "user" && !RebuildSkipRoles.Contains(msg.Role))
                userMessages.Add(msg);
        }

        if (userMessages.Count <= 1)
            return;

        var priorMessages = userMessages.Take(userMessages.Count - 1).ToList();

        var historyHash = ComputeHistoryHash(priorMessages);
        var lastHash = engine.GetContextValue(ContextKeys.LastProcessedHistoryHash);

        if (lastHash == historyHash)
            return;

        var turnCap = DefaultHistoryTurnCap;
        var turnCapRaw = engine.GetContextValue(ContextKeys.RebuildHistoryTurnCap);
        if (int.TryParse(turnCapRaw, out var parsed) && parsed > 0)
            turnCap = parsed;

        if (priorMessages.Count > turnCap)
            priorMessages = priorMessages.Skip(priorMessages.Count - turnCap).ToList();

        engine.RebuildMode = true;
        try
        {
            foreach (var msg in priorMessages)
            {
                if (string.IsNullOrWhiteSpace(msg.Content))
                    continue;
                engine.ProcessInput(msg.Content);
            }
        }
        finally
        {
            engine.RebuildMode = false;
        }

        engine.SetContext(ContextKeys.LastProcessedHistoryHash, historyHash);
    }

    private static string ComputeHistoryHash(List<ChatMessage> priorMessages)
    {
        var sb = new StringBuilder();
        foreach (var msg in priorMessages)
            sb.Append(msg.Content ?? "");
        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash[..16]);
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

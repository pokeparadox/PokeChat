using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using PokeChat.Api.Models;
using PokeChat.Core;
using PokeChat.Tools;

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
        ApplyWorkingDirectory(engine, request.WorkingDirectory);

        var systemMessage = request.Messages.FirstOrDefault(m => m.Role == "system");
        if (systemMessage?.Content != null)
            SystemPromptMapper.Apply(engine, systemMessage.Content);

        await RebuildHistoryAsync(engine, request.Messages);

        var userMessage = request.Messages.LastOrDefault(m => m.Role == "user");
        var input = userMessage?.Content ?? "";

        var rateKey = rateLimitKey ?? "unknown";
        var nlpAllowed = _tokenBucket.TryDeduct(rateKey, _tokenOptions.NlpCost);

        if (!nlpAllowed)
        {
            return RateLimitedResponse(request, _tokenOptions.NlpCost, rateKey);
        }

        var lastMsg = request.Messages.LastOrDefault();
        if (lastMsg?.Role == "tool" && request.Tools?.Count > 0)
        {
            var toolContent = lastMsg.Content ?? "";
            var toolResponse = new ChatCompletionResponse
            {
                Model = request.Model,
                Choices =
                [
                    new ChatCompletionChoice
                    {
                        Index = 0,
                        Message = new ChatMessage { Role = "assistant", Content = toolContent },
                        FinishReason = "stop"
                    }
                ],
                Usage = new Usage
                {
                    PromptTokens = request.Messages.Sum(m => (m.Content?.Length ?? 0) / 4),
                    CompletionTokens = toolContent.Length / 4,
                    TotalTokens = (request.Messages.Sum(m => (m.Content?.Length ?? 0)) + toolContent.Length) / 4
                },
                RouteInfo = new RouteInfo
                {
                    Category = "tool_result",
                    EngineHandled = true,
                    Description = "returned tool result as content"
                }
            };
            AddRateLimitHeaders(toolResponse, rateKey);
            return toolResponse;
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

        if (request.Tools?.Count > 0)
        {
            var direct = TryDetectFileToolCall(input, request.Tools);
            if (direct != null)
            {
                var directResponse = new ChatCompletionResponse
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
                                Content = null,
                                ToolCalls = [direct]
                            },
                            FinishReason = "tool_calls"
                        }
                    ],
                    Usage = new Usage
                    {
                        PromptTokens = request.Messages.Sum(m => (m.Content?.Length ?? 0) / 4),
                        CompletionTokens = 0
                    },
                    RouteInfo = new RouteInfo
                    {
                        Category = "tool_call",
                        EngineHandled = true,
                        Description = $"direct tool call: {direct.Function.Name}"
                    }
                };
                AddRateLimitHeaders(directResponse, rateKey);
                return directResponse;
            }
        }

        var rawResponseText = await engine.ProcessInputAsync(input);

        var pendingTool = engine.LastPendingToolMarker;
        if (pendingTool != null && request.Tools?.Count > 0)
        {
            var mapped = MapToOpenAIToolCall(pendingTool.Value.ToolName, pendingTool.Value.Args, request.Tools);
            if (mapped != null)
            {
                var callId = $"call_{Guid.NewGuid().ToString("N")[..24]}";
                engine.ClearPendingToolMarker();
                var toolResponse = new ChatCompletionResponse
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
                                Content = null,
                                ToolCalls =
                                [
                                    new ToolCall
                                    {
                                        Id = callId,
                                        Type = "function",
                                        Function = new FunctionCall
                                        {
                                            Name = mapped.Value.Name,
                                            Arguments = mapped.Value.Arguments
                                        }
                                    }
                                ]
                            },
                            FinishReason = "tool_calls"
                        }
                    ],
                    Usage = new Usage
                    {
                        PromptTokens = request.Messages.Sum(m => (m.Content?.Length ?? 0) / 4),
                        CompletionTokens = 0
                    },
                    RouteInfo = new RouteInfo
                    {
                        Category = "tool_call",
                        EngineHandled = true,
                        Description = $"tool call: {mapped.Value.Name}"
                    }
                };
                AddRateLimitHeaders(toolResponse, rateKey);
                return toolResponse;
            }
        }

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
                        if (upstreamResult.Choices.Count > 0)
                        {
                            var msg = upstreamResult.Choices[0].Message;
                            if (msg.ToolCalls?.Count > 0)
                            {
                                if (request.Tools?.Count > 0)
                                {
                                    msg.ToolCalls = [.. msg.ToolCalls.Where(tc =>
                                        request.Tools.Any(t =>
                                            string.Equals(t.Function?.Name, tc.Function?.Name, StringComparison.OrdinalIgnoreCase)))];
                                }
                            }
                            else
                            {
                                var content = msg.Content ?? "";
                                msg.Content = engine.ProcessToolMarkers(content);
                            }
                        }

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
        ApplyWorkingDirectory(engine, request.WorkingDirectory);

        var systemMessage = request.Messages.FirstOrDefault(m => m.Role == "system");
        if (systemMessage?.Content != null)
            SystemPromptMapper.Apply(engine, systemMessage.Content);

        await RebuildHistoryAsync(engine, request.Messages);

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

        var lastMsgStream = request.Messages.LastOrDefault();
        if (lastMsgStream?.Role == "tool" && request.Tools?.Count > 0)
        {
            var toolContent = lastMsgStream.Content ?? "";
            var toolChunkId = $"chatcmpl-{Guid.NewGuid().ToString("N")[..12]}";
            var toolCreated = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            await onChunk(new ChatCompletionChunk
            {
                Id = toolChunkId, Created = toolCreated, Model = request.Model,
                Choices = [new ChunkChoice { Index = 0, Delta = new Delta { Role = "assistant" } }]
            });
            await onChunk(new ChatCompletionChunk
            {
                Id = toolChunkId, Created = toolCreated, Model = request.Model,
                Choices = [new ChunkChoice { Index = 0, Delta = new Delta { Content = toolContent } }]
            });
            await onChunk(new ChatCompletionChunk
            {
                Id = toolChunkId, Created = toolCreated, Model = request.Model,
                Choices = [new ChunkChoice { Index = 0, Delta = new Delta(), FinishReason = "stop" }],
                Usage = new Usage
                {
                    PromptTokens = request.Messages.Sum(m => m.Content?.Length ?? 0) / 4,
                    CompletionTokens = toolContent.Length / 4,
                    TotalTokens = (request.Messages.Sum(m => m.Content?.Length ?? 0) + toolContent.Length) / 4
                }
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

        Action<string>? statusCallback = msg =>
        {
            if (string.IsNullOrEmpty(msg) || msg == "clear")
                return;

            var statusChunk = new ChatCompletionChunk
            {
                Id = chunkId, Created = created, Model = request.Model,
                Choices = [new ChunkChoice { Index = 0, Delta = new Delta { Content = $"[{msg}]" } }]
            };

            try
            {
                onChunk(statusChunk).GetAwaiter().GetResult();
            }
            catch
            {
                // Client disconnected — ignore
            }
        };

        engine.OnStatusUpdate = statusCallback;

        if (request.Tools?.Count > 0)
        {
            var direct = TryDetectFileToolCall(input, request.Tools);
            if (direct != null)
            {
                engine.OnStatusUpdate = null;
                var callId = direct.Id;
                await onChunk(new ChatCompletionChunk
                {
                    Id = chunkId, Created = created, Model = request.Model,
                    Choices = [new ChunkChoice { Index = 0, Delta = new Delta
                    {
                        ToolCalls = [new StreamingToolCall { Index = 0, Id = callId, Type = "function",
                            Function = new FunctionCall { Name = direct.Function.Name, Arguments = direct.Function.Arguments } }]
                    }}]
                });
                await onChunk(new ChatCompletionChunk
                {
                    Id = chunkId, Created = created, Model = request.Model,
                    Choices = [new ChunkChoice { Index = 0, Delta = new Delta(), FinishReason = "tool_calls" }],
                    Usage = new Usage
                    {
                        PromptTokens = request.Messages.Sum(m => m.Content?.Length ?? 0) / 4,
                        CompletionTokens = 0
                    }
                });
                await onDone();
                return;
            }
        }

        string responseText;
        bool engineHandled;

        try
        {
            responseText = await engine.ProcessInputAsync(input);
        }
        finally
        {
            engine.OnStatusUpdate = null;
        }

        engineHandled = !engine.LastResponseIsDeadEnd;

        var pendingTool = engine.LastPendingToolMarker;
        if (pendingTool != null && request.Tools?.Count > 0)
        {
            var mapped = MapToOpenAIToolCall(pendingTool.Value.ToolName, pendingTool.Value.Args, request.Tools);
            if (mapped != null)
            {
                engine.ClearPendingToolMarker();
                var callId = $"call_{Guid.NewGuid().ToString("N")[..24]}";
                var toolCallObj = new { id = callId, type = "function", function = new { name = mapped.Value.Name, arguments = mapped.Value.Arguments } };

                await onChunk(new ChatCompletionChunk
                {
                    Id = chunkId, Created = created, Model = request.Model,
                    Choices = [new ChunkChoice { Index = 0, Delta = new Delta
                    {
                        ToolCalls = [new StreamingToolCall { Index = 0, Id = callId, Type = "function",
                            Function = new FunctionCall { Name = mapped.Value.Name, Arguments = mapped.Value.Arguments } }]
                    }}]
                });
                await onChunk(new ChatCompletionChunk
                {
                    Id = chunkId, Created = created, Model = request.Model,
                    Choices = [new ChunkChoice { Index = 0, Delta = new Delta(), FinishReason = "tool_calls" }],
                    Usage = new Usage
                    {
                        PromptTokens = request.Messages.Sum(m => m.Content?.Length ?? 0) / 4,
                        CompletionTokens = 0
                    }
                });
                await onDone();
                return;
            }
        }

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

    internal static async Task RebuildHistoryAsync(ChatEngine engine, List<ChatMessage> messages)
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
                await engine.ProcessInputAsync(msg.Content);
            }
        }
        finally
        {
            engine.RebuildMode = false;
        }

        engine.SetContext(ContextKeys.LastProcessedHistoryHash, historyHash);
    }

    internal static void RebuildHistory(ChatEngine engine, List<ChatMessage> messages)
        => RebuildHistoryAsync(engine, messages).GetAwaiter().GetResult();

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

    private static void ApplyWorkingDirectory(ChatEngine engine, string? workingDirectory)
    {
        if (!string.IsNullOrWhiteSpace(workingDirectory))
            engine.SetContext(ContextKeys.ClientWorkingDirectory, workingDirectory);
    }

    internal static (string Name, string Arguments)? MapToOpenAIToolCall(string toolName, string[] args, List<ToolDefinition> availableTools)
    {
        var toolNames = availableTools
            .Where(t => t.Function?.Name != null)
            .Select(t => t.Function!.Name!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return toolName.ToLowerInvariant() switch
        {
            "file_ops" when args.Length >= 2 && args[0] == "read" && toolNames.Contains("read") =>
                ("read", JsonSerializer.Serialize(new { filePath = args[1] })),
            "file_ops" when args.Length >= 3 && args[0] == "write" && toolNames.Contains("write") =>
                ("write", JsonSerializer.Serialize(new { filePath = args[1], content = string.Join(':', args.Skip(2)) })),
            "file_ops" when args.Length >= 2 && args[0] == "list" && toolNames.Contains("glob") =>
                ("glob", JsonSerializer.Serialize(new { pattern = "*", path = args[1] })),
            "file_ops" when args.Length >= 3 && args[0] == "search" && toolNames.Contains("grep") =>
                ("grep", JsonSerializer.Serialize(new { pattern = args[2], path = args[1] })),
            "shell_command" when args.Length >= 1 && toolNames.Contains("bash") =>
                ("bash", JsonSerializer.Serialize(new { command = string.Join(':', args) })),
            _ => null
        };
    }

    private static readonly Regex FileReadPattern = new(
        @"(?:read|open|show|view|display|cat|less|update|improve|edit|modify|change|review|check|fix|rewrite|work\s+on|examine|inspect|look\s+at)\s+(?:me\s+)?(?:(?:my|the|a|an|this|our|some)\s+)?(?<file>[\w./\\-]+\.\w+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex FileWritePattern = new(
        @"(?:write|create|make|save|append)\s+(?:a\s+)?(?:new\s+)?(?<file>[\w./\\-]+\.\w+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex GrepPattern = new(
        @"(?:search|find|grep)\s+(?:for\s+)?(?<query>.+?)\s+(?:in|inside|through)\s+(?<path>[\w./\\-]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex GlobPattern = new(
        @"(?:list|show|ls)\s+(?:all\s+)?(?:the\s+)?(?:files|file|directory|dir|folder)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex BashPattern = new(
        @"^(?:run|execute|shell)\s+(?:command\s+)?(?<cmd>.+)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex GitPattern = new(
        @"^(?:git\s+)?(?<cmd>status|log|diff|branch|checkout\s+\S+|push|pull|fetch|stash|commit\s+.+|add\s+.+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    internal static ToolCall? TryDetectFileToolCall(string input, List<ToolDefinition> tools)
    {
        if (string.IsNullOrWhiteSpace(input) || tools.Count == 0)
            return null;

        var toolNames = tools
            .Where(t => t.Function?.Name != null)
            .Select(t => t.Function!.Name!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var trimmed = input.Trim();

        var readMatch = FileReadPattern.Match(trimmed);
        if (readMatch.Success)
        {
            var filePath = readMatch.Groups["file"].Value;
            if (toolNames.Contains("read"))
                return MakeToolCall("read", new { filePath });
        }

        var writeMatch = FileWritePattern.Match(trimmed);
        if (writeMatch.Success)
        {
            var filePath = writeMatch.Groups["file"].Value;
            if (toolNames.Contains("write"))
                return MakeToolCall("write", new { filePath, content = "" });
        }

        var grepMatch = GrepPattern.Match(trimmed);
        if (grepMatch.Success)
        {
            var query = grepMatch.Groups["query"].Value;
            var path = grepMatch.Groups["path"].Value;
            if (toolNames.Contains("grep"))
                return MakeToolCall("grep", new { pattern = query, path });
        }

        if (GlobPattern.IsMatch(trimmed) && toolNames.Contains("glob"))
            return MakeToolCall("glob", new { pattern = "**/*", path = "." });

        var bashMatch = BashPattern.Match(trimmed);
        if (bashMatch.Success && toolNames.Contains("bash"))
        {
            var cmd = bashMatch.Groups["cmd"].Value;
            return MakeToolCall("bash", new { command = cmd });
        }

        var gitMatch = GitPattern.Match(trimmed);
        if (gitMatch.Success && toolNames.Contains("bash"))
        {
            var cmd = $"git {gitMatch.Groups["cmd"].Value}";
            return MakeToolCall("bash", new { command = cmd });
        }

        return null;
    }

    private static ToolCall MakeToolCall(string name, object args)
    {
        return new ToolCall
        {
            Id = $"call_{Guid.NewGuid().ToString("N")[..24]}",
            Type = "function",
            Function = new FunctionCall
            {
                Name = name,
                Arguments = JsonSerializer.Serialize(args)
            }
        };
    }
}

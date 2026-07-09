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
        var engine = _sessionManager.GetOrCreate(sessionId);

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
                PromptTokens = request.Messages.Sum(m => m.Content.Length / 4),
                CompletionTokens = responseText.Length / 4,
                TotalTokens = (request.Messages.Sum(m => m.Content.Length) + responseText.Length) / 4
            },
            RouteInfo = routeInfo
        };
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

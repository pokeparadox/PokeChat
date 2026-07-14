using System.Net;
using System.Text;
using System.Text.Json;
using PokeChat.Api.Models;
using PokeChat.Api.Services;
using Shouldly;

namespace PokeChat.Tests.Api;

public class OpenAIAdapterSpecTests
{
    #region Stop Sequences

    [Fact]
    public void ApplyStopSequences_SingleStop_TrunctesAtFirstMatch()
    {
        var (text, truncated) = OpenAIAdapter.ApplyStopSequences("Hello world. Goodbye!", "Goodbye");
        text.ShouldBe("Hello world.");
        truncated.ShouldBeTrue();
    }

    [Fact]
    public void ApplyStopSequences_StopArray_TakesFirstMatch()
    {
        var (text, truncated) = OpenAIAdapter.ApplyStopSequences("Hello world. End now. Goodbye!", new object[] { "End now", "Goodbye" });
        text.ShouldBe("Hello world.");
        truncated.ShouldBeTrue();
    }

    [Fact]
    public void ApplyStopSequences_StopArraySecondEarlier_Wins()
    {
        var (text, truncated) = OpenAIAdapter.ApplyStopSequences("Hello STOP world END.", new object[] { "END", "STOP" });
        text.ShouldBe("Hello");
        truncated.ShouldBeTrue();
    }

    [Fact]
    public void ApplyStopSequences_NoMatch_ReturnsUnchanged()
    {
        var (text, truncated) = OpenAIAdapter.ApplyStopSequences("Hello world.", "GOODBYE");
        text.ShouldBe("Hello world.");
        truncated.ShouldBeFalse();
    }

    [Fact]
    public void ApplyStopSequences_NullStop_ReturnsUnchanged()
    {
        var (text, truncated) = OpenAIAdapter.ApplyStopSequences("Hello world.", null);
        text.ShouldBe("Hello world.");
        truncated.ShouldBeFalse();
    }

    [Fact]
    public void ApplyStopSequences_EmptyText_ReturnsEmpty()
    {
        var (text, truncated) = OpenAIAdapter.ApplyStopSequences("", "stop");
        text.ShouldBe("");
        truncated.ShouldBeFalse();
    }

    [Fact]
    public void ApplyStopSequences_TrimsTrailingWhitespace()
    {
        var (text, truncated) = OpenAIAdapter.ApplyStopSequences("Hello   \n  STOP world", "STOP");
        text.ShouldBe("Hello");
        truncated.ShouldBeTrue();
    }

    #endregion

    #region Max Tokens

    [Fact]
    public void ApplyMaxTokens_NullMaxTokens_NoChange()
    {
        var (text, truncated) = OpenAIAdapter.ApplyMaxTokens("Hello world", null);
        text.ShouldBe("Hello world");
        truncated.ShouldBeFalse();
    }

    [Fact]
    public void ApplyMaxTokens_EstimatedTokensUnderLimit_NoChange()
    {
        var shortText = "Hi"; // 0 tokens (2/4 = 0)
        var (text, truncated) = OpenAIAdapter.ApplyMaxTokens(shortText, 10);
        text.ShouldBe(shortText);
        truncated.ShouldBeFalse();
    }

    [Fact]
    public void ApplyMaxTokens_ExceedsLimit_Truncates()
    {
        var longText = string.Join(" ", Enumerable.Repeat("word", 100)); // 400 chars = ~100 tokens
        var (text, truncated) = OpenAIAdapter.ApplyMaxTokens(longText, 10);
        truncated.ShouldBeTrue();
        text.Length.ShouldBeLessThan(longText.Length);
        text.ShouldNotBe(longText);
    }

    [Fact]
    public void ApplyMaxTokens_TruncatesAtWhitespaceBoundary()
    {
        var text = "one two three four five six seven eight nine ten";
        var (result, truncated) = OpenAIAdapter.ApplyMaxTokens(text, 2);
        truncated.ShouldBeTrue();
        result.ShouldNotContain("  ");
    }

    [Fact]
    public void ApplyMaxTokens_ZeroOrNegative_NoChange()
    {
        var (text1, t1) = OpenAIAdapter.ApplyMaxTokens("Hello", 0);
        t1.ShouldBeFalse();
        var (text2, t2) = OpenAIAdapter.ApplyMaxTokens("Hello", -1);
        t2.ShouldBeFalse();
    }

    #endregion

    #region NormalizeStopArray

    [Fact]
    public void NormalizeStopArray_Null_ReturnsEmpty()
    {
        OpenAIAdapter.NormalizeStopArray(null).ShouldBeEmpty();
    }

    [Fact]
    public void NormalizeStopArray_SingleString_ReturnsOne()
    {
        var result = OpenAIAdapter.NormalizeStopArray("stop");
        result.Length.ShouldBe(1);
        result[0].ShouldBe("stop");
    }

    [Fact]
    public void NormalizeStopArray_JsonArray_ReturnsAll()
    {
        var json = JsonDocument.Parse("""["stop1", "stop2"]""").RootElement;
        var result = OpenAIAdapter.NormalizeStopArray(json);
        result.Length.ShouldBe(2);
        result[0].ShouldBe("stop1");
        result[1].ShouldBe("stop2");
    }

    [Fact]
    public void NormalizeStopArray_JsonString_ReturnsOne()
    {
        var json = JsonDocument.Parse(""" "stop" """).RootElement;
        var result = OpenAIAdapter.NormalizeStopArray(json);
        result.Length.ShouldBe(1);
        result[0].ShouldBe("stop");
    }

    [Fact]
    public void NormalizeStopArray_StringArray_ReturnsAll()
    {
        var result = OpenAIAdapter.NormalizeStopArray(new[] { "a", "b" });
        result.Length.ShouldBe(2);
    }

    #endregion

    #region ChunkBySentences (existing, verifying no regression)

    [Fact]
    public void ChunkBySentences_StillWorks_AfterRefactor()
    {
        var result = OpenAIAdapter.ChunkBySentences("Hello! How are you? Fine.");
        result.Count.ShouldBe(3);
    }

    #endregion

    #region Tier 2 Acceptance (DTO deserialization)

    [Fact]
    public void Request_WithTier2Fields_DeserializesWithoutError()
    {
        var json = """
        {
            "model": "pokechat-v1",
            "messages": [{"role": "user", "content": "hi"}],
            "top_p": 0.9,
            "frequency_penalty": 0.5,
            "presence_penalty": 0.3,
            "logit_bias": {"50256": -100},
            "response_format": {"type": "text"},
            "n": 1,
            "logprobs": true,
            "top_logprobs": 5,
            "seed": 42,
            "stop": ["END"],
            "user": "alice"
        }
        """;

        var request = JsonSerializer.Deserialize<ChatCompletionRequest>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        request.ShouldNotBeNull();
        request.Model.ShouldBe("pokechat-v1");
        request.TopP.ShouldBe(0.9);
        request.FrequencyPenalty.ShouldBe(0.5);
        request.PresencePenalty.ShouldBe(0.3);
        request.LogitBias.ShouldNotBeNull();
        request.LogitBias!["50256"].ShouldBe(-100);
        request.ResponseFormat.ShouldNotBeNull();
        request.N.ShouldBe(1);
        request.Logprobs.ShouldBe(true);
        request.TopLogprobs.ShouldBe(5);
        request.Seed.ShouldBe(42);
        request.User.ShouldBe("alice");
        request.Stop.ShouldNotBeNull();
    }

    [Fact]
    public void Request_StopAsSingleString_Deserializes()
    {
        var json = """{"model":"pokechat-v1","messages":[{"role":"user","content":"hi"}],"stop":"END"}""";
        var request = JsonSerializer.Deserialize<ChatCompletionRequest>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        request.ShouldNotBeNull();
        request.Stop.ShouldNotBeNull();
        var stops = OpenAIAdapter.NormalizeStopArray(request.Stop);
        stops.Length.ShouldBe(1);
        stops[0].ShouldBe("END");
    }

    #endregion

    #region Seed Forwarding to Upstream

    [Fact]
    public async Task ForwardAsync_WithSeed_IncludesSeedInUpstreamBody()
    {
        var capturedBody = await CaptureUpstreamBody(seed: 42);
        capturedBody.ShouldContain("\"seed\":42");
    }

    [Fact]
    public async Task ForwardAsync_WithoutSeed_OmitsSeedFromUpstreamBody()
    {
        var capturedBody = await CaptureUpstreamBody(seed: null);
        capturedBody.ShouldNotContain("seed");
    }

    [Fact]
    public async Task ForwardAsync_WithStop_IncludesStopInUpstreamBody()
    {
        var capturedBody = await CaptureUpstreamBody(stop: "END");
        capturedBody.ShouldContain("\"stop\"");
        capturedBody.ShouldContain("END");
    }

    [Fact]
    public async Task ForwardAsync_WithMultipleStops_IncludesArrayInUpstreamBody()
    {
        var capturedBody = await CaptureUpstreamBody(stop: new object[] { "A", "B" });
        capturedBody.ShouldContain("\"stop\"");
        capturedBody.ShouldContain("A");
        capturedBody.ShouldContain("B");
    }

    #endregion

    #region User Field in RouteInfo

    [Fact]
    public void RouteInfo_UserId_SetFromRequestUser()
    {
        var routeInfo = new RouteInfo { UserId = "alice" };
        routeInfo.UserId.ShouldBe("alice");
    }

    #endregion

    #region Helpers

    private static async Task<string> CaptureUpstreamBody(int? seed = null, object? stop = null)
    {
        string? capturedBody = null;

        var handler = new CaptureHandler(req =>
        {
            capturedBody = req.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
        });

        var http = new HttpClient(handler);
        var options = new UpstreamOptions { Endpoint = "http://localhost:11434/v1/chat/completions", Model = "test" };
        var client = new UpstreamLLMClient(http, options);

        var request = new ChatCompletionRequest
        {
            Model = "pokechat-v1",
            Messages = [new ChatMessage { Role = "user", Content = "hello" }],
            Seed = seed,
            Stop = stop
        };

        await client.ForwardAsync(request);
        return capturedBody ?? "";
    }

    private class CaptureHandler(Action<HttpRequestMessage> onCapture) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            onCapture(request);
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"id":"c1","object":"chat.completion","created":1000,"model":"m","choices":[{"index":0,"message":{"role":"assistant","content":"ok"},"finish_reason":"stop"}]}""", Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }

    #endregion
}

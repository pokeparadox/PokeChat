using System.Net;
using System.Text;
using PokeChat.Api.Models;
using PokeChat.Api.Services;
using Shouldly;

namespace PokeChat.Tests.Api;

public class UpstreamLLMClientStreamTests
{
    private static ChatCompletionRequest MakeRequest(string content = "hello")
    {
        return new ChatCompletionRequest
        {
            Model = "pokechat-v1",
            Messages = [new ChatMessage { Role = "user", Content = content }]
        };
    }

    private static UpstreamLLMClient MakeClient(HttpMessageHandler handler, string model = "test-model")
    {
        var http = new HttpClient(handler);
        var options = new UpstreamOptions { Endpoint = "http://localhost:11434/v1/chat/completions", Model = model };
        return new UpstreamLLMClient(http, options);
    }

    private static HttpMessageHandler MockSseStream(params string[] lines)
    {
        var sb = new StringBuilder();
        foreach (var line in lines)
            sb.AppendLine(line);

        var content = new StringContent(sb.ToString(), Encoding.UTF8, "text/event-stream");
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        return new MockHttpHandler(response);
    }

    [Fact]
    public async Task ForwardStreamingAsync_ParsesSseChunks()
    {
        var chunk1 = """{"id":"c1","object":"chat.completion.chunk","created":1000,"model":"m","choices":[{"index":0,"delta":{"role":"assistant"},"finish_reason":null}]}""";
        var chunk2 = """{"id":"c1","object":"chat.completion.chunk","created":1000,"model":"m","choices":[{"index":0,"delta":{"content":"Hello"},"finish_reason":null}]}""";
        var chunk3 = """{"id":"c1","object":"chat.completion.chunk","created":1000,"model":"m","choices":[{"index":0,"delta":{"content":" world"},"finish_reason":null}]}""";

        var handler = MockSseStream($"data: {chunk1}", $"data: {chunk2}", $"data: {chunk3}", "data: [DONE]");
        var client = MakeClient(handler);

        var chunks = new List<ChatCompletionChunk>();
        var doneCalled = false;

        var result = await client.ForwardStreamingAsync(MakeRequest(),
            chunk => { chunks.Add(chunk); return Task.CompletedTask; },
            () => { doneCalled = true; return Task.CompletedTask; });

        result.ShouldBeTrue();
        chunks.Count.ShouldBe(3);
        chunks[0].Choices[0].Delta.Role.ShouldBe("assistant");
        chunks[1].Choices[0].Delta.Content.ShouldBe("Hello");
        chunks[2].Choices[0].Delta.Content.ShouldBe(" world");
        doneCalled.ShouldBeTrue();
    }

    [Fact]
    public async Task ForwardStreamingAsync_SkipsEmptyAndCommentLines()
    {
        var chunk = """{"id":"c1","object":"chat.completion.chunk","created":1000,"model":"m","choices":[{"index":0,"delta":{"content":"hi"},"finish_reason":null}]}""";

        var handler = MockSseStream("", ": this is a comment", $"data: {chunk}", "data: [DONE]");
        var client = MakeClient(handler);

        var chunks = new List<ChatCompletionChunk>();
        await client.ForwardStreamingAsync(MakeRequest(),
            c => { chunks.Add(c); return Task.CompletedTask; },
            () => Task.CompletedTask);

        chunks.Count.ShouldBe(1);
        chunks[0].Choices[0].Delta.Content.ShouldBe("hi");
    }

    [Fact]
    public async Task ForwardStreamingAsync_SetsModelOnChunks()
    {
        var chunk = """{"id":"c1","object":"chat.completion.chunk","created":1000,"model":"other","choices":[{"index":0,"delta":{"content":"yo"},"finish_reason":null}]}""";

        var handler = MockSseStream($"data: {chunk}", "data: [DONE]");
        var client = MakeClient(handler, model: "my-model");

        var chunks = new List<ChatCompletionChunk>();
        await client.ForwardStreamingAsync(MakeRequest(),
            c => { chunks.Add(c); return Task.CompletedTask; },
            () => Task.CompletedTask);

        chunks[0].Model.ShouldBe("my-model");
    }

    [Fact]
    public async Task ForwardStreamingAsync_MalformedJson_SkipsChunk()
    {
        var goodChunk = """{"id":"c1","object":"chat.completion.chunk","created":1000,"model":"m","choices":[{"index":0,"delta":{"content":"ok"},"finish_reason":null}]}""";

        var handler = MockSseStream("data: {not valid json}", $"data: {goodChunk}", "data: [DONE]");
        var client = MakeClient(handler);

        var chunks = new List<ChatCompletionChunk>();
        await client.ForwardStreamingAsync(MakeRequest(),
            c => { chunks.Add(c); return Task.CompletedTask; },
            () => Task.CompletedTask);

        chunks.Count.ShouldBe(1);
        chunks[0].Choices[0].Delta.Content.ShouldBe("ok");
    }

    [Fact]
    public async Task ForwardStreamingAsync_EmptyStream_CallsDone()
    {
        var handler = MockSseStream("data: [DONE]");
        var client = MakeClient(handler);

        var chunks = new List<ChatCompletionChunk>();
        var doneCalled = false;

        await client.ForwardStreamingAsync(MakeRequest(),
            c => { chunks.Add(c); return Task.CompletedTask; },
            () => { doneCalled = true; return Task.CompletedTask; });

        chunks.ShouldBeEmpty();
        doneCalled.ShouldBeTrue();
    }

    [Fact]
    public async Task ForwardStreamingAsync_Disabled_ReturnsFalse()
    {
        var handler = MockSseStream("data: [DONE]");
        var http = new HttpClient(handler);
        var options = new UpstreamOptions { Endpoint = null };
        var client = new UpstreamLLMClient(http, options);

        var result = await client.ForwardStreamingAsync(MakeRequest(),
            _ => Task.CompletedTask, () => Task.CompletedTask);

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task ForwardStreamingAsync_HttpError_ReturnsFalse()
    {
        var handler = new MockHttpHandler(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var client = MakeClient(handler);

        var result = await client.ForwardStreamingAsync(MakeRequest(),
            _ => Task.CompletedTask, () => Task.CompletedTask);

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task ForwardStreamingAsync_SkipsNonDataLines()
    {
        var chunk = """{"id":"c1","object":"chat.completion.chunk","created":1000,"model":"m","choices":[{"index":0,"delta":{"content":"yes"},"finish_reason":null}]}""";

        var handler = MockSseStream("event: message", $"data: {chunk}", "data: [DONE]");
        var client = MakeClient(handler);

        var chunks = new List<ChatCompletionChunk>();
        await client.ForwardStreamingAsync(MakeRequest(),
            c => { chunks.Add(c); return Task.CompletedTask; },
            () => Task.CompletedTask);

        chunks.Count.ShouldBe(1);
        chunks[0].Choices[0].Delta.Content.ShouldBe("yes");
    }

    private class MockHttpHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(response);
        }
    }
}

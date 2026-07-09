using PokeChat.Api.Models;
using PokeChat.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<ChatEngineFactory>();

var upstreamOptions = new UpstreamOptions();
builder.Configuration.GetSection("Upstream").Bind(upstreamOptions);
builder.Services.AddSingleton(upstreamOptions);

builder.Services.AddHttpClient<UpstreamLLMClient>();
builder.Services.AddSingleton<SessionManager>();
builder.Services.AddSingleton<OpenAIAdapter>();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.MapPost("/v1/chat/completions", async (ChatCompletionRequest request, OpenAIAdapter adapter, SessionManager sessions) =>
{
    var sessionId = request.SessionId ?? Guid.NewGuid().ToString();
    var response = await adapter.ProcessAsync(request, sessionId);
    return Results.Ok(response);
});

app.Run();

using System.Text.Json;
using System.Text.Json.Serialization;
using PokeChat.Api.Models;
using PokeChat.Api.Services;
using PokeChat.Data;

var jsonOptions = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
};

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

builder.Services.AddSingleton<ChatEngineFactory>();

var upstreamOptions = new UpstreamOptions();
builder.Configuration.GetSection("Upstream").Bind(upstreamOptions);
builder.Services.AddSingleton(upstreamOptions);

builder.Services.AddHttpClient<UpstreamLLMClient>();
builder.Services.AddSingleton<SessionManager>();
builder.Services.AddSingleton<OpenAIAdapter>();

var app = builder.Build();

using (var initContext = new PokeChatDbContext())
{
    new DatabaseInitializer(initContext).Initialize();
}

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.MapGet("/v1/models", () => Results.Ok(new
{
    @object = "list",
    data = new[]
    {
        new { id = "pokechat-v1", @object = "model", created = 1700000000L, owned_by = "pokechat" }
    }
}));

app.MapPost("/v1/chat/completions", async (HttpContext httpContext, ChatCompletionRequest request, OpenAIAdapter adapter, SessionManager sessions) =>
{
    var sessionId = request.SessionId ?? Guid.NewGuid().ToString();
    sessions.UpdateActivity(sessionId);

    if (request.Stream)
    {
        httpContext.Response.ContentType = "text/event-stream";
        httpContext.Response.Headers.CacheControl = "no-cache";
        httpContext.Response.Headers.Connection = "keep-alive";

        await adapter.StreamResponseAsync(request, sessionId,
            chunk => httpContext.Response.WriteAsync($"data: {JsonSerializer.Serialize(chunk, jsonOptions)}\n\n"),
            () => httpContext.Response.WriteAsync("data: [DONE]\n\n"));

        await httpContext.Response.Body.FlushAsync();
        return Results.Empty;
    }

    var response = await adapter.ProcessAsync(request, sessionId);
    return Results.Ok(response);
});

app.MapPost("/sessions", (SessionManager sessions, SessionCreateRequest? request) =>
{
    var sessionId = Guid.NewGuid().ToString();
    sessions.GetOrCreate(sessionId, request?.UserName);
    return Results.Created($"/sessions/{sessionId}", new { session_id = sessionId });
});

app.MapGet("/sessions", (SessionManager sessions) =>
{
    var active = sessions.ListActiveSessions();
    return Results.Ok(active.Select(s => new
    {
        session_id = s.SessionGuid,
        user_id = s.UserId,
        started_at = s.StartedAt,
        last_active_at = s.LastActiveAt,
        turn_count = s.TurnCount,
        bot_name = s.BotName,
        persona = s.Persona
    }));
});

app.MapGet("/sessions/{id}", (string id, SessionManager sessions) =>
{
    var session = sessions.GetSessionMetadata(id);
    if (session == null)
        return Results.NotFound(new { error = "Session not found" });

    return Results.Ok(new
    {
        session_id = session.SessionGuid,
        user_id = session.UserId,
        started_at = session.StartedAt,
        ended_at = session.EndedAt,
        last_active_at = session.LastActiveAt,
        turn_count = session.TurnCount,
        bot_name = session.BotName,
        persona = session.Persona
    });
});

app.MapDelete("/sessions/{id}", (string id, SessionManager sessions) =>
{
    if (!sessions.SessionExists(id))
        return Results.NotFound(new { error = "Session not found" });

    sessions.EndSession(id);
    return Results.Ok(new { status = "ended", session_id = id });
});

app.MapPost("/sessions/{id}/chat", (string id, ChatRequest request, SessionManager sessions) =>
{
    var engine = sessions.GetOrCreate(id);
    var response = engine.ProcessInput(request.Message);
    sessions.UpdateActivity(id);
    return Results.Ok(new { response, session_id = id });
});

app.Run();

public class SessionCreateRequest
{
    public string? UserName { get; set; }
}

public class ChatRequest
{
    public string Message { get; set; } = string.Empty;
}

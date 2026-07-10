using PokeChat.Api.Models;
using PokeChat.Api.Services;
using PokeChat.Data;

var builder = WebApplication.CreateBuilder(args);

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

app.MapPost("/v1/chat/completions", async (ChatCompletionRequest request, OpenAIAdapter adapter, SessionManager sessions) =>
{
    var sessionId = request.SessionId ?? Guid.NewGuid().ToString();
    var response = await adapter.ProcessAsync(request, sessionId);
    sessions.UpdateActivity(sessionId);
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

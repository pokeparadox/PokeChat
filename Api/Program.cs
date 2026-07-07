using System.Collections.Concurrent;
using PokeChat.Api;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<SessionManager>();
var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.MapPost("/chat", (ChatRequest request, SessionManager manager) =>
{
    if (string.IsNullOrWhiteSpace(request.Message))
        return Results.BadRequest(new { error = "Message is required" });

    var response = manager.ProcessMessage(request.SessionId, request.Message);
    return Results.Ok(response);
});

app.Run();

public record ChatRequest(string Message, string? SessionId);
public record ChatResponse(string Response, string SessionId, string? Greeting);

public sealed class SessionManager : IDisposable
{
    private readonly ConcurrentDictionary<string, ChatSessionWrapper> _sessions = new();

    public ChatResponse ProcessMessage(string? sessionId, string message)
    {
        var id = sessionId ?? Guid.NewGuid().ToString();
        var wrapper = _sessions.GetOrAdd(id, _ => new ChatSessionWrapper());
        var (response, greeting) = wrapper.ProcessMessage(message);
        return new ChatResponse(response, id, greeting);
    }

    public void Dispose()
    {
        foreach (var kvp in _sessions)
            kvp.Value.Dispose();
        _sessions.Clear();
    }
}

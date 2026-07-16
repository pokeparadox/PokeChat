using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using PokeChat.Api.Models;
using PokeChat.Api.Services;
using PokeChat.Data;
using PokeChat.Enrichment;
using PokeChat.Math;
using PokeChat.Mcp;
using PokeChat.Tools;

var jsonOptions = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
};

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddUserSecrets<PokeChat.Api.Services.WeatherApiOptions>();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

builder.Services.AddSingleton<PokeChatDbContext>();
builder.Services.AddSingleton(sp =>
    new ChatEngineFactory(sp.GetService<EnrichmentQueue>()));

var memPalaceOptions = new MemPalaceOptions();
builder.Configuration.GetSection("MemPalace").Bind(memPalaceOptions);
builder.Services.AddSingleton(memPalaceOptions);

var enrichmentMcp = new McpRegistry();
var enrichmentTools = new ToolRegistry(mcpRegistry: enrichmentMcp);
var enrichmentEnricher = new MemPalaceEnricher(enrichmentTools, memPalaceOptions);
builder.Services.AddSingleton<IKnowledgeEnricher>(enrichmentEnricher);
builder.Services.AddSingleton(
    new EnrichmentQueue(enrichmentEnricher));

var upstreamOptions = new UpstreamOptions();
builder.Configuration.GetSection("Upstream").Bind(upstreamOptions);
builder.Services.AddSingleton(upstreamOptions);

var tokenOptions = new TokenBucketOptions();
builder.Configuration.GetSection("RateLimiting").Bind(tokenOptions);
builder.Services.AddSingleton(tokenOptions);
builder.Services.AddSingleton<ITokenBucketStore, InMemoryTokenBucketStore>();
builder.Services.AddSingleton<ITimeEngine, SystemTimeEngine>();

var sessionQuotas = new SessionQuotaOptions();
builder.Configuration.GetSection("SessionQuotas").Bind(sessionQuotas);
builder.Services.AddSingleton(sessionQuotas);

var weatherOptions = new WeatherApiOptions();
builder.Configuration.GetSection("Weather").Bind(weatherOptions);
var envWeatherKey = Environment.GetEnvironmentVariable("WEATHER_API_KEY");
if (!string.IsNullOrWhiteSpace(envWeatherKey))
    weatherOptions.ApiKey = envWeatherKey;
var envWeatherBase = Environment.GetEnvironmentVariable("WEATHER_API_BASE_URL");
if (!string.IsNullOrWhiteSpace(envWeatherBase))
    weatherOptions.BaseUrl = envWeatherBase;
builder.Services.AddSingleton(weatherOptions);

builder.Services.AddHttpClient<UpstreamLLMClient>();
builder.Services.AddHttpClient<WeatherApiClient>();
builder.Services.AddSingleton<SessionManager>();
builder.Services.AddSingleton<OpenAIAdapter>();
builder.Services.AddSingleton<TitleGenerator>();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("user-partitioned", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromSeconds(60),
                QueueLimit = 2
            }));
});

var app = builder.Build();

if (args.Contains("--restore-db"))
{
    var dbPath = Path.Combine(AppContext.BaseDirectory, "pokechat.db");
    var envPath = Environment.GetEnvironmentVariable("POKECHAT_DB_PATH");
    if (!string.IsNullOrEmpty(envPath)) dbPath = envPath;

    if (BackupHelper.Restore(dbPath))
    {
        Console.WriteLine("[Database] Restored from backup.");
    }
    else
    {
        Console.WriteLine("[Database] No backup found at " + BackupHelper.GetBackupPath(dbPath));
    }
    return;
}

app.UseRateLimiter();

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
        new { id = "pokechat-v1", @object = "model", created = 1700000000L, owned_by = "pokechat" },
        new { id = "pokecode-v1", @object = "model", created = 1700000000L, owned_by = "pokechat" }
    }
}));

app.MapPost("/v1/chat/completions", async (HttpContext httpContext, ChatCompletionRequest request, OpenAIAdapter adapter, SessionManager sessions) =>
{
    var rateLimitKey = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    var sessionId = request.SessionId ?? Guid.NewGuid().ToString();
    sessions.UpdateActivity(sessionId);

    var userAgent = httpContext.Request.Headers.UserAgent.FirstOrDefault();
    var (persona, warning) = PersonaRouter.ResolvePersona(request.Model, userAgent);

    if (request.Stream)
    {
        httpContext.Response.ContentType = "text/event-stream";
        httpContext.Response.Headers.CacheControl = "no-cache";
        httpContext.Response.Headers.Connection = "keep-alive";

        await adapter.StreamResponseAsync(request, sessionId, persona: persona, rateLimitKey: rateLimitKey,
            onChunk: chunk => httpContext.Response.WriteAsync($"data: {JsonSerializer.Serialize(chunk, jsonOptions)}\n\n"),
            onDone: () => httpContext.Response.WriteAsync("data: [DONE]\n\n"),
            ct: httpContext.RequestAborted);

        await httpContext.Response.Body.FlushAsync();
        return Results.Empty;
    }

    var response = await adapter.ProcessAsync(request, sessionId, persona, rateLimitKey: rateLimitKey);

    if (warning != null && response.Choices.Count > 0)
    {
        response.Choices[0].Message.Content = warning + "\n\n" + response.Choices[0].Message.Content;
    }

    httpContext.Response.Headers["X-PokeChat-Persona"] = persona;
    httpContext.Response.Headers["X-PokeChat-Model"] = persona == "coding" ? "pokecode-v1" : "pokechat-v1";
    httpContext.Response.Headers["X-RateLimit-Remaining"] = response.RateLimitRemaining.ToString();
    httpContext.Response.Headers["X-RateLimit-Reset"] = response.RateLimitReset.ToString();

    return Results.Ok(response);
}).RequireRateLimiting("user-partitioned");

app.MapPost("/v1/title", (TitleRequest request, TitleGenerator generator) =>
{
    var title = generator.GenerateTitle(request.Messages);
    return Results.Ok(new { title });
});

app.MapPost("/sessions", (SessionManager sessions, SessionCreateRequest? request) =>
{
    var sessionId = Guid.NewGuid().ToString();
    sessions.GetOrCreate(sessionId, request?.UserName);
    return Results.Created($"/sessions/{sessionId}", new { session_id = sessionId });
}).RequireRateLimiting("user-partitioned");

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
    try
    {
        var response = engine.ProcessInput(request.Message);
        sessions.UpdateActivity(id);
        return Results.Ok(new { response, session_id = id });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Error] Exception while processing chat for session {id}: {ex}");
        return Results.Problem(detail: ex.Message, statusCode: 500);
    }
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

public class TitleRequest
{
    public List<ChatMessage> Messages { get; set; } = new();
}

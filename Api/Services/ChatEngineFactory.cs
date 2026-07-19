using Microsoft.EntityFrameworkCore;
using PokeChat.Api.Core.Planning;
using PokeChat.Core;
using PokeChat.Data;
using PokeChat.Enrichment;

namespace PokeChat.Api.Services;

public class ChatEngineFactory
{
    private readonly IDbContextFactory<PokeChatDbContext> _dbContextFactory;
    private readonly EnrichmentQueue? _enrichmentQueue;
    private readonly IPlannerService? _plannerService;
    private readonly bool _openCodeDetected;

    public ChatEngineFactory(IDbContextFactory<PokeChatDbContext> dbContextFactory, EnrichmentQueue? enrichmentQueue = null, IPlannerService? plannerService = null)
    {
        _dbContextFactory = dbContextFactory;
        _enrichmentQueue = enrichmentQueue;
        _plannerService = plannerService;
        _openCodeDetected = DetectOpenCodeEnvironment();
    }

    public virtual ChatEngine Create(string? sessionId = null, string persona = "chat")
    {
        if (_openCodeDetected && persona == "chat")
            persona = "coding";

        var engine = new ChatEngine(_dbContextFactory, _enrichmentQueue, _plannerService);
        if (!string.IsNullOrEmpty(sessionId))
            engine.SessionId = sessionId;
        if (persona != "chat")
            engine.SwitchPersona(persona);
        engine.InitializeSession();
        return engine;
    }

    private static bool DetectOpenCodeEnvironment()
    {
        try
        {
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OPENCODE_API_KEY")))
                return true;
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OPENCODE_SESSION_ID")))
                return true;
            if (string.Equals(Environment.GetEnvironmentVariable("OPENCODE_ENV"), "opencode", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        catch
        {
        }
        return false;
    }
}

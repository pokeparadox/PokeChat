using Microsoft.EntityFrameworkCore;
using PokeChat.Core;
using PokeChat.Data;
using PokeChat.Enrichment;

namespace PokeChat.Api.Services;

public class ChatEngineFactory
{
    private readonly IDbContextFactory<PokeChatDbContext> _dbContextFactory;
    private readonly EnrichmentQueue? _enrichmentQueue;

    public ChatEngineFactory(IDbContextFactory<PokeChatDbContext> dbContextFactory, EnrichmentQueue? enrichmentQueue = null)
    {
        _dbContextFactory = dbContextFactory;
        _enrichmentQueue = enrichmentQueue;
    }

    public virtual ChatEngine Create(string? sessionId = null, string persona = "chat")
    {
        var engine = new ChatEngine(_dbContextFactory, _enrichmentQueue);
        if (!string.IsNullOrEmpty(sessionId))
            engine.SessionId = sessionId;
        if (persona != "chat")
            engine.SwitchPersona(persona);
        engine.InitializeSession();
        return engine;
    }
}

using PokeChat.Core;
using PokeChat.Enrichment;

namespace PokeChat.Api.Services;

public class ChatEngineFactory
{
    private readonly EnrichmentQueue? _enrichmentQueue;

    public ChatEngineFactory(EnrichmentQueue? enrichmentQueue = null)
    {
        _enrichmentQueue = enrichmentQueue;
    }

    public virtual ChatEngine Create(string? sessionId = null, string persona = "chat")
    {
        var engine = new ChatEngine(_enrichmentQueue);
        if (!string.IsNullOrEmpty(sessionId))
            engine.SessionId = sessionId;
        if (persona != "chat")
            engine.SwitchPersona(persona);
        engine.InitializeSession();
        return engine;
    }
}

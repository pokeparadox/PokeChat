using PokeChat.Core;

namespace PokeChat.Api.Services;

public class ChatEngineFactory
{
    public virtual ChatEngine Create(string? sessionId = null)
    {
        var engine = new ChatEngine();
        if (!string.IsNullOrEmpty(sessionId))
            engine.SessionId = sessionId;
        return engine;
    }
}

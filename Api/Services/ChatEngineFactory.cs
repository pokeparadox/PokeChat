using PokeChat.Core;

namespace PokeChat.Api.Services;

public class ChatEngineFactory
{
    public virtual ChatEngine Create(string? sessionId = null, string persona = "chat")
    {
        var engine = new ChatEngine();
        if (!string.IsNullOrEmpty(sessionId))
            engine.SessionId = sessionId;
        if (persona != "chat")
            engine.SwitchPersona(persona);
        return engine;
    }
}

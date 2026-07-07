using PokeChat.Core;

namespace PokeChat.Api;

internal sealed class ChatSessionWrapper : IDisposable
{
    private readonly ChatSession _session;
    private bool _greeted;

    public ChatSessionWrapper()
    {
        _session = new ChatSession();
    }

    public (string Response, string? Greeting) ProcessMessage(string message)
    {
        if (!_greeted)
        {
            _greeted = true;
            var greeting = _session.GetInitialGreeting();
            var response = _session.ProcessInput(message);
            return (response, greeting);
        }
        return (_session.ProcessInput(message), null);
    }

    public void Dispose() => _session.Dispose();
}

using PokeChat.Core;

var session = new ChatSession();
try
{
    session.Start();
}
finally
{
    session.Dispose();
}

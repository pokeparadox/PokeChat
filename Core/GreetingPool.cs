using PokeChat.Knowledge;

namespace PokeChat.Core;

public static class GreetingPool
{
    public static string GetRandomGreeting(KnowledgeStore knowledgeStore)
    {
        var greetings = knowledgeStore.GetGreetings();
        if (greetings.Count == 0)
        {
            return "Hello! I'm PokeChat. What's your name?";
        }

        return greetings[Random.Shared.Next(greetings.Count)].Text;
    }
}

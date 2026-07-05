using PokeChat.Knowledge;

namespace PokeChat.Core;

public static class GreetingPool
{
    public static string GetRandomGreeting(KnowledgeStore knowledgeStore, string botName, string? persona = null)
    {
        var greetings = knowledgeStore.GetGreetings(persona);
        if (greetings.Count == 0)
        {
            return $"Hello! I'm {botName}. What's your name?";
        }

        var template = greetings[Random.Shared.Next(greetings.Count)].Text;
        return template.Replace("{BOTNAME}", botName).Replace("PokeChat", botName);
    }
}

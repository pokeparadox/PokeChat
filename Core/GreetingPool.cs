using System;
using System.Collections.Generic;
using PokeChat.Knowledge;

namespace PokeChat.Core;

public static class GreetingPool
{
    private static readonly Random _random = new();

    public static string GetRandomGreeting(KnowledgeStore knowledgeStore)
    {
        var greetings = knowledgeStore.GetGreetings();
        if (greetings.Count == 0)
        {
            return "Hello! I'm PokeChat. What's your name?";
        }

        return greetings[_random.Next(greetings.Count)].Text;
    }
}

using System;
using System.Collections.Generic;
using PokeChat.Knowledge;
using PokeChat.NLP;

namespace PokeChat.Responses;

public class ResponseEngine
{
    private readonly KnowledgeStore _knowledgeStore;
    private readonly ContextTracker _context;
    private readonly Random _random;

    public ResponseEngine(KnowledgeStore knowledgeStore, ContextTracker context)
    {
        _knowledgeStore = knowledgeStore;
        _context = context;
        _random = new Random();
    }

    public string GenerateResponse(string input, int? userId)
    {
        var rule = ResponseRules.MatchRule(input, _knowledgeStore);

        if (rule != null && rule.Responses.Count > 0)
        {
            return rule.Responses[_random.Next(rule.Responses.Count)];
        }

        var tokens = Tokenizer.Tokenize(input);
        var tags = PosTagger.Tag(tokens);
        var triples = SvoExtractor.Extract(tokens, tags);

        foreach (var triple in triples)
        {
            var existingFact = _knowledgeStore.GetFact(triple.Subject, triple.Verb, triple.Object_);
            if (existingFact != null)
            {
                return $"I already know that {triple.Subject} {triple.Verb} {triple.Object_}. Did you know something new about it?";
            }
        }

        var facts = userId.HasValue ? _knowledgeStore.GetFactsByUser(userId.Value) : new List<Fact>();
        if (facts.Count > 0 && _random.Next(3) == 0)
        {
            var randomFact = facts[_random.Next(facts.Count)];
            var followUps = new List<string>
            {
                $"Speaking of {randomFact.Subject}, you mentioned they {randomFact.Verb} {randomFact.Object}. Tell me more!",
                $"I remember you said something about {randomFact.Subject}. What else?",
                $"Earlier you mentioned {randomFact.Subject} {randomFact.Verb} {randomFact.Object}. Anything new?"
            };
            return followUps[_random.Next(followUps.Count)];
        }

        var defaults = new List<string>
        {
            "Interesting! Tell me more.",
            "I see. What else is on your mind?",
            "That's fascinating. Can you elaborate?",
            "I'm listening. Go on!",
            "Hmm, that's thought-provoking. What do you think about that?",
            "I'll keep that in mind. Anything else?",
            "Thanks for sharing! What would you like to talk about next?"
        };

        return defaults[_random.Next(defaults.Count)];
    }
}

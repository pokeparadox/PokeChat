using PokeChat.Knowledge;
using PokeChat.NLP;

namespace PokeChat.Responses;

public class ResponseEngine(KnowledgeStore knowledgeStore, ContextTracker context, SpellChecker spellChecker)
{
    private readonly Random _random = new();

    public string GenerateResponse(string input, int? userId)
    {
        var unknownWordsRaw = context.GetContext("unknown_words");
        if (!string.IsNullOrEmpty(unknownWordsRaw))
        {
            var unknownWords = unknownWordsRaw
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Distinct()
                .ToList();

            context.SetContext("unknown_words", null);

            if (unknownWords.Count > 0)
            {
                var word = unknownWords[0];
                if (spellChecker.HasSuggestions(word))
                {
                    var suggestions = spellChecker.SuggestCorrections(word);
                    var suggestion = suggestions[0];
                    context.SetContext("pending_clarification_word", word);
                    context.SetContext("pending_clarification_suggestion", suggestion);
                    return $"Did you mean '{suggestion}' instead of '{word}'?";
                }
                else
                {
                    context.SetContext("pending_clarification_word", word);
                    context.SetContext("pending_clarification_suggestion", null);
                    return $"I don't know the word '{word}'. What does it mean?";
                }
            }
        }

        var rule = ResponseRules.MatchRule(input, knowledgeStore);

        if (rule != null && rule.Responses.Count > 0)
        {
            return rule.Responses[_random.Next(rule.Responses.Count)];
        }

        var tokens = Tokenizer.Tokenize(input);
        var correctedTokens = spellChecker.AutoCorrect(tokens);
        var tags = PosTagger.Tag(correctedTokens);
        var triples = SvoExtractor.Extract(correctedTokens, tags);

        foreach (var triple in triples)
        {
            var existingFact = knowledgeStore.GetFact(triple.Subject, triple.Verb, triple.Object);
            if (existingFact != null)
            {
                return $"I already know that {triple.Subject} {triple.Verb} {triple.Object}. Did you know something new about it?";
            }
        }

        if (!string.IsNullOrEmpty(context.LastSubject))
        {
            var subject = context.LastSubject;
            var contextFollowUps = new List<string>
            {
                $"Tell me more about {subject}.",
                $"What else do you know about {subject}?",
                $"You mentioned {subject}. What's on your mind?"
            };

            if (!string.IsNullOrEmpty(context.LastObject))
            {
                var obj = context.LastObject;
                contextFollowUps.Add($"You said {subject} is related to {obj}. Anything else?");
                contextFollowUps.Add($"Earlier you mentioned {subject} and {obj}. Go on!");
            }

            return contextFollowUps[_random.Next(contextFollowUps.Count)];
        }

        var facts = userId.HasValue ? knowledgeStore.GetFactsByUser(userId.Value) : new List<Fact>();
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

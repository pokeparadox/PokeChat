using PokeChat.Data;
using PokeChat.Data.Entities;

namespace PokeChat.Tests.Helpers;

internal static class TestDataHelper
{
    public static void SeedBotResponses(PokeChatDbContext db)
    {
        var now = DateTime.UtcNow.ToString("O");
        db.BotResponses.AddRange(
            new BotResponse { Category = "default_response", ResponseText = "Interesting! Tell me more.", CreatedAt = now },
            new BotResponse { Category = "default_response", ResponseText = "I see.", CreatedAt = now },
            new BotResponse { Category = "existing_fact", ResponseText = "I already know that {0} {1} {2}.", CreatedAt = now },
            new BotResponse { Category = "context_followup", ResponseText = "Tell me more about {0}.", CreatedAt = now },
            new BotResponse { Category = "context_followup_with_object", ResponseText = "Tell me more about {0} and {1}.", CreatedAt = now },
            new BotResponse { Category = "random_fact_followup", ResponseText = "You told me {0} {1} {2}. Tell me more!", CreatedAt = now },
            new BotResponse { Category = "dictionary_query_found", ResponseText = "A {0} is {1}.", CreatedAt = now },
            new BotResponse { Category = "dictionary_query_not_found", ResponseText = "I don't know what {0} means.", CreatedAt = now },
            new BotResponse { Category = "thesaurus_query_found", ResponseText = "Some words related to {0} are: {1}.", CreatedAt = now },
            new BotResponse { Category = "thesaurus_query_none", ResponseText = "I don't know of any related words.", CreatedAt = now },
            new BotResponse { Category = "link_saved", ResponseText = "I've noted that {0} is related to {1}.", CreatedAt = now },
            new BotResponse { Category = "unknown_word_suggestion", ResponseText = "Did you mean '{0}' instead of '{1}'?", CreatedAt = now },
            new BotResponse { Category = "unknown_word_no_suggestion", ResponseText = "I don't know the word '{0}'. What does it mean?", CreatedAt = now },
            new BotResponse { Category = "proactive_preference", ResponseText = "What else do you like? You mentioned {0}.", CreatedAt = now },
            new BotResponse { Category = "proactive_dislike", ResponseText = "Why don't you like {0}?", CreatedAt = now },
            new BotResponse { Category = "proactive_possession", ResponseText = "Tell me more about your {0}.", CreatedAt = now },
            new BotResponse { Category = "proactive_belief", ResponseText = "How did you learn about {0}?", CreatedAt = now },
            new BotResponse { Category = "proactive_personal", ResponseText = "You said you're {0}. What's that like?", CreatedAt = now },
            new BotResponse { Category = "proactive_general_fact", ResponseText = "You mentioned {0} {1} {2}.", CreatedAt = now },
            new BotResponse { Category = "proactive_general", ResponseText = "Tell me more about {0}.", CreatedAt = now },
            new BotResponse { Category = "proactive_statement", ResponseText = "I remember that {0} {1} {2}.", CreatedAt = now },
            new BotResponse { Category = "bot_rename_accepted", ResponseText = "Okay, from now on you can call me {0}!", CreatedAt = now },
            new BotResponse { Category = "bot_rename_accepted", ResponseText = "I like {0}! You can call me that.", CreatedAt = now },
            new BotResponse { Category = "bot_rename_rejected", ResponseText = "Hmm, I'm not sure {0} suits me. Can you think of something else?", CreatedAt = now },
            new BotResponse { Category = "bot_rename_suggestion", ResponseText = "How about the name {0}?", CreatedAt = now }
        );
        db.SaveChanges();
    }

    public static void SeedPosDictionary(PokeChatDbContext db)
    {
        var now = DateTime.UtcNow.ToString("O");
        db.PosDictionary.AddRange(
            new PosDictionaryEntry { Word = "i", WordType = "pronoun", CreatedAt = now },
            new PosDictionaryEntry { Word = "like", WordType = "verb", CreatedAt = now },
            new PosDictionaryEntry { Word = "pizza", WordType = "noun", CreatedAt = now },
            new PosDictionaryEntry { Word = "is", WordType = "verb", CreatedAt = now },
            new PosDictionaryEntry { Word = "my", WordType = "pronoun", CreatedAt = now },
            new PosDictionaryEntry { Word = "name", WordType = "noun", CreatedAt = now },
            new PosDictionaryEntry { Word = "the", WordType = "determiner", CreatedAt = now },
            new PosDictionaryEntry { Word = "cat", WordType = "noun", CreatedAt = now },
            new PosDictionaryEntry { Word = "sky", WordType = "noun", CreatedAt = now },
            new PosDictionaryEntry { Word = "blue", WordType = "adjective", CreatedAt = now },
            new PosDictionaryEntry { Word = "hate", WordType = "verb", CreatedAt = now },
            new PosDictionaryEntry { Word = "broccoli", WordType = "noun", CreatedAt = now }
        );
        db.SaveChanges();
    }
}

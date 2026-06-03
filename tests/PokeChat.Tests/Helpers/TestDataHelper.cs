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
            new BotResponse { Category = "bot_rename_suggestion", ResponseText = "How about the name {0}?", CreatedAt = now },
            new BotResponse { Category = "bot_reset_warning", ResponseText = "This will delete all our conversations and everything I've learned from you. Are you sure?", CreatedAt = now },
            new BotResponse { Category = "bot_reset_warning", ResponseText = "Are you sure you want me to forget everything we've talked about?", CreatedAt = now },
            new BotResponse { Category = "bot_reset_confirmed", ResponseText = "Done! I've forgotten everything. Let's start fresh!", CreatedAt = now },
            new BotResponse { Category = "bot_reset_confirmed", ResponseText = "All memories cleared. It's like we're meeting for the first time!", CreatedAt = now },
            new BotResponse { Category = "bot_reset_cancelled", ResponseText = "Okay, nothing was deleted. Let's continue!", CreatedAt = now },
            new BotResponse { Category = "bot_reset_cancelled", ResponseText = "No problem, I'll keep our memories safe!", CreatedAt = now },
            new BotResponse { Category = "empathy_sad", ResponseText = "I'm sorry you're feeling that way. Do you want to talk about it?", CreatedAt = now },
            new BotResponse { Category = "empathy_sad", ResponseText = "That sounds difficult. I'm here if you need someone to listen.", CreatedAt = now },
            new BotResponse { Category = "empathy_happy", ResponseText = "That's great to hear! What's making you happy?", CreatedAt = now },
            new BotResponse { Category = "empathy_happy", ResponseText = "I'm glad you're feeling good! Tell me more.", CreatedAt = now },
            new BotResponse { Category = "empathy_angry", ResponseText = "That sounds frustrating. Do you want to tell me more?", CreatedAt = now },
            new BotResponse { Category = "empathy_angry", ResponseText = "I can understand why you'd feel that way. What happened?", CreatedAt = now },
            new BotResponse { Category = "empathy_afraid", ResponseText = "That sounds worrying. I'm here if you want to talk.", CreatedAt = now },
            new BotResponse { Category = "empathy_afraid", ResponseText = "I understand feeling anxious about things. What's on your mind?", CreatedAt = now },
            new BotResponse { Category = "empathy_surprised", ResponseText = "That is surprising! Tell me more about it.", CreatedAt = now },
            new BotResponse { Category = "empathy_surprised", ResponseText = "Wow, I bet that caught you off guard! What happened?", CreatedAt = now },
            new BotResponse { Category = "emotion_followup", ResponseText = "You seemed {0} earlier. Are you feeling better now?", CreatedAt = now },
            new BotResponse { Category = "emotion_followup", ResponseText = "Last time you were feeling {0}. Has anything changed?", CreatedAt = now }
        );
        db.SaveChanges();
    }

    public static void SeedEmotionKeywords(PokeChatDbContext db)
    {
        var now = DateTime.UtcNow.ToString("O");
        db.EmotionKeywords.AddRange(
            new EmotionKeyword { Word = "happy", Sentiment = "positive", Intensity = 2, CreatedAt = now },
            new EmotionKeyword { Word = "great", Sentiment = "positive", Intensity = 2, CreatedAt = now },
            new EmotionKeyword { Word = "wonderful", Sentiment = "positive", Intensity = 3, CreatedAt = now },
            new EmotionKeyword { Word = "love", Sentiment = "positive", Intensity = 3, CreatedAt = now },
            new EmotionKeyword { Word = "sad", Sentiment = "negative", Intensity = 2, CreatedAt = now },
            new EmotionKeyword { Word = "unhappy", Sentiment = "negative", Intensity = 2, CreatedAt = now },
            new EmotionKeyword { Word = "terrible", Sentiment = "negative", Intensity = 3, CreatedAt = now },
            new EmotionKeyword { Word = "awful", Sentiment = "negative", Intensity = 3, CreatedAt = now },
            new EmotionKeyword { Word = "angry", Sentiment = "anger", Intensity = 3, CreatedAt = now },
            new EmotionKeyword { Word = "furious", Sentiment = "anger", Intensity = 5, CreatedAt = now },
            new EmotionKeyword { Word = "annoyed", Sentiment = "anger", Intensity = 2, CreatedAt = now },
            new EmotionKeyword { Word = "scared", Sentiment = "fear", Intensity = 3, CreatedAt = now },
            new EmotionKeyword { Word = "afraid", Sentiment = "fear", Intensity = 3, CreatedAt = now },
            new EmotionKeyword { Word = "worried", Sentiment = "fear", Intensity = 2, CreatedAt = now },
            new EmotionKeyword { Word = "surprised", Sentiment = "surprise", Intensity = 2, CreatedAt = now },
            new EmotionKeyword { Word = "shocked", Sentiment = "surprise", Intensity = 3, CreatedAt = now },
            new EmotionKeyword { Word = "amazed", Sentiment = "surprise", Intensity = 3, CreatedAt = now }
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
            new PosDictionaryEntry { Word = "broccoli", WordType = "noun", CreatedAt = now },
            new PosDictionaryEntry { Word = "love", WordType = "verb", CreatedAt = now },
            new PosDictionaryEntry { Word = "happy", WordType = "adjective", CreatedAt = now }
        );
        db.SaveChanges();
    }
}

using PokeChat.Data;
using PokeChat.Data.Entities;

namespace PokeChat.Tests.Helpers;

internal static class TestDataHelper
{
    public static void SeedBotResponses(PokeChatDbContext db)
    {
        var now = DateTime.UtcNow.ToString("o");
        var responses = new (string Category, string Text)[]
        {
            ("default_response", "Interesting! Tell me more."),
            ("default_response", "I see."),
            ("existing_fact", "I already know that {0} {1} {2}."),
            ("context_followup", "Tell me more about {0}."),
            ("context_followup_with_object", "Tell me more about {0} and {1}."),
            ("random_fact_followup", "You told me {0} {1} {2}. Tell me more!"),
            ("dictionary_query_found", "A {0} is {1}."),
            ("dictionary_query_not_found", "I don't know what {0} means."),
            ("thesaurus_query_found", "Some words related to {0} are: {1}."),
            ("thesaurus_query_none", "I don't know of any related words."),
            ("link_saved", "I've noted that {0} is related to {1}."),
            ("unknown_word_suggestion", "Did you mean '{0}' instead of '{1}'?"),
            ("unknown_word_no_suggestion", "I don't know the word '{0}'. What does it mean?"),
            ("proactive_preference", "What else do you like? You mentioned {0}."),
            ("proactive_dislike", "Why don't you like {0}?"),
            ("proactive_possession", "Tell me more about your {0}."),
            ("proactive_belief", "How did you learn about {0}?"),
            ("proactive_personal", "You said you're {0}. What's that like?"),
            ("proactive_general_fact", "You mentioned {0} {1} {2}."),
            ("proactive_general", "Tell me more about {0}."),
            ("proactive_statement", "I remember that {0} {1} {2}."),
            ("bot_rename_accepted", "Okay, from now on you can call me {0}!"),
            ("bot_rename_accepted", "I like {0}! You can call me that."),
            ("bot_rename_rejected", "Hmm, I'm not sure {0} suits me. Can you think of something else?"),
            ("bot_rename_suggestion", "How about the name {0}?"),
            ("bot_reset_warning", "This will delete all our conversations and everything I've learned from you. Are you sure?"),
            ("bot_reset_warning", "Are you sure you want me to forget everything we've talked about?"),
            ("bot_reset_confirmed", "Done! I've forgotten everything. Let's start fresh!"),
            ("bot_reset_confirmed", "All memories cleared. It's like we're meeting for the first time!"),
            ("bot_reset_cancelled", "Okay, nothing was deleted. Let's continue!"),
            ("bot_reset_cancelled", "No problem, I'll keep our memories safe!"),
            ("empathy_sad", "I'm sorry you're feeling that way. Do you want to talk about it?"),
            ("empathy_sad", "That sounds difficult. I'm here if you need someone to listen."),
            ("empathy_happy", "That's great to hear! What's making you happy?"),
            ("empathy_happy", "I'm glad you're feeling good! Tell me more."),
            ("empathy_angry", "That sounds frustrating. Do you want to tell me more?"),
            ("empathy_angry", "I can understand why you'd feel that way. What happened?"),
            ("empathy_afraid", "That sounds worrying. I'm here if you want to talk."),
            ("empathy_afraid", "I understand feeling anxious about things. What's on your mind?"),
            ("empathy_surprised", "That is surprising! Tell me more about it."),
            ("empathy_surprised", "Wow, I bet that caught you off guard! What happened?"),
            ("emotion_followup", "You seemed {0} earlier. Are you feeling better now?"),
            ("emotion_followup", "Last time you were feeling {0}. Has anything changed?"),
            ("temporal_fact_found", "Let me think... {0} you mentioned that {1} {2} {3}."),
            ("temporal_fact_found", "I remember! {0} you said {1} {2} {3}."),
            ("temporal_fact_none", "I don't remember anything about {0}. What did you do?"),
            ("temporal_fact_list", "From {0}, I remember: {1}"),
            ("temporal_confirmation", "I'll remember that for {0}."),
            ("inference_generalisation", "It sounds like you like {0}! You mentioned {1}."),
            ("inference_generalisation", "So you like {0}? You said you like {1}."),
            ("inference_contradiction", "Earlier you said you {0} {1}, but now you're saying you {2} {3}. Did your mind change?"),
            ("inference_contradiction", "I've noticed something - before you said you {0} {1}, and now you {2} {3}. Can you clarify?"),
            ("session_summary_short", "Today we talked about {0}. That was our main topic!"),
            ("session_summary_short", "We covered {0} in our conversation. Not bad!"),
            ("session_summary_long", "We covered a few things: {0}. Quite a chat!"),
            ("session_summary_empty", "We haven't talked about anything yet. What's on your mind?"),
            ("session_summary_end", "Before you go — today we talked about {0}. See you next time!"),
        };
        db.BotResponses.AddRange(responses.Select(r => new BotResponse
        {
            Category = r.Category,
            ResponseText = r.Text,
            CreatedAt = now
        }));
        db.SaveChanges();
    }

    public static void SeedEmotionKeywords(PokeChatDbContext db)
    {
        var now = DateTime.UtcNow.ToString("o");
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
        var now = DateTime.UtcNow.ToString("o");
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
            new PosDictionaryEntry { Word = "happy", WordType = "adjective", CreatedAt = now },
            new PosDictionaryEntry { Word = "am", WordType = "verb", CreatedAt = now },
            new PosDictionaryEntry { Word = "are", WordType = "verb", CreatedAt = now },
            new PosDictionaryEntry { Word = "have", WordType = "verb", CreatedAt = now },
            new PosDictionaryEntry { Word = "do", WordType = "verb", CreatedAt = now },
            new PosDictionaryEntry { Word = "not", WordType = "adverb", CreatedAt = now },
            new PosDictionaryEntry { Word = "going", WordType = "verb", CreatedAt = now },
            new PosDictionaryEntry { Word = "got", WordType = "verb", CreatedAt = now },
            new PosDictionaryEntry { Word = "cannot", WordType = "verb", CreatedAt = now },
            new PosDictionaryEntry { Word = "went", WordType = "verb", CreatedAt = now },
            new PosDictionaryEntry { Word = "cinema", WordType = "noun", CreatedAt = now },
            new PosDictionaryEntry { Word = "yesterday", WordType = "noun", CreatedAt = now },
            new PosDictionaryEntry { Word = "to", WordType = "preposition", CreatedAt = now },
            new PosDictionaryEntry { Word = "what", WordType = "pronoun", CreatedAt = now },
            new PosDictionaryEntry { Word = "did", WordType = "verb", CreatedAt = now },
            new PosDictionaryEntry { Word = "food", WordType = "noun", CreatedAt = now },
            new PosDictionaryEntry { Word = "burger", WordType = "noun", CreatedAt = now },
            new PosDictionaryEntry { Word = "pasta", WordType = "noun", CreatedAt = now },
            new PosDictionaryEntry { Word = "summary", WordType = "noun", CreatedAt = now },
            new PosDictionaryEntry { Word = "we", WordType = "pronoun", CreatedAt = now },
            new PosDictionaryEntry { Word = "talk", WordType = "verb", CreatedAt = now },
            new PosDictionaryEntry { Word = "about", WordType = "preposition", CreatedAt = now },
            new PosDictionaryEntry { Word = "our", WordType = "pronoun", CreatedAt = now },
            new PosDictionaryEntry { Word = "me", WordType = "pronoun", CreatedAt = now },
            new PosDictionaryEntry { Word = "summarise", WordType = "verb", CreatedAt = now },
            new PosDictionaryEntry { Word = "summarize", WordType = "verb", CreatedAt = now },
            new PosDictionaryEntry { Word = "conversation", WordType = "noun", CreatedAt = now },
            new PosDictionaryEntry { Word = "today", WordType = "noun", CreatedAt = now },
            new PosDictionaryEntry { Word = "chess", WordType = "noun", CreatedAt = now }
        );
        db.SaveChanges();
    }

    public static void SeedTemporalExpressions(PokeChatDbContext db)
    {
        db.TemporalExpressions.AddRange(
            new TemporalExpression { Expression = "today", DaysOffset = 0, IsRange = false },
            new TemporalExpression { Expression = "yesterday", DaysOffset = -1, IsRange = false },
            new TemporalExpression { Expression = "last night", DaysOffset = -1, IsRange = false },
            new TemporalExpression { Expression = "recently", DaysOffset = -7, IsRange = true },
            new TemporalExpression { Expression = "last week", DaysOffset = -7, IsRange = false },
            new TemporalExpression { Expression = "last year", DaysOffset = -365, IsRange = false }
        );
        db.SaveChanges();
    }

    public static void SeedInferenceWordLinks(PokeChatDbContext db)
    {
        var now = DateTime.UtcNow.ToString("o");
        db.WordLinks.AddRange(
            new WordLink { SourceWord = "pizza", TargetWord = "food", LinkType = "is_a", CreatedAt = now },
            new WordLink { SourceWord = "burger", TargetWord = "food", LinkType = "is_a", CreatedAt = now },
            new WordLink { SourceWord = "pasta", TargetWord = "food", LinkType = "is_a", CreatedAt = now },
            new WordLink { SourceWord = "salad", TargetWord = "food", LinkType = "is_a", CreatedAt = now },
            new WordLink { SourceWord = "coffee", TargetWord = "drink", LinkType = "is_a", CreatedAt = now },
            new WordLink { SourceWord = "tea", TargetWord = "drink", LinkType = "is_a", CreatedAt = now },
            new WordLink { SourceWord = "book", TargetWord = "thing", LinkType = "is_a", CreatedAt = now },
            new WordLink { SourceWord = "movie", TargetWord = "thing", LinkType = "is_a", CreatedAt = now }
        );
        db.SaveChanges();
    }

    public static void SeedContractions(PokeChatDbContext db)
    {
        var now = DateTime.UtcNow.ToString("o");
        db.Contractions.AddRange(
            new ContractionEntity { Contraction = "i'm", Expansion = "i am" },
            new ContractionEntity { Contraction = "you're", Expansion = "you are" },
            new ContractionEntity { Contraction = "he's", Expansion = "he is" },
            new ContractionEntity { Contraction = "she's", Expansion = "she is" },
            new ContractionEntity { Contraction = "it's", Expansion = "it is" },
            new ContractionEntity { Contraction = "we're", Expansion = "we are" },
            new ContractionEntity { Contraction = "they're", Expansion = "they are" },
            new ContractionEntity { Contraction = "i've", Expansion = "i have" },
            new ContractionEntity { Contraction = "you've", Expansion = "you have" },
            new ContractionEntity { Contraction = "we've", Expansion = "we have" },
            new ContractionEntity { Contraction = "they've", Expansion = "they have" },
            new ContractionEntity { Contraction = "i'll", Expansion = "i will" },
            new ContractionEntity { Contraction = "you'll", Expansion = "you will" },
            new ContractionEntity { Contraction = "he'll", Expansion = "he will" },
            new ContractionEntity { Contraction = "she'll", Expansion = "she will" },
            new ContractionEntity { Contraction = "it'll", Expansion = "it will" },
            new ContractionEntity { Contraction = "we'll", Expansion = "we will" },
            new ContractionEntity { Contraction = "they'll", Expansion = "they will" },
            new ContractionEntity { Contraction = "i'd", Expansion = "i would" },
            new ContractionEntity { Contraction = "you'd", Expansion = "you would" },
            new ContractionEntity { Contraction = "he'd", Expansion = "he would" },
            new ContractionEntity { Contraction = "she'd", Expansion = "she would" },
            new ContractionEntity { Contraction = "we'd", Expansion = "we would" },
            new ContractionEntity { Contraction = "they'd", Expansion = "they would" },
            new ContractionEntity { Contraction = "isn't", Expansion = "is not" },
            new ContractionEntity { Contraction = "aren't", Expansion = "are not" },
            new ContractionEntity { Contraction = "wasn't", Expansion = "was not" },
            new ContractionEntity { Contraction = "weren't", Expansion = "were not" },
            new ContractionEntity { Contraction = "don't", Expansion = "do not" },
            new ContractionEntity { Contraction = "doesn't", Expansion = "does not" },
            new ContractionEntity { Contraction = "didn't", Expansion = "did not" },
            new ContractionEntity { Contraction = "won't", Expansion = "will not" },
            new ContractionEntity { Contraction = "wouldn't", Expansion = "would not" },
            new ContractionEntity { Contraction = "can't", Expansion = "cannot" },
            new ContractionEntity { Contraction = "couldn't", Expansion = "could not" },
            new ContractionEntity { Contraction = "shouldn't", Expansion = "should not" },
            new ContractionEntity { Contraction = "mustn't", Expansion = "must not" },
            new ContractionEntity { Contraction = "needn't", Expansion = "need not" },
            new ContractionEntity { Contraction = "hasn't", Expansion = "has not" },
            new ContractionEntity { Contraction = "haven't", Expansion = "have not" },
            new ContractionEntity { Contraction = "hadn't", Expansion = "had not" },
            new ContractionEntity { Contraction = "let's", Expansion = "let us" },
            new ContractionEntity { Contraction = "gonna", Expansion = "going to" },
            new ContractionEntity { Contraction = "wanna", Expansion = "want to" },
            new ContractionEntity { Contraction = "gotta", Expansion = "got to" }
        );
        db.SaveChanges();
    }
}

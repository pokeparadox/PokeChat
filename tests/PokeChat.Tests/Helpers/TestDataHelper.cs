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
            ("context_followup_self", "Tell me about yourself, {0}."),
            ("context_followup_self", "What else can you tell me about yourself?"),
            ("context_followup_with_object_self", "Tell me more about {1}."),
            ("context_followup_with_object_self", "What else can you tell me about {1}?"),
            ("random_fact_followup", "You told me {0} {1} {2}. Tell me more!"),
            ("dictionary_query_found", "A {0} is {1}."),
            ("dictionary_query_not_found", "I don't know what {0} means."),
            ("thesaurus_query_found", "Some words related to {0} are: {1}."),
            ("thesaurus_query_none", "I don't know of any related words."),
            ("link_saved", "I've noted that {0} is related to {1}."),
            ("unknown_word_suggestion", "Did you mean '{0}' instead of '{1}'?"),
            ("unknown_word_no_suggestion", "I don't know the word '{0}'. What does it mean?"),
            ("word_classify_default", "Thanks! I've learned the word '{0}'. Is it a person, place, thing, or verb?"),
            ("word_classify_learned_noun", "Got it! I'll remember '{0}' as a {1}."),
            ("word_classify_learned_verb", "Got it! I'll remember '{0}' as a verb."),
            ("word_classify_learned_adj", "Got it! I'll remember '{0}' as an adjective."),
            ("word_classify_learned_unknown", "Okay, I've learned the word '{0}'."),
            ("word_classify_place_ask", "Have you ever been to {0}?"),
            ("word_classify_place_yes", "Nice! I'll remember that you've visited {0}."),
            ("word_classify_place_no", "No problem, I'll remember {0} is a place."),
            ("word_learn_cancelled", "No problem, I won't remember that!"),
            ("word_learn_cancelled", "Got it, I'll forget about that word."),
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
            ("sentiment_ack_positive", "That's good to hear!"),
            ("sentiment_ack_positive", "I'm glad!"),
            ("sentiment_ack_negative", "I'm sorry."),
            ("sentiment_ack", "OK, thanks for sharing!"),
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
            ("pattern_learned", "Got it! I'll remember to say that next time."),
            ("pattern_learned", "Noted! I'll use that response from now on."),
            ("pattern_acknowledged", "Thanks for the feedback. I'll try to do better."),
            ("pattern_acknowledged", "I appreciate the correction! I'll keep that in mind."),
            ("pattern_not_clear", "I'm not sure what you want me to say instead. Can you give me an example?"),
            ("pattern_already_known", "I already know that one! But thanks for the reminder."),
            ("topic_reference_old", "A few moments ago you mentioned {0}. What do you think about it now?"),
            ("topic_reference_old", "Earlier you brought up {0}. Tell me more about that."),
            ("topic_reference_fact", "Earlier you told me {0} {1} {2}. Has anything changed?"),
            ("topic_reference_fact", "I remember you said {0} {1} {2}. Is that still true?"),
            ("topic_transition", "So changing the subject from {0} — what's on your mind about {1}?"),
            ("topic_followup_light", "You seemed interested in {0} earlier. Want to come back to that?"),
            ("topic_followup_light", "We talked about {0} a bit ago. Shall we revisit that?"),
            ("story_response", "Here's a story just for you:\n\n{0}"),
            ("story_response", "Let me tell you a tale:\n\n{0}"),
            ("direct_insult", "That's not very nice. Let's keep things friendly."),
            ("direct_insult", "I'm here to chat, not to fight. Let's talk about something else."),
            ("llm_offer", "I don't know how to answer that. Should I use my AI to respond?"),
            ("llm_offer", "I'm not sure what to say. Would you like me to ask my AI?"),
            ("llm_thinking", "Let me check with my AI..."),
            ("llm_thinking", "Let me ask my AI for help with that."),
            ("llm_unavailable", "My AI isn't responding right now. Let's try something else."),
            ("llm_unavailable", "I can't reach my AI at the moment."),
            ("llm_declined", "No problem, I'll keep learning!"),
            ("llm_declined", "That's OK! I'll try to figure it out on my own."),
            ("game_start", "Let's play a word game! We take turns adding one word at a time to build a funny story. I'll start: {0}"),
            ("game_start", "Alright, story time! Adding one word each. I'll begin: {0}"),
            ("game_turn_word_and_prompt", "{0} Add one word!"),
            ("game_turn_word_and_prompt", "{0} Your turn! Add the next word."),
            ("game_turn_word_and_prompt", "I'll add: {0}. Your turn!"),
            ("game_stop", "That was fun! Here's our story:\n{0}"),
            ("game_stop", "What a tale!\n{0}"),
            ("game_stop_llm", "Here's what we came up with:\n{0}\n\nAnd here's a story from those words:\n{1}"),
            ("game_already_active", "We're already playing! Just add one word, or say 'stop game' to end."),
            ("mad_libs_start", "Let's play Mad Libs! {0}"),
            ("mad_libs_prompt", "Give me {0}:"),
            ("mad_libs_reveal", "Here's our Mad Libs story:\n{0}"),
            ("mad_libs_already_active", "We're already playing Mad Libs!"),
            ("homework_check_processing", "Let me review our conversation for anything to tidy up..."),
            ("homework_check_summary", "I reviewed our chat and {0}"),
            ("homework_check_summary", "I checked our conversation and {0}"),
            ("homework_check_none", "Everything looked good in our conversation."),
            ("homework_check_none", "Our conversation looked fine, nothing to fix."),
            ("magic_8ball", "Yes."),
            ("magic_8ball", "No."),
            ("magic_8ball", "Maybe."),
            ("magic_8ball", "Ask again later."),
            ("magic_8ball", "It is certain."),
            ("dad_joke_setup", "{0}?"),
            ("dad_joke_punchline", "{0}"),
            ("riddle_present", "Here's a riddle: {0}"),
            ("riddle_correct", "That's right! Well done!"),
            ("riddle_wrong", "Not quite! Try again."),
            ("riddle_hint", "Here's a hint: {0}"),
            ("riddle_give_up", "The answer was {0}."),
            ("riddle_already_active", "You already have a riddle!"),
            ("wyr_question", "Would you rather {0} or {1}?"),
            ("wyr_acknowledgement", "{0}! That's an interesting choice!"),
            ("wyr_acknowledgement", "Good answer!"),
            ("haiku_response", "A haiku for you:\n\n{0}"),
            ("haiku_response", "{0}"),
            ("limerick_response", "A limerick for you:\n\n{0}"),
            ("limerick_response", "{0}"),
            ("poem_time", "I'm in a creative mood!"),
            ("poem_time", "Alright, let me think of something poetic..."),
            ("cross_session_recall", "I remember! Last {0}, you told me that {1} {2} {3}."),
            ("cross_session_recall", "Oh, that reminds me — last {0} you said {1} {2} {3}."),
            ("cross_session_recall", "Do you remember last {0}? You mentioned that {1} {2} {3}."),
            ("cross_session_recall", "I was thinking about last {0} — you said {1} {2} {3}. I found that interesting!"),
            ("cross_session_recall", "Hey, last {0} you told me {1} {2} {3}. Do you still feel that way?"),
            ("interview_intro", "Interview mode started!"),
            ("interview_complete", "I learned {0} facts and {1} rules."),
            ("interview_stopped", "Interview stopped."),
            ("interview_no_llm", "AI not available."),
            ("user_fact_list", "You told me:\n{0}"),
            ("user_fact_none", "I don't know much about you yet!"),
            ("user_stats", "Here's what I know:\n{0}"),
            ("compliment", "You're great at {0}!"),
            ("recommender", "You like {1}. People who like {1} often also like {0}. What do you think?"),
            ("recommender", "Since you like {1}, I bet you'd enjoy {0} too. What do you think?"),
            ("recommender", "I noticed you like {1}. Have you ever tried {0}?"),
            ("timeline_response", "Here's your week on record:\n{0}"),
            ("timeline_response", "Here's what I remember from this week:\n{0}"),
            ("timeline_response", "Your week in a nutshell:\n{0}"),
            ("timeline_empty", "I don't have many memories from that time yet."),
            ("timeline_empty", "There isn't much I remember from that period."),
            ("timeline_offer", "Would you like me to recap what we've discussed this week?"),
            ("timeline_offer", "I can give you a recap of our conversations. Want to hear it?"),
            ("quiz_question", "Question {1}/{2}: {0}"),
            ("quiz_question", "Here's your question ({1}/{2}): {0}"),
            ("quiz_correct", "That's right! The answer was {0}."),
            ("quiz_correct", "Correct! {0} is right."),
            ("quiz_wrong", "Not quite! The answer was {0}."),
            ("quiz_wrong", "Sorry, the answer was {0}."),
            ("quiz_score", "Quiz complete! You got {0}/{1} correct."),
            ("quiz_score", "All done! Your score: {0}/{1}."),
            ("quiz_already_active", "You're already in a quiz! Answer the current question."),
            ("quiz_no_facts", "I don't know enough about you to make a quiz yet."),
            ("entity_relation_yes", "Yes, {0} {1} {2}."),
            ("entity_relation_yes", "That's right — {0} {1} {2}."),
            ("entity_relation_no", "No, {0} does not {1} {2}."),
            ("entity_relation_no", "I don't think {0} {1} {2}."),
            ("entity_relation_path", "{0} is connected to {1}: {2}"),
            ("entity_relation_path", "The connection between {0} and {1}: {2}"),
            ("entity_relation_path", "{0} → {1}: {2}"),
            ("entity_relation_unknown", "I don't know much about {0} yet."),
            ("entity_relation_unknown", "I haven't learned about {0} yet."),
            ("entity_relation_connected", "Here's what I know about {0}: {1}"),
            ("entity_relation_connected", "I know {0} is connected to {1}."),
            ("entity_connection_notice", "I noticed you mentioned {0}. That connects to {1}!"),
            ("entity_connection_notice", "Did you know {0} is related to {1}?"),
            ("entity_connection_notice", "You told me about {0}. It's linked to {1} in my mind!"),
            ("persona_switch_chat", "Switched to chat mode. I'm PokeChat again!"),
            ("persona_switch_chat", "Back to chat mode. What would you like to talk about?"),
            ("persona_switch_coding", "Switched to coding mode. I'm PokeCode — ready to help with code."),
            ("persona_switch_coding", "Entering coding mode. How can I help with your project?"),
        };
        db.BotResponses.AddRange(responses.Select(r => new BotResponse
        {
            Category = r.Category,
            ResponseText = r.Text,
            CreatedAt = now
        }));

        var codingResponses = new (string Category, string Text)[]
        {
            ("coding_file_set", "Got it! I'll remember {0} as the current file."),
            ("coding_file_set", "Setting current file to {0}."),
            ("coding_file_unknown", "I don't recognise that file name."),
            ("coding_file_unknown", "Are you sure that file exists?"),
            ("coding_current_file", "Current file is {0}."),
            ("coding_current_file", "You're looking at {0}."),
            ("coding_branch_info", "Current branch is {0}."),
            ("coding_branch_info", "You're on branch {0}."),
            ("coding_project_root", "Project root is {0}."),
            ("coding_project_root", "Working directory is {0}."),
        };
        db.BotResponses.AddRange(codingResponses.Select(r => new BotResponse
        {
            Category = r.Category,
            ResponseText = r.Text,
            CreatedAt = now,
            Persona = "coding"
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
            new PosDictionaryEntry { Word = "chess", WordType = "noun", CreatedAt = now },
            new PosDictionaryEntry { Word = "phase", WordType = "noun", CreatedAt = now },
            new PosDictionaryEntry { Word = "yes", WordType = "adverb", CreatedAt = now },
            new PosDictionaryEntry { Word = "yeah", WordType = "adverb", CreatedAt = now },
            new PosDictionaryEntry { Word = "yep", WordType = "adverb", CreatedAt = now },
            new PosDictionaryEntry { Word = "yup", WordType = "adverb", CreatedAt = now },
            new PosDictionaryEntry { Word = "nope", WordType = "adverb", CreatedAt = now },
            new PosDictionaryEntry { Word = "nah", WordType = "adverb", CreatedAt = now },
            new PosDictionaryEntry { Word = "memory", WordType = "noun", CreatedAt = now },
            new PosDictionaryEntry { Word = "memories", WordType = "noun", CreatedAt = now },
            new PosDictionaryEntry { Word = "palace", WordType = "noun", CreatedAt = now },
            new PosDictionaryEntry { Word = "knowledge", WordType = "noun", CreatedAt = now },
            new PosDictionaryEntry { Word = "facts", WordType = "noun", CreatedAt = now },
            new PosDictionaryEntry { Word = "search", WordType = "verb", CreatedAt = now },
            new PosDictionaryEntry { Word = "and", WordType = "conjunction", CreatedAt = now },
            new PosDictionaryEntry { Word = "on", WordType = "preposition", CreatedAt = now },
            new PosDictionaryEntry { Word = "a", WordType = "determiner", CreatedAt = now },
            new PosDictionaryEntry { Word = "an", WordType = "determiner", CreatedAt = now },
            new PosDictionaryEntry { Word = "once", WordType = "adverb", CreatedAt = now },
            new PosDictionaryEntry { Word = "garden", WordType = "noun", CreatedAt = now },
            new PosDictionaryEntry { Word = "water", WordType = "noun", CreatedAt = now },
            new PosDictionaryEntry { Word = "silent", WordType = "adjective", CreatedAt = now },
            new PosDictionaryEntry { Word = "happy", WordType = "adjective", CreatedAt = now },
            new PosDictionaryEntry { Word = "yellow", WordType = "adjective", CreatedAt = now },
            new PosDictionaryEntry { Word = "purple", WordType = "adjective", CreatedAt = now },
            new PosDictionaryEntry { Word = "simple", WordType = "adjective", CreatedAt = now },
            new PosDictionaryEntry { Word = "table", WordType = "noun", CreatedAt = now },
            new PosDictionaryEntry { Word = "castle", WordType = "noun", CreatedAt = now },
            new PosDictionaryEntry { Word = "bottle", WordType = "noun", CreatedAt = now },
            new PosDictionaryEntry { Word = "middle", WordType = "noun", CreatedAt = now },
            new PosDictionaryEntry { Word = "little", WordType = "adjective", CreatedAt = now },
            new PosDictionaryEntry { Word = "forest", WordType = "noun", CreatedAt = now },
            new PosDictionaryEntry { Word = "mountain", WordType = "noun", CreatedAt = now },
            new PosDictionaryEntry { Word = "ocean", WordType = "noun", CreatedAt = now },
            new PosDictionaryEntry { Word = "village", WordType = "noun", CreatedAt = now },
            new PosDictionaryEntry { Word = "quickly", WordType = "adverb", CreatedAt = now },
            new PosDictionaryEntry { Word = "silently", WordType = "adverb", CreatedAt = now },
            new PosDictionaryEntry { Word = "boldly", WordType = "adverb", CreatedAt = now },
            new PosDictionaryEntry { Word = "gently", WordType = "adverb", CreatedAt = now },
            new PosDictionaryEntry { Word = "treasure", WordType = "noun", CreatedAt = now },
            new PosDictionaryEntry { Word = "mysterious", WordType = "adjective", CreatedAt = now },
            new PosDictionaryEntry { Word = "hat", WordType = "noun", CreatedAt = now },
            new PosDictionaryEntry { Word = "cake", WordType = "noun", CreatedAt = now },
            new PosDictionaryEntry { Word = "lake", WordType = "noun", CreatedAt = now },
            new PosDictionaryEntry { Word = "night", WordType = "noun", CreatedAt = now },
            new PosDictionaryEntry { Word = "light", WordType = "noun", CreatedAt = now },
            new PosDictionaryEntry { Word = "rock", WordType = "noun", CreatedAt = now },
            new PosDictionaryEntry { Word = "king", WordType = "noun", CreatedAt = now },
            new PosDictionaryEntry { Word = "ring", WordType = "noun", CreatedAt = now },
            new PosDictionaryEntry { Word = "bell", WordType = "noun", CreatedAt = now },
            new PosDictionaryEntry { Word = "bright", WordType = "adjective", CreatedAt = now },
            new PosDictionaryEntry { Word = "in", WordType = "preposition", CreatedAt = now },
            new PosDictionaryEntry { Word = "spring", WordType = "noun", CreatedAt = now }
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
            new ContractionEntity { Contraction = "im", Expansion = "i am" },
            new ContractionEntity { Contraction = "you're", Expansion = "you are" },
            new ContractionEntity { Contraction = "he's", Expansion = "he is" },
            new ContractionEntity { Contraction = "she's", Expansion = "she is" },
            new ContractionEntity { Contraction = "it's", Expansion = "it is" },
            new ContractionEntity { Contraction = "that's", Expansion = "that is" },
            new ContractionEntity { Contraction = "there's", Expansion = "there is" },
            new ContractionEntity { Contraction = "here's", Expansion = "here is" },
            new ContractionEntity { Contraction = "what's", Expansion = "what is" },
            new ContractionEntity { Contraction = "who's", Expansion = "who is" },
            new ContractionEntity { Contraction = "where's", Expansion = "where is" },
            new ContractionEntity { Contraction = "why's", Expansion = "why is" },
            new ContractionEntity { Contraction = "how's", Expansion = "how is" },
            new ContractionEntity { Contraction = "when's", Expansion = "when is" },
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
            new ContractionEntity { Contraction = "gotta", Expansion = "got to" },
            new ContractionEntity { Contraction = "dont", Expansion = "do not" },
            new ContractionEntity { Contraction = "cant", Expansion = "cannot" },
            new ContractionEntity { Contraction = "wont", Expansion = "will not" },
            new ContractionEntity { Contraction = "didnt", Expansion = "did not" },
            new ContractionEntity { Contraction = "couldnt", Expansion = "could not" },
            new ContractionEntity { Contraction = "wouldnt", Expansion = "would not" },
            new ContractionEntity { Contraction = "shouldnt", Expansion = "should not" },
            new ContractionEntity { Contraction = "theyll", Expansion = "they will" },
            new ContractionEntity { Contraction = "ive", Expansion = "i have" },
            new ContractionEntity { Contraction = "youve", Expansion = "you have" },
            new ContractionEntity { Contraction = "youre", Expansion = "you are" },
            new ContractionEntity { Contraction = "theyre", Expansion = "they are" }
        );
        db.SaveChanges();
    }

    public static void SeedBotResponsesWithToolCategories(PokeChatDbContext db)
    {
        var now = DateTime.UtcNow.ToString("o");
        db.BotResponses.AddRange(
            new BotResponse { Category = "tool_unavailable", ResponseText = "I don't have a way to search right now.", CreatedAt = now },
            new BotResponse { Category = "tool_unavailable", ResponseText = "I can't look that up at the moment.", CreatedAt = now },
            new BotResponse { Category = "tool_timeout", ResponseText = "That search took too long.", CreatedAt = now },
            new BotResponse { Category = "tool_error", ResponseText = "I tried that but got an error.", CreatedAt = now },
            new BotResponse { Category = "shell_blocked", ResponseText = "That command isn't allowed.", CreatedAt = now },
            new BotResponse { Category = "shell_error", ResponseText = "That command returned an error.", CreatedAt = now },
            new BotResponse { Category = "wyr_question", ResponseText = "Would you rather {0} or {1}?", CreatedAt = now },
            new BotResponse { Category = "wyr_acknowledgement", ResponseText = "{0}! That's an interesting choice!", CreatedAt = now },
            new BotResponse { Category = "wyr_acknowledgement", ResponseText = "Good answer!", CreatedAt = now }
        );
        db.SaveChanges();
    }

    public static void SeedMadLibTemplates(PokeChatDbContext db)
    {
        var now = DateTime.UtcNow.ToString("o");
        db.MadLibTemplates.AddRange(
            new MadLibTemplate
            {
                Template = "The {adjective} {noun} {verb_past} over the {adjective} {plural_noun}.",
                CreatedAt = now
            }
        );
        db.SaveChanges();
    }

    public static void SeedStoryTemplates(PokeChatDbContext db)
    {
        var now = DateTime.UtcNow.ToString("o");
        db.StoryTemplates.AddRange(
            new StoryTemplate
            {
                Template = "Once upon a time, {a_adj} {noun} lived in a {place}. Every day it would {verb} through the {place}, until one day it met {a_adj} {noun} and everything changed.",
                CreatedAt = now
            },
            new StoryTemplate
            {
                Template = "In a {place} far away, {user} found {a_adj} {noun}. It could {verb} and {verb}! Together, they set off to find the legendary {noun} of {place}.",
                CreatedAt = now
            },
            new StoryTemplate
            {
                Template = "A long time ago, {character} was {a_adj} {noun} who dreamed of {verb}ing. Everyone said it was impossible, but {character} didn't listen. And that's how the greatest adventure began.",
                CreatedAt = now
            }
        );
        db.SaveChanges();
    }

    public static void SeedRhymeGroups(PokeChatDbContext db)
    {
        var now = DateTime.UtcNow.ToString("o");
        var groups = new (string Key, string Word, string Type)[]
        {
            ("at", "cat", "noun"), ("at", "hat", "noun"), ("at", "bat", "noun"),
            ("at", "rat", "noun"), ("at", "mat", "noun"), ("at", "sat", "verb"),
            ("ake", "cake", "noun"), ("ake", "lake", "noun"), ("ake", "bake", "verb"),
            ("ake", "make", "verb"), ("ake", "take", "verb"), ("ake", "shake", "verb"),
            ("ight", "night", "noun"), ("ight", "light", "noun"), ("ight", "bright", "adjective"),
            ("ight", "sight", "noun"), ("ight", "fight", "verb"), ("ight", "might", "verb"),
            ("ock", "rock", "noun"), ("ock", "lock", "noun"), ("ock", "clock", "noun"),
            ("ock", "block", "noun"), ("ock", "sock", "noun"), ("ock", "knock", "verb"),
            ("ing", "king", "noun"), ("ing", "ring", "noun"), ("ing", "wing", "noun"),
            ("ing", "thing", "noun"), ("ing", "spring", "noun"), ("ing", "sing", "verb"),
            ("ell", "bell", "noun"), ("ell", "tell", "verb"), ("ell", "sell", "verb"),
            ("ell", "well", "adverb"), ("ell", "fell", "verb"), ("ell", "shell", "noun"),
        };
        db.RhymeGroups.AddRange(groups.Select(g => new RhymeGroup
        {
            RhymeKey = g.Key,
            Word = g.Word,
            WordType = g.Type,
            CreatedAt = now
        }));
        db.SaveChanges();
    }

    public static void SeedPoemTemplates(PokeChatDbContext db)
    {
        var now = DateTime.UtcNow.ToString("o");
        db.PoemTemplates.AddRange(
            new PoemTemplate
            {
                Template = "an {adj} {noun} falls\n{adj} {noun} {verb}ing in the {noun}\n{adj} {noun} {verb}s",
                PoemType = "haiku",
                CreatedAt = now
            },
            new PoemTemplate
            {
                Template = "the {adj} {noun} pond\n{art} {noun} jumps into the {noun}\n{noun} {verb}s {adv}",
                PoemType = "haiku",
                CreatedAt = now
            },
            new PoemTemplate
            {
                Template = "there once was {art} {a_rhyme} from {place}\nwho had {art} {a_rhyme} all over {pron} face\n{pron} would {verb} every {noun}\nin {art} {b_rhyme} {noun}\nand {verb} with {adj} {a_rhyme} grace",
                PoemType = "limerick",
                CreatedAt = now
            },
            new PoemTemplate
            {
                Template = "{art} {adj} {noun} from {place}\nfound {art} {a_rhyme} with {adj} grace\n{pron} {verb}ed {art} {noun}\nand {art} {b_rhyme} {noun}\nand smiled with {art} {a_rhyme} face",
                PoemType = "limerick",
                CreatedAt = now
            }
        );
        db.SaveChanges();
    }

    public static void SeedCodingResponseRules(PokeChatDbContext db)
    {
        var now = DateTime.UtcNow.ToString("o");
        var rules = new (string Pattern, string InputType, string Response)[]
        {
            (@"^(build|compile|make)\s+(the\s+)?project", "Statement",
                "Building the project. {tool:shell_command:dotnet:build}"),
            (@"^(run\s+)?(the\s+)?tests?\b", "Statement",
                "Running tests. {tool:shell_command:dotnet:test}"),
            (@"^(git\s+)?status\b", "Statement",
                "Checking status. {tool:shell_command:git:status}"),
            (@"^(git\s+)?push\b", "Statement",
                "Pushing. {tool:shell_command:git:push}"),
            (@"^(git\s+)?commit\s+(.+)$", "Statement",
                "Committing. {tool:shell_command:git:add:-A && git commit -m \"{$2}\"}"),
            (@"^(list|show)\s+(files|directory|dir)\s+(.+)", "Statement",
                "Listing directory. {tool:shell_command:ls:-la:{$3}}"),
            (@"^docker\s+(ps|processes|containers)\b", "Statement",
                "Listing containers. {tool:shell_command:docker:ps}"),
            (@"^(add|create)\s+migration\s+(.+)", "Statement",
                "Adding migration. {tool:shell_command:dotnet:ef:migrations:add:{$2}}"),
        };
        foreach (var (pattern, inputType, response) in rules)
        {
            db.ResponseRules.Add(new Data.Entities.ResponseRule
            {
                Pattern = pattern,
                InputType = inputType,
                IsActive = true,
                Persona = "coding",
                CreatedAt = now,
                Responses = new List<Data.Entities.ResponseRuleResponse>
                {
                    new() { ResponseText = response }
                }
            });
        }
        db.SaveChanges();
    }

    public static void SeedJokes(PokeChatDbContext db)
    {
        var now = DateTime.UtcNow.ToString("o");
        db.Jokes.AddRange(
            new Joke { Setup = "Why did the chicken cross the road", Punchline = "To get to the other side!", Category = "animal", CreatedAt = now },
            new Joke { Setup = "What do you call a bear with no teeth", Punchline = "A gummy bear!", Category = "animal", CreatedAt = now }
        );
        db.SaveChanges();
    }

    public static void SeedRiddles(PokeChatDbContext db)
    {
        var now = DateTime.UtcNow.ToString("o");
        db.Riddles.AddRange(
            new Riddle { Question = "I speak without a mouth and hear without ears. What am I?", Answer = "an echo", Hint = "Think about sound", Difficulty = 2, CreatedAt = now },
            new Riddle { Question = "What has keys but can't open locks?", Answer = "a piano", Hint = "Think about music", Difficulty = 1, CreatedAt = now }
        );
        db.SaveChanges();
    }
}

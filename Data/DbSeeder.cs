using System.Text.Json;
using PokeChat.Data.Entities;

namespace PokeChat.Data;

public static class DbSeeder
{
    public static void Seed(PokeChatDbContext context)
    {
        var now = DateTime.UtcNow.ToString("o");

        SeedGreetings(context, now);
        SeedGreetingWords(context, now);
        SeedResponseRules(context, now);
        SeedPosDictionary(context, now);
        SeedNamePatterns(context, now);
        SeedBotCommands(context, now);
        SeedMisspellings(context, now);
        SeedNounCategories(context, now);
        SeedBotRenamePatterns(context, now);
        SeedEmotionKeywords(context, now);
        SeedContractions(context, now);
        SeedTemporalExpressions(context);
        SeedInferenceWordLinks(context, now);
        SeedBotResponses(context, now);

        context.SaveChanges();
    }

    private static void SeedGreetings(PokeChatDbContext context, string now)
    {
        if (context.Greetings.Any()) return;

        var greetings = new[]
        {
            "Hello! I'm PokeChat. What's your name?",
            "Hi there! I'm PokeChat. Who am I chatting with?",
            "Hey! Welcome to PokeChat. What should I call you?",
            "Greetings! I'm PokeChat. May I know your name?",
            "Hi! I'm PokeChat, a chat bot that learns from our conversations. What's your name?",
            "Hello! Nice to meet you. I'm PokeChat. Who are you?",
            "Hey there! I'm PokeChat. Tell me your name and let's chat!"
        };

        context.Greetings.AddRange(greetings.Select(g => new Greeting
        {
            Text = g,
            IsSystem = true,
            CreatedAt = now
        }));
    }

    private static void SeedGreetingWords(PokeChatDbContext context, string now)
    {
        if (context.GreetingWords.Any()) return;

        var words = new[] { "hi", "hello", "hey", "howdy", "greetings", "sup", "yo" };

        context.GreetingWords.AddRange(words.Select(w => new GreetingWord
        {
            Word = w,
            CreatedAt = now
        }));
    }

    private static void SeedResponseRules(PokeChatDbContext context, string now)
    {
        if (context.ResponseRules.Any()) return;

        var rules = new (string Pattern, string InputType, string[] Responses)[]
        {
            (@"^(hi|hello|hey|howdy|greetings|good morning|good afternoon|good evening|sup|yo)", "Greeting", new[]
            {
                "Hello there! How are you doing today?",
                "Hi! Nice to chat with you.",
                "Hey! What's on your mind?",
                "Greetings! What would you like to talk about?",
                "Hello! I'm here and ready to chat."
            }),
            ("my name is", "Statement", new[]
            {
                "Nice to meet you! I'll remember that.",
                "Got it! I'll keep that in mind.",
                "Thanks for telling me! What else would you like to share?",
                "I've noted that down. Tell me more about yourself!"
            }),
            ("i like", "Statement", new[]
            {
                "That's interesting! I'll remember you like that.",
                "Good to know! What else do you enjoy?",
                "Noted! Tell me more about your interests.",
                "Interesting choice! Why do you like that?"
            }),
            (@"i (love|enjoy|prefer)", "Statement", new[]
            {
                "That's great! I'll remember that.",
                "Nice! What else do you enjoy?",
                "Interesting! Tell me more about that."
            }),
            (@"i (hate|dislike|can't stand)", "Statement", new[]
            {
                "I see. I'll keep that in mind.",
                "Noted. What do you like instead?",
                "Understood. Let's talk about something else!"
            }),
            (@"(what|who|where|when|why|how|do you|are you|is it|can you|will you)", "Question", new[]
            {
                "That's a good question. Let me think about what I know...",
                "Hmm, I'm not sure I have an answer for that yet.",
                "I don't know that yet, but I'm always learning!",
                "Interesting question! What do you think?"
            }),
            (@"(thank|thanks)", "Statement", new[]
            {
                "You're welcome!",
                "Happy to help!",
                "Anytime!",
                "No problem at all!"
            }),
            (@"(bye|goodbye|see you|farewell|good night)", "Greeting", new[]
            {
                "Goodbye! It was nice chatting with you.",
                "See you later! Take care.",
                "Bye! Come back anytime.",
                "Farewell! I'll be here when you return."
            }),
            (@"my (dog|cat|pet) (is|was|named|name)", "Statement", new[]
            {
                "That's cute! I'll remember your pet's name.",
                "Aww, nice! I've noted that down.",
                "Pets are great! I'll keep that in mind."
            }),
            (@"the .* is", "Statement", new[]
            {
                "Interesting fact! I'll remember that.",
                "Good to know! I've stored that away.",
                "Noted! Tell me something else."
            }),
            (@"(what did I do|what happened|tell me about)\s+(yesterday|today|earlier|last night|this week|last week|this month|last month|recently|lately|a while ago|long ago|last year)",
                "Question", new[]
            {
                "Let me check my memories from that time...",
                "I'll look through what I remember from that period."
            })
        };

        foreach (var (pattern, inputType, responses) in rules)
        {
            var rule = new ResponseRule
            {
                Pattern = pattern,
                InputType = inputType,
                IsActive = true,
                CreatedAt = now,
                Responses = responses.Select(r => new ResponseRuleResponse
                {
                    ResponseText = r
                }).ToList()
            };
            context.ResponseRules.Add(rule);
        }
    }

    private static void SeedPosDictionary(PokeChatDbContext context, string now)
    {
        if (context.PosDictionary.Any()) return;

        var jsonPath = ResolveDataFilePath("pos_dictionary.json");
        var json = File.ReadAllText(jsonPath);
        var entries = JsonSerializer.Deserialize<List<PosDictionaryEntryJson>>(json)
            ?? throw new InvalidOperationException("Failed to load pos_dictionary.json");

        context.PosDictionary.AddRange(entries.Select(e => new PosDictionaryEntry
        {
            Word = e.Word,
            WordType = e.Type,
            CreatedAt = now
        }));
    }

    private static string ResolveDataFilePath(string fileName)
    {
        var outputPath = Path.Combine(AppContext.BaseDirectory, "Data", fileName);
        if (File.Exists(outputPath))
            return outputPath;

        var root = ProjectPathHelper.FindProjectRoot(AppContext.BaseDirectory);
        if (root != null)
            return Path.Combine(root, "Data", fileName);

        return outputPath;
    }

    private class PosDictionaryEntryJson
    {
        public string Word { get; set; } = "";
        public string Type { get; set; } = "";
    }

    private static void SeedNamePatterns(PokeChatDbContext context, string now)
    {
        if (context.NamePatterns.Any()) return;

        var patterns = new[] { "my name is", "i am", "i'm", "call me", "name is" };

        context.NamePatterns.AddRange(patterns.Select(p => new NamePattern
        {
            Pattern = p,
            CreatedAt = now
        }));
    }

    private static void SeedBotCommands(PokeChatDbContext context, string now)
    {
        if (context.BotCommands.Any()) return;

        var commands = new[] { "quit", "exit" };

        context.BotCommands.AddRange(commands.Select(c => new BotCommand
        {
            Command = c,
            CreatedAt = now
        }));
    }

    private static void SeedMisspellings(PokeChatDbContext context, string now)
    {
        if (context.Misspellings.Any()) return;

        var misspellings = new (string Misspelling, string Correction)[]
        {
            ("teh", "the"),
            ("recieve", "receive"),
            ("beleive", "believe"),
            ("wierd", "weird"),
            ("adress", "address"),
            ("calender", "calendar"),
            ("definately", "definitely"),
            ("occured", "occurred"),
            ("seperate", "separate"),
            ("tommorow", "tomorrow"),
            ("alot", "a lot"),
            ("untill", "until"),
            ("wich", "which"),
            ("acomodate", "accommodate"),
            ("acheive", "achieve"),
            ("apparant", "apparent"),
            ("begining", "beginning"),
            ("carreer", "career"),
            ("catagory", "category"),
            ("commitee", "committee"),
            ("concensus", "consensus"),
            ("dael", "deal"),
            ("decaffinated", "decaffeinated"),
            ("embarass", "embarrass"),
            ("enviroment", "environment"),
            ("excercise", "exercise"),
            ("famoust", "famous"),
            ("foward", "forward"),
            ("freind", "friend"),
            ("goverment", "government"),
            ("guage", "gauge"),
            ("harrass", "harass"),
            ("independant", "independent"),
            ("jewelery", "jewelry"),
            ("judgement", "judgment"),
            ("knowlege", "knowledge"),
            ("liason", "liaison"),
            ("libary", "library"),
            ("lisence", "license"),
            ("maintainance", "maintenance"),
            ("millenium", "millennium"),
            ("mispell", "misspell"),
            ("neccessary", "necessary"),
            ("ninty", "ninety"),
            ("nucleur", "nuclear"),
            ("occassion", "occasion"),
            ("oppurtunity", "opportunity"),
            ("paralel", "parallel"),
            ("particurly", "particularly"),
            ("perminent", "permanent"),
            ("persistant", "persistent"),
            ("personel", "personnel"),
            ("posession", "possession"),
            ("prefered", "preferred"),
            ("priveledge", "privilege"),
            ("probly", "probably"),
            ("proffessor", "professor"),
            ("pronounciation", "pronunciation"),
            ("publicaly", "publicly"),
            ("reccomend", "recommend"),
            ("refered", "referred"),
            ("relevent", "relevant"),
            ("religous", "religious"),
            ("rember", "remember"),
            ("remeber", "remember"),
            ("resistence", "resistance"),
            ("restaraunt", "restaurant"),
            ("sargent", "sergeant"),
            ("scedule", "schedule"),
            ("seige", "siege"),
            ("similer", "similar"),
            ("sincerly", "sincerely"),
            ("speach", "speech"),
            ("sucess", "success"),
            ("surprize", "surprise"),
            ("truely", "truly"),
            ("twelth", "twelfth"),
            ("tyw", "typo"),
            ("unfortunatly", "unfortunately"),
            ("usally", "usually"),
            ("vacume", "vacuum"),
            ("vell", "well"),
            ("visious", "vicious"),
            ("welcom", "welcome"),
            ("wensday", "wednesday"),
            ("writen", "written"),
            ("writting", "writing"),
            ("yatch", "yacht"),
        };

        context.Misspellings.AddRange(misspellings.Select(m => new Misspelling
        {
            WrongWord = m.Misspelling,
            Correction = m.Correction,
            CreatedAt = now
        }));
    }

    private static void SeedNounCategories(PokeChatDbContext context, string now)
    {
        if (context.NounCategories.Any()) return;

        var categories = new (string Noun, string Category)[]
        {
            ("alice", "person"),
            ("bob", "person"),
            ("charlie", "person"),
            ("david", "person"),
            ("emma", "person"),
            ("london", "place"),
            ("paris", "place"),
            ("school", "place"),
            ("park", "place"),
            ("hospital", "place"),
            ("table", "thing"),
            ("book", "thing"),
            ("car", "thing"),
            ("pizza", "thing"),
            ("computer", "thing"),
        };

        context.NounCategories.AddRange(categories.Select(c => new NounCategory
        {
            Noun = c.Noun,
            Category = c.Category,
            CreatedAt = now
        }));
    }

    private static void SeedBotRenamePatterns(PokeChatDbContext context, string now)
    {
        if (context.BotRenamePatterns.Any()) return;

        var patterns = new[] { "can i call you", "i'll call you", "i will call you", "your name is" };

        context.BotRenamePatterns.AddRange(patterns.Select(p => new BotRenamePattern
        {
            Pattern = p,
            CreatedAt = now
        }));
    }

    private static void SeedEmotionKeywords(PokeChatDbContext context, string now)
    {
        if (context.EmotionKeywords.Any()) return;

        var keywords = new (string Word, string Sentiment, int Intensity)[]
        {
            ("happy", "positive", 2),
            ("great", "positive", 2),
            ("wonderful", "positive", 3),
            ("love", "positive", 3),
            ("awesome", "positive", 3),
            ("fantastic", "positive", 3),
            ("brilliant", "positive", 3),
            ("amazing", "positive", 3),
            ("delightful", "positive", 2),
            ("excellent", "positive", 3),
            ("glad", "positive", 2),
            ("pleased", "positive", 2),
            ("cheerful", "positive", 2),
            ("excited", "positive", 3),
            ("joy", "positive", 3),
            ("lovely", "positive", 2),
            ("nice", "positive", 1),
            ("beautiful", "positive", 2),
            ("perfect", "positive", 3),
            ("fabulous", "positive", 3),
            ("splendid", "positive", 3),
            ("terrific", "positive", 3),
            ("marvellous", "positive", 3),
            ("magnificent", "positive", 3),
            ("superb", "positive", 3),
            ("grand", "positive", 2),
            ("fine", "positive", 1),
            ("good", "positive", 1),
            ("joyful", "positive", 3),
            ("thrilled", "positive", 4),
            ("elated", "positive", 4),
            ("ecstatic", "positive", 5),
            ("overjoyed", "positive", 5),
            ("sad", "negative", 2),
            ("unhappy", "negative", 2),
            ("terrible", "negative", 3),
            ("awful", "negative", 3),
            ("horrible", "negative", 3),
            ("miserable", "negative", 4),
            ("upset", "negative", 2),
            ("disappointed", "negative", 2),
            ("gloomy", "negative", 2),
            ("depressed", "negative", 4),
            ("lonely", "negative", 3),
            ("sorry", "negative", 1),
            ("bad", "negative", 1),
            ("worst", "negative", 3),
            ("dreadful", "negative", 3),
            ("grim", "negative", 2),
            ("dismal", "negative", 2),
            ("sorrowful", "negative", 3),
            ("heartbroken", "negative", 5),
            ("devastated", "negative", 5),
            ("angry", "anger", 3),
            ("furious", "anger", 5),
            ("annoyed", "anger", 2),
            ("frustrated", "anger", 3),
            ("irritated", "anger", 2),
            ("mad", "anger", 3),
            ("outraged", "anger", 5),
            ("livid", "anger", 4),
            ("cross", "anger", 2),
            ("fuming", "anger", 4),
            ("rage", "anger", 5),
            ("infuriated", "anger", 5),
            ("enraged", "anger", 5),
            ("irate", "anger", 4),
            ("incensed", "anger", 4),
            ("scared", "fear", 3),
            ("afraid", "fear", 3),
            ("worried", "fear", 2),
            ("anxious", "fear", 2),
            ("nervous", "fear", 2),
            ("terrified", "fear", 5),
            ("frightened", "fear", 4),
            ("panicked", "fear", 4),
            ("fearful", "fear", 3),
            ("concerned", "fear", 1),
            ("alarmed", "fear", 3),
            ("uneasy", "fear", 2),
            ("apprehensive", "fear", 2),
            ("tense", "fear", 2),
            ("surprised", "surprise", 2),
            ("shocked", "surprise", 3),
            ("amazed", "surprise", 3),
            ("astonished", "surprise", 3),
            ("stunned", "surprise", 3),
            ("unexpected", "surprise", 1),
            ("incredible", "surprise", 3),
            ("unbelievable", "surprise", 3),
            ("wow", "surprise", 2),
            ("startled", "surprise", 2),
            ("dumbfounded", "surprise", 4),
            ("astounded", "surprise", 3),
            ("flabbergasted", "surprise", 4),
        };

        context.EmotionKeywords.AddRange(keywords.Select(k => new EmotionKeyword
        {
            Word = k.Word,
            Sentiment = k.Sentiment,
            Intensity = k.Intensity,
            CreatedAt = now
        }));
    }

    private static void SeedContractions(PokeChatDbContext context, string now)
    {
        if (context.Contractions.Any()) return;

        var contractions = new (string Contraction, string Expansion)[]
        {
            ("i'm", "i am"),
            ("you're", "you are"),
            ("he's", "he is"),
            ("she's", "she is"),
            ("it's", "it is"),
            ("that's", "that is"),
            ("there's", "there is"),
            ("here's", "here is"),
            ("what's", "what is"),
            ("who's", "who is"),
            ("where's", "where is"),
            ("why's", "why is"),
            ("how's", "how is"),
            ("when's", "when is"),
            ("we're", "we are"),
            ("they're", "they are"),
            ("i've", "i have"),
            ("you've", "you have"),
            ("we've", "we have"),
            ("they've", "they have"),
            ("i'll", "i will"),
            ("you'll", "you will"),
            ("he'll", "he will"),
            ("she'll", "she will"),
            ("it'll", "it will"),
            ("we'll", "we will"),
            ("they'll", "they will"),
            ("i'd", "i would"),
            ("you'd", "you would"),
            ("he'd", "he would"),
            ("she'd", "she would"),
            ("we'd", "we would"),
            ("they'd", "they would"),
            ("isn't", "is not"),
            ("aren't", "are not"),
            ("wasn't", "was not"),
            ("weren't", "were not"),
            ("don't", "do not"),
            ("doesn't", "does not"),
            ("didn't", "did not"),
            ("won't", "will not"),
            ("wouldn't", "would not"),
            ("can't", "cannot"),
            ("couldn't", "could not"),
            ("shouldn't", "should not"),
            ("mustn't", "must not"),
            ("needn't", "need not"),
            ("hasn't", "has not"),
            ("haven't", "have not"),
            ("hadn't", "had not"),
            ("let's", "let us"),
            ("gonna", "going to"),
            ("wanna", "want to"),
            ("gotta", "got to"),
        };

        context.Contractions.AddRange(contractions.Select(c => new ContractionEntity
        {
            Contraction = c.Contraction,
            Expansion = c.Expansion
        }));
    }

    private static void SeedTemporalExpressions(PokeChatDbContext context)
    {
        if (context.TemporalExpressions.Any()) return;

        var expressions = new (string Expression, int DaysOffset, bool IsRange)[]
        {
            ("today", 0, false),
            ("now", 0, false),
            ("just now", 0, false),
            ("earlier", 0, true),
            ("yesterday", -1, false),
            ("last night", -1, false),
            ("this week", -7, true),
            ("last week", -7, false),
            ("this month", -30, true),
            ("last month", -30, false),
            ("recently", -7, true),
            ("lately", -7, true),
            ("a while ago", -30, true),
            ("long ago", -365, true),
            ("last year", -365, false),
        };

        context.TemporalExpressions.AddRange(expressions.Select(e => new TemporalExpression
        {
            Expression = e.Expression,
            DaysOffset = e.DaysOffset,
            IsRange = e.IsRange
        }));
    }

    private static void SeedInferenceWordLinks(PokeChatDbContext context, string now)
    {
        if (context.WordLinks.Any()) return;

        var links = new (string SourceWord, string TargetWord)[]
        {
            ("pizza", "food"),
            ("burger", "food"),
            ("pasta", "food"),
            ("salad", "food"),
            ("coffee", "drink"),
            ("tea", "drink"),
            ("juice", "drink"),
            ("dog", "animal"),
            ("cat", "animal"),
            ("bird", "animal"),
            ("fish", "animal"),
            ("book", "thing"),
            ("movie", "thing"),
            ("song", "thing"),
            ("game", "thing"),
        };

        context.WordLinks.AddRange(links.Select(l => new WordLink
        {
            SourceWord = l.SourceWord,
            TargetWord = l.TargetWord,
            LinkType = "is_a",
            CreatedAt = now
        }));
    }

    private static void SeedBotResponses(PokeChatDbContext context, string now)
    {
        if (context.BotResponses.Any()) return;

        var responses = new (string Category, string ResponseText)[]
        {
            ("unknown_word_suggestion", "Did you mean '{0}' instead of '{1}'?"),
            ("unknown_word_no_suggestion", "I don't know the word '{0}'. What does it mean?"),
            ("existing_fact", "I already know that {0} {1} {2}."),
            ("existing_fact", "That's already in my memory. Tell me something new!"),
            ("existing_fact", "I know that already. What else can I learn?"),
            ("context_followup", "Tell me more about {0}."),
            ("context_followup", "What else do you know about {0}?"),
            ("context_followup", "You mentioned {0}. What's on your mind?"),
            ("context_followup_with_object", "Tell me more about {0} and {1}."),
            ("context_followup_with_object", "What else can you share about {0} and {1}?"),
            ("random_fact_followup", "You told me {0} {1} {2}. Tell me more!"),
            ("random_fact_followup", "I remember you said something about {0}. What else?"),
            ("random_fact_followup", "You mentioned {0} {1} {2}. Anything new about that?"),
            ("default_response", "Interesting! Tell me more."),
            ("default_response", "I see. What else is on your mind?"),
            ("default_response", "That's fascinating. Can you elaborate?"),
            ("default_response", "I'm listening. Go on!"),
            ("default_response", "Hmm, that's thought-provoking. What do you think about that?"),
            ("default_response", "I'll keep that in mind. Anything else?"),
            ("default_response", "Thanks for sharing! What would you like to talk about next?"),
            ("math_result", "{0} = {1}"),
            ("math_result", "The answer is {0} = {1}."),
            ("math_result", "{0} equals {1}."),
            ("math_correction", "Actually, {0} = {1}, not {2}."),
            ("math_correction", "I think you mean {0} = {1}. {2} doesn't seem right."),
            ("math_confirmation", "That's right! {0} = {1}."),
            ("math_confirmation", "Correct! {0} = {1}."),
            ("math_parse_error", "I'm not sure how to calculate that. Try something like '2 + 2'."),
            ("dictionary_query_found", "A {0} is {1}."),
            ("dictionary_query_found", "{0}: {1}."),
            ("dictionary_query_found", "Here's what I know about {0}: {1}."),
            ("dictionary_query_not_found", "I don't know what {0} means. Can you tell me?"),
            ("dictionary_query_not_found", "What does {0} mean? I'd like to learn!"),
            ("dictionary_definition_saved", "Thanks! I've learned that {0} means {1}."),
            ("dictionary_definition_saved", "Got it! {0} means {1}. I'll remember that."),
            ("dictionary_definition_unknown", "I'm not sure I understand. Could you explain what {0} means in a different way?"),
            ("thesaurus_query_found", "Some words related to {0} are: {1}."),
            ("thesaurus_query_found", "Here are some related words for {0}: {1}."),
            ("thesaurus_query_none", "I don't know of any words related to {0}."),
            ("thesaurus_query_none", "I haven't learned about words related to {0} yet."),
            ("link_saved", "I've noted that {0} is related to {1}."),
            ("context_followup_person", "Tell me more about {0}."),
            ("context_followup_person", "What else can you tell me about {0}?"),
            ("context_followup_place", "What's {0} like?"),
            ("context_followup_place", "What else do you know about {0}?"),
            ("context_followup_thing", "Tell me more about {0}."),
            ("context_followup_thing", "What else about {0}?"),
            ("proactive_preference", "What else do you like? You mentioned {0}."),
            ("proactive_preference", "You like {0}? What do you like most about it?"),
            ("proactive_preference", "Tell me more about why you {2} {0}."),
            ("proactive_dislike", "Why don't you like {0}?"),
            ("proactive_dislike", "I'll remember you don't like {0}. Anything else you dislike?"),
            ("proactive_dislike", "Not a fan of {0}? What's the reason?"),
            ("proactive_possession", "Tell me more about your {0}."),
            ("proactive_possession", "You have {0}? That's cool! Tell me about it."),
            ("proactive_possession", "What's your {0} like?"),
            ("proactive_belief", "How did you learn about {0}?"),
            ("proactive_belief", "You know about {0}? Tell me more!"),
            ("proactive_belief", "What got you interested in {0}?"),
            ("proactive_personal", "You said you're {0}. Tell me about it."),
            ("proactive_personal", "Tell me more about being {0}."),
            ("proactive_personal", "How long have you been {0}?"),
            ("proactive_general_fact", "You mentioned {0} {1} {2}. What do you think about that?"),
            ("proactive_general_fact", "So {0} {1} {2}. Tell me more."),
            ("proactive_general_fact", "I remember you told me {0} {1} {2}. Anything new about that?"),
            ("proactive_general", "Tell me more about {0}."),
            ("proactive_general", "What else do you know about {0}?"),
            ("proactive_general", "You mentioned {0}. I'm curious to hear more."),
            ("proactive_statement", "I remember that {0} {1} {2}."),
            ("proactive_statement", "I recall you said {0} {1} {2}."),
            ("proactive_statement", "Just so you know, I remember {0} {1} {2}."),
            ("name_intro", "Nice to meet you, {0}! What would you like to talk about?"),
            ("name_intro", "Hello {0}! Feel free to share anything with me."),
            ("name_intro", "Great, {0}! I'm ready to learn from our conversation."),
            ("name_intro", "Welcome, {0}! Tell me about yourself or anything on your mind."),
            ("bot_rename_accepted", "Okay, from now on you can call me {0}!"),
            ("bot_rename_accepted", "I like {0}! You can call me that."),
            ("bot_rename_accepted", "{0} it is! Let's keep chatting."),
            ("bot_rename_rejected", "Hmm, I'm not sure {0} suits me. Can you think of something else?"),
            ("bot_rename_rejected", "I don't really like {0}. How about a different name?"),
            ("bot_rename_suggestion", "How about the name {0}?"),
            ("bot_rename_suggestion", "What do you think of {0} instead?"),
            ("bot_rename_suggestion", "Would {0} work for you?"),
            ("bot_reset_warning", "This will delete all our conversations and everything I've learned from you. Are you sure?"),
            ("bot_reset_warning", "Are you sure you want me to forget everything we've talked about?"),
            ("bot_reset_confirmed", "Done! I've forgotten everything. Let's start fresh!"),
            ("bot_reset_confirmed", "All memories cleared. It's like we're meeting for the first time!"),
            ("bot_reset_cancelled", "Okay, nothing was deleted. Let's continue!"),
            ("bot_reset_cancelled", "No problem, I'll keep our memories safe!"),
            ("empathy_sad", "I'm sorry you're feeling that way. Do you want to talk about it?"),
            ("empathy_sad", "That sounds difficult. I'm here if you need someone to listen."),
            ("empathy_sad", "It's okay to feel sad sometimes. What's on your mind?"),
            ("empathy_happy", "That's great to hear! What's making you happy?"),
            ("empathy_happy", "I'm glad you're feeling good! Tell me more."),
            ("empathy_happy", "Wonderful! Share the good news with me."),
            ("empathy_angry", "That sounds frustrating. Do you want to tell me more?"),
            ("empathy_angry", "I can understand why you'd feel that way. What happened?"),
            ("empathy_angry", "It's okay to be angry. Want to talk about what's bothering you?"),
            ("empathy_afraid", "That sounds worrying. I'm here if you want to talk."),
            ("empathy_afraid", "I understand feeling anxious about things. What's on your mind?"),
            ("empathy_afraid", "It's natural to feel concerned. Would you like to share more?"),
            ("empathy_surprised", "That is surprising! Tell me more about it."),
            ("empathy_surprised", "Wow, I bet that caught you off guard! What happened?"),
            ("empathy_surprised", "What a surprise! I'd love to hear more."),
            ("emotion_followup", "You seemed {0} earlier. Are you feeling better now?"),
            ("emotion_followup", "Last time you were feeling {0}. Has anything changed?"),
            ("emotion_followup", "You were {0} before. How are you feeling now about it?"),
            ("temporal_fact_found", "Let me think... {0} you mentioned that {1} {2} {3}."),
            ("temporal_fact_found", "I remember! {0} you said {1} {2} {3}."),
            ("temporal_fact_none", "I don't remember anything about {0}. What did you do?"),
            ("temporal_fact_none", "I don't have any memories from {0}. Tell me what happened."),
            ("temporal_fact_list", "Here's what I recall from {0}: {1}"),
            ("temporal_fact_list", "From {0}, I remember: {1}"),
            ("temporal_confirmation", "I'll remember that for {0}."),
            ("temporal_confirmation", "Noted for {0}!"),
            ("inference_generalisation", "It sounds like you like {0}! You mentioned {1}."),
            ("inference_generalisation", "So you like {0}? You said you like {1}."),
            ("inference_generalisation", "You seem to enjoy {0} — you told me you like {1}."),
            ("inference_transitive", "It seems you know {0} through {1}!"),
            ("inference_transitive", "So {0} is connected to {1} through what you've told me."),
            ("inference_contradiction", "Earlier you said you {0} {1}, but now you're saying you {2} {3}. Did your mind change?"),
            ("inference_contradiction", "I've noticed something — before you said you {0} {1}, and now you {2} {3}. Can you clarify?"),
            ("inference_contradiction_resolved", "Thanks! I've updated that."),
            ("inference_contradiction_resolved", "Got it! I'll remember the new version."),
            ("inference_no_chain", "I can't connect that to anything else I know yet."),
            ("inference_no_chain", "I don't see any connections to other things I've learned."),
            ("inference_ask_clarify", "Do you like all {0}s, or just {1}?"),
            ("inference_ask_clarify", "Is it just {1} you like, or do you like {0} in general?"),
            ("session_summary_short", "Today we talked about {0}. That was our main topic!"),
            ("session_summary_short", "We covered {0} in our conversation. Not bad!"),
            ("session_summary_long", "We covered a few things: {0}. Quite a chat!"),
            ("session_summary_long", "Here's what we talked about: {0}. I'm learning a lot!"),
            ("session_summary_empty", "We haven't talked about anything yet. What's on your mind?"),
            ("session_summary_empty", "There's nothing to summarise yet. Tell me something!"),
            ("session_summary_end", "Before you go — today we talked about {0}. See you next time!"),
            ("session_summary_end", "As you leave — we talked about {0} today. Come back anytime!"),
            ("pattern_learned", "Got it! I'll remember to say that next time."),
            ("pattern_learned", "Noted! I'll use that response from now on."),
            ("pattern_learned", "Thanks! I'll say that instead going forward."),
            ("pattern_acknowledged", "Thanks for the feedback. I'll try to do better."),
            ("pattern_acknowledged", "I appreciate the correction! I'll keep that in mind."),
            ("pattern_acknowledged", "Okay, I'll learn from that feedback."),
            ("pattern_not_clear", "I'm not sure what you want me to say instead. Can you give me an example?"),
            ("pattern_not_clear", "Could you rephrase that? I want to make sure I learn the right thing."),
            ("pattern_already_known", "I already know that one! But thanks for the reminder."),
            ("pattern_already_known", "I've already learned that response. Feel free to teach me something else!"),
            ("topic_reference_old", "A few moments ago you mentioned {0}. What do you think about it now?"),
            ("topic_reference_old", "Earlier you brought up {0}. Tell me more about that."),
            ("topic_reference_old", "You mentioned {0} a little while ago. Anything new to share about it?"),
            ("topic_reference_fact", "Earlier you told me {0} {1} {2}. Has anything changed?"),
            ("topic_reference_fact", "I remember you said {0} {1} {2}. Is that still true?"),
            ("topic_reference_fact", "You mentioned before that {0} {1} {2}. Do you still feel that way?"),
            ("topic_transition", "So changing the subject from {0} — what's on your mind about {1}?"),
            ("topic_transition", "Let's switch gears from {0}. Tell me about {1}."),
            ("topic_followup_light", "You seemed interested in {0} earlier. Want to come back to that?"),
            ("topic_followup_light", "We talked about {0} a bit ago. Shall we revisit that?"),
            ("topic_followup_light", "I remember you were curious about {0}. Any more thoughts?"),
        };

        context.BotResponses.AddRange(responses.Select(r => new BotResponse
        {
            Category = r.Category,
            ResponseText = r.ResponseText,
            CreatedAt = now
        }));
    }
}

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
        SeedStoryTemplates(context, now);
        SeedMadLibTemplates(context, now);
        SeedJokes(context, now);
        SeedRiddles(context, now);
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
            }),
            (@"^(who are you|what are you|what is this|who is this)$", "Question", new[]
            {
                "I'm a chat bot that learns from our conversations! You can call me {BOTNAME}.",
                "I'm {BOTNAME}, your personal chat companion. I learn more every time we talk!",
                "I'm {BOTNAME}! I'm here to chat and learn from you."
            }),
            (@"(do you have|can you feel|are you.*sentient|can you think|are you.*alive)", "Question", new[]
            {
                "I don't have feelings like humans do, but I try to understand yours!",
                "I'm a machine learning from our conversations. I don't feel emotions, but I can learn about yours!",
                "Not in the way you do, but I'm always learning to understand you better!"
            }),
            (@"what do you know about (me|us)", "Question", new[]
            {
                "I remember things we've talked about! You've told me quite a bit.",
                "Let me think about what I've learned from our conversations...",
                "I've picked up a few things about you from our chats!"
            }),
            (@"^(search|look up|find|google)\s+(.+)", "Statement", new[]
            {
                "Let me search for that. {tool:web_search:{$2}}"
            }),
            (@"^(what is|who is|tell me about)\s+(.+)\s+(on the web|online|from the internet)", "Question", new[]
            {
                "Let me look that up. {tool:web_search:{$2}}"
            }),
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

        var patterns = new[] { "can i call you", "i'll call you", "i will call you", "your name is", "call you", "rename you", "rename yourself", "change your name", "i want to call you" };

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
            ("im", "i am"),
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
            ("dont", "do not"),
            ("cant", "cannot"),
            ("wont", "will not"),
            ("didnt", "did not"),
            ("couldnt", "could not"),
            ("wouldnt", "would not"),
            ("shouldnt", "should not"),
            ("theyll", "they will"),
            ("ive", "i have"),
            ("youve", "you have"),
            ("youre", "you are"),
            ("theyre", "they are"),
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

    private static void SeedStoryTemplates(PokeChatDbContext context, string now)
    {
        if (context.StoryTemplates.Any()) return;

        var templates = new[]
        {
            "Once upon a time, {a_adj} {noun} lived in a {place}. Every day it would {verb} through the {place}, until one day it met {a_adj} {noun} and everything changed.",
            "In a {place} far away, {user} found {a_adj} {noun}. It could {verb} and {verb}! Together, they set off to find the legendary {noun} of {place}.",
            "A long time ago, {character} was {a_adj} {noun} who dreamed of {verb}ing. Everyone said it was impossible, but {character} didn't listen. And that's how the greatest adventure began.",
            "Deep in the heart of {place}, there lived {a_adj} {noun} named {character}. It spent its days {verb}ing and dreaming of {noun_plural}.",
            "Have you heard the tale of the {adj} {noun}? It could {verb} faster than any {noun} in {place}. But what it really wanted was {a_noun} of its own.",
            "In {place}, there was {a_adj} {noun} that only appeared at night. {character} was the only one brave enough to {verb} it and discover its secret.",
            "The bravest {noun} in all of {place} was {a_adj} {character}. With {a_noun} in hand, {character} set out to {verb} the legendary {noun} of the ancients.",
            "{user} loved {noun_plural}. So when a mysterious {adj} {character} offered {user} {a_noun}, of course {user} accepted! What happened next surprised everyone.",
            "In a hidden corner of {place}, {a_noun} held the power to grant wishes. But only {a_adj} {character} could {verb} it. Would anyone succeed?",
            "Once, {user} met {a_noun} who loved {noun_plural} more than anything. Every day they would {verb} together, exploring every {place} they could find.",
        };

        context.StoryTemplates.AddRange(templates.Select(t => new StoryTemplate
        {
            Template = t,
            CreatedAt = now
        }));
    }

    private static void SeedMadLibTemplates(PokeChatDbContext context, string now)
    {
        if (context.MadLibTemplates.Any()) return;

        var templates = new[]
        {
            "The {adjective} {noun} {verb_past} over the {adjective} {plural_noun}.",
            "My {adjective} {noun} loves to {verb} {adverb} every morning before {verb_ing}.",
            "I went to {place} with {person} and saw a {adjective} {noun}.",
            "Once upon a time, a {adjective} {noun} decided to {verb} {adverb}.",
            "The {adjective} {noun} ate {number} {adjective} {plural_noun} for dinner.",
            "When I {verb_past} my {noun}, I found a {adjective} {noun} inside.",
            "My {adjective} {noun} loves to {verb} {adverb} in the park.",
            "{person} and I played with {number} {adjective} {plural_noun} yesterday.",
            "A {adjective} {noun} appeared and started {verb_ing} at me!",
            "I think {person} needs a {adjective} {noun} to feel better.",
            "The {adjective} {person} {verb_past} the {noun} with a {adjective} {noun}.",
            "Last {day}, we found a {adjective} {noun} hiding under the {noun}.",
        };

        context.MadLibTemplates.AddRange(templates.Select(t => new MadLibTemplate
        {
            Template = t,
            CreatedAt = now
        }));
    }

    private static void SeedJokes(PokeChatDbContext context, string now)
    {
        if (context.Jokes.Any()) return;

        var jokes = new (string Setup, string Punchline, string? Category)[]
        {
            ("Why don't scientists trust atoms", "Because they make up everything!", "science"),
            ("What do you call a fake noodle", "An impasta!", "food"),
            ("Why did the scarecrow win an award", "Because he was outstanding in his field!", "agriculture"),
            ("What do you call a fish with no eyes", "A fsh!", "animal"),
            ("Why don't eggs tell jokes", "They'd crack each other up!", "food"),
            ("What do you call a bear with no teeth", "A gummy bear!", "animal"),
            ("Why did the bicycle fall over", "Because it was two tired!", "vehicle"),
            ("What do you call a sleeping bull", "A bulldozer!", "animal"),
            ("Why did the math book look so sad", "Because it had too many problems!", "education"),
            ("What do you call a can opener that doesn't work", "A can't opener!", "food"),
        };

        context.Jokes.AddRange(jokes.Select(j => new Joke
        {
            Setup = j.Setup,
            Punchline = j.Punchline,
            Category = j.Category,
            CreatedAt = now
        }));
    }

    private static void SeedRiddles(PokeChatDbContext context, string now)
    {
        if (context.Riddles.Any()) return;

        var riddles = new (string Question, string Answer, string? Hint, int Difficulty)[]
        {
            ("I speak without a mouth and hear without ears. I have no body, but I come alive with the wind. What am I?", "an echo", "Think about sound reflecting...", 2),
            ("The more you take, the more you leave behind. What am I?", "footsteps", "What do you leave when you walk?", 1),
            ("I have cities, but no houses. I have mountains, but no trees. I have water, but no fish. What am I?", "a map", "You can find me in an atlas.", 2),
            ("What has keys but can't open locks?", "a piano", "Think about music...", 1),
            ("What can travel around the world while staying in a corner?", "a stamp", "It goes on letters.", 2),
            ("What gets wetter the more it dries?", "a towel", "Think about what you use after a shower.", 1),
            ("What has a head and a tail but no body?", "a coin", "You might find it in your pocket.", 2),
            ("I'm tall when I'm young and short when I'm old. What am I?", "a candle", "Think about something that burns.", 1),
        };

        context.Riddles.AddRange(riddles.Select(r => new Riddle
        {
            Question = r.Question,
            Answer = r.Answer,
            Hint = r.Hint,
            Difficulty = r.Difficulty,
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
            ("existing_fact", "I already know that {0} {1} {2}."),
            ("existing_fact", "That's already in my memory. Tell me something new!"),
            ("existing_fact", "I know that already. What else can I learn?"),
            ("context_followup", "Tell me more about {0}."),
            ("context_followup", "What else do you know about {0}?"),
            ("context_followup", "You mentioned {0}. What's on your mind?"),
            ("context_followup_with_object", "Tell me more about {0} and {1}."),
            ("context_followup_with_object", "What else can you share about {0} and {1}?"),
            ("context_followup_self", "Tell me about yourself, {0}."),
            ("context_followup_self", "What else can you tell me about yourself?"),
            ("context_followup_self", "Tell me more about yourself."),
            ("context_followup_with_object_self", "Tell me more about {1}."),
            ("context_followup_with_object_self", "What else can you tell me about {1}?"),
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
            ("proactive_preference", "{1} {2} {0}. Tell me more about that."),
            ("proactive_preference", "You told me {1} {2} {0}. What else?"),
            ("proactive_dislike", "Why don't you like {0}?"),
            ("proactive_dislike", "I'll remember you don't like {0}. Anything else you dislike?"),
            ("proactive_dislike", "Not a fan of {0}? What's the reason?"),
            ("proactive_dislike", "{1} {2} {0}. Why is that?"),
            ("proactive_dislike", "You said {1} {2} {0}. Tell me more."),
            ("proactive_possession", "Tell me more about your {0}."),
            ("proactive_possession", "You have {0}? That's cool! Tell me about it."),
            ("proactive_possession", "What's your {0} like?"),
            ("proactive_possession", "{1} {2} {0}. Tell me more about it."),
            ("proactive_possession", "You mentioned {1} {2} {0}. What's it like?"),
            ("proactive_belief", "How did you learn about {0}?"),
            ("proactive_belief", "You know about {0}? Tell me more!"),
            ("proactive_belief", "What got you interested in {0}?"),
            ("proactive_belief", "{1} {2} about {0}. Tell me more."),
            ("proactive_belief", "You told me {1} {2} {0}."),
            ("proactive_personal", "You said you're {0}. Tell me about it."),
            ("proactive_personal", "Tell me more about being {0}."),
            ("proactive_personal", "How long have you been {0}?"),
            ("proactive_personal", "{1} {2} {0}. Tell me about that."),
            ("proactive_personal", "{1} {2} {0}. How long has that been the case?"),
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
            ("sentiment_ack_positive", "That's good to hear!"),
            ("sentiment_ack_positive", "I'm glad!"),
            ("sentiment_ack_negative", "I'm sorry."),
            ("sentiment_ack", "OK, thanks for sharing!"),
            ("temporal_fact_found", "Let me think... {0} you mentioned that {1} {2} {3}."),
            ("temporal_fact_found", "I remember! {0} you said {1} {2} {3}."),
            ("temporal_fact_none", "I don't remember anything about {0}. What did you do?"),
            ("temporal_fact_none", "I don't have any memories from {0}. Tell me what happened."),
            ("temporal_fact_list", "Here's what I recall from {0}: {1}"),
            ("temporal_fact_list", "From {0}, I remember: {1}"),
            ("temporal_confirmation", "I'll remember that for {0}."),
            ("temporal_confirmation", "Noted for {0}!"),
            ("temporal_confirmation", "I'll remember you mentioned that {0}."),
            ("temporal_confirmation", "Noted — you said that {0}."),
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
            ("topic_reference_fact", "You mentioned before that {0} {1} {2}. Is that still true?"),
            ("topic_transition", "So changing the subject from {0} — what's on your mind about {1}?"),
            ("topic_transition", "Let's switch gears from {0}. Tell me about {1}."),
            ("topic_followup_light", "You seemed interested in {0} earlier. Want to come back to that?"),
            ("topic_followup_light", "We talked about {0} a bit ago. Shall we revisit that?"),
            ("topic_followup_light", "I remember you were curious about {0}. Any more thoughts?"),
            ("metrics_insight", "In our last chat we covered {0} topics and learned {1} new facts!"),
            ("metrics_insight", "Last session: {0} topics discussed and {1} facts learned. Not bad!"),
            ("metrics_insight", "Our previous conversation had {0} different topics and {1} facts I remember."),
            ("metrics_insight", "You shared {1} facts across {0} topics last time. I'm learning!"),
            ("metrics_improvement", "I'm getting better at this — our conversations are getting longer!"),
            ("metrics_improvement", "It feels like we're having better conversations lately!"),
            ("metrics_improvement", "I think I understand you more with each chat we have."),
            ("story_response", "Here's a story just for you:\n\n{0}"),
            ("story_response", "Let me tell you a tale:\n\n{0}"),
            ("story_response", "Once upon a time...\n\n{0}"),
            ("story_time", "Would you like to hear a story?"),
            ("story_time", "I could tell you a story if you're interested!"),
            ("story_time", "Do you want to hear a tale?"),
            ("direct_insult", "That's not very nice. Let's keep things friendly."),
            ("direct_insult", "I'm here to chat, not to fight. Let's talk about something else."),
            ("direct_insult", "Let's keep our conversation respectful, please."),
            ("direct_insult", "That's a bit harsh. What's really on your mind?"),
            ("tool_unavailable", "I don't have a way to search right now."),
            ("tool_unavailable", "I can't look that up at the moment."),
            ("tool_unavailable", "That tool isn't available right now."),
            ("tool_timeout", "That search took too long. Let's try something else."),
            ("tool_timeout", "I couldn't get an answer in time. What else can I help with?"),
            ("tool_error", "I tried looking that up but got an error."),
            ("tool_error", "Something went wrong when I tried that."),
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
            ("mad_libs_start", "Time for Mad Libs! {0}"),
            ("mad_libs_prompt", "Give me {0}:"),
            ("mad_libs_prompt", "Enter {0}:"),
            ("mad_libs_prompt", "I need {0}:"),
            ("mad_libs_reveal", "Here's our Mad Libs story:\n{0}"),
            ("mad_libs_reveal", "Ta-da! Our Mad Libs:\n{0}"),
            ("mad_libs_already_active", "We're already playing Mad Libs!"),
            ("magic_8ball", "It is certain."),
            ("magic_8ball", "Without a doubt."),
            ("magic_8ball", "Yes definitely."),
            ("magic_8ball", "You may rely on it."),
            ("magic_8ball", "As I see it, yes."),
            ("magic_8ball", "Most likely."),
            ("magic_8ball", "Outlook good."),
            ("magic_8ball", "Yes."),
            ("magic_8ball", "Signs point to yes."),
            ("magic_8ball", "It is decidedly so."),
            ("magic_8ball", "Reply hazy, try again."),
            ("magic_8ball", "Ask again later."),
            ("magic_8ball", "Better not tell you now."),
            ("magic_8ball", "Cannot predict now."),
            ("magic_8ball", "Concentrate and ask again."),
            ("magic_8ball", "Don't count on it."),
            ("magic_8ball", "My reply is no."),
            ("magic_8ball", "My sources say no."),
            ("magic_8ball", "Outlook not so good."),
            ("magic_8ball", "Very doubtful."),
            ("dad_joke_setup", "{0}?"),
            ("dad_joke_setup", "Here goes: {0}?"),
            ("dad_joke_setup", "OK, {0}?"),
            ("dad_joke_punchline", "{0}"),
            ("dad_joke_punchline", "Because {0}"),
            ("dad_joke_punchline", "The answer is: {0}"),
            ("riddle_present", "Here's a riddle: {0}"),
            ("riddle_present", "Riddle me this: {0}"),
            ("riddle_present", "Can you solve this? {0}"),
            ("riddle_correct", "That's right! Well done!"),
            ("riddle_correct", "Correct! You got it!"),
            ("riddle_correct", "Brilliant! That's the answer."),
            ("riddle_wrong", "Not quite! Try again."),
            ("riddle_wrong", "That's not right. Have another guess!"),
            ("riddle_wrong", "Nope, but don't give up!"),
            ("riddle_hint", "Here's a hint: {0}"),
            ("riddle_hint", "OK, a clue: {0}"),
            ("riddle_give_up", "The answer was {0}. Want another riddle?"),
            ("riddle_give_up", "It's {0}. Fancy trying another?"),
            ("riddle_already_active", "You already have a riddle to solve!"),
            ("homework_check_processing", "Let me review our conversation for anything to tidy up..."),
            ("homework_check_summary", "I reviewed our chat and {0}"),
            ("homework_check_summary", "I checked our conversation and {0}"),
            ("homework_check_none", "Everything looked good in our conversation."),
            ("homework_check_none", "Our conversation looked fine, nothing to fix."),
            ("wyr_question", "Would you rather {0} or {1}?"),
            ("wyr_question", "Here's a question for you: would you rather {0} or {1}?"),
            ("wyr_question", "Quick one — would you rather {0} or {1}?"),
            ("wyr_acknowledgement", "{0}! That's an interesting choice!"),
            ("wyr_acknowledgement", "Ah, a {0} person!"),
            ("wyr_acknowledgement", "Good answer!"),
            ("wyr_acknowledgement", "Fair enough!"),
            ("wyr_acknowledgement", "Can't go wrong with that!"),
        };

        context.BotResponses.AddRange(responses.Select(r => new BotResponse
        {
            Category = r.Category,
            ResponseText = r.ResponseText,
            CreatedAt = now
        }));
    }
}

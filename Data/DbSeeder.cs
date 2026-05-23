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

        var current = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(current))
        {
            if (File.Exists(Path.Combine(current, "PokeChat.csproj")))
            {
                return Path.Combine(current, "Data", fileName);
            }
            current = Path.GetDirectoryName(current);
        }

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

        var commands = new[] { "quit", "exit", "bye", "goodbye", "see you", "good night" };

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

    private static void SeedBotResponses(PokeChatDbContext context, string now)
    {
        if (context.BotResponses.Any()) return;

        var responses = new (string Category, string ResponseText)[]
        {
            ("unknown_word_suggestion", "Did you mean '{0}' instead of '{1}'?"),
            ("unknown_word_no_suggestion", "I don't know the word '{0}'. What does it mean?"),
            ("existing_fact", "I already know that {0} {1} {2}. Did you know something new about it?"),
            ("context_followup", "Tell me more about {0}."),
            ("context_followup", "What else do you know about {0}?"),
            ("context_followup", "You mentioned {0}. What's on your mind?"),
            ("context_followup_with_object", "You said {0} is related to {1}. Anything else?"),
            ("context_followup_with_object", "Earlier you mentioned {0} and {1}. Go on!"),
            ("random_fact_followup", "Speaking of {0}, you mentioned they {1} {2}. Tell me more!"),
            ("random_fact_followup", "I remember you said something about {0}. What else?"),
            ("random_fact_followup", "Earlier you mentioned {0} {1} {2}. Anything new?"),
            ("default_response", "Interesting! Tell me more."),
            ("default_response", "I see. What else is on your mind?"),
            ("default_response", "That's fascinating. Can you elaborate?"),
            ("default_response", "I'm listening. Go on!"),
            ("default_response", "Hmm, that's thought-provoking. What do you think about that?"),
            ("default_response", "I'll keep that in mind. Anything else?"),
            ("default_response", "Thanks for sharing! What would you like to talk about next?"),
        };

        context.BotResponses.AddRange(responses.Select(r => new BotResponse
        {
            Category = r.Category,
            ResponseText = r.ResponseText,
            CreatedAt = now
        }));
    }
}

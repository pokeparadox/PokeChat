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
        SeedCodingResponseRules(context, now);
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
        SeedRhymeGroups(context, now);
        SeedPoemTemplates(context, now);
        SeedBotResponses(context, now);
        SeedHangmanBotResponses(context, now);
        SeedErrorKnowledgeEntries(context, now);

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
            (@"^(run|execute|shell)\s+(command|cmd)\s+(.+)", "Statement", new[]
            {
                "Running command: {$3}. {tool:shell_command:{$3}}"
            }),
            (@"^run\s+(.+)", "Statement", new[]
            {
                "Running that now. {tool:shell_command:{$1}}"
            }),
            (@"^(read|show|open)\s+(file\s+)?(.+)", "Statement", new[]
            {
                "Let me read that file. {tool:file_ops:read:{$3}}"
            }),
            (@"^(write|create|save)\s+(file\s+)?(.+)\s*$", "Statement", new[]
            {
                "I'll create that file. {tool:file_ops:write:{$3}}"
            }),
            (@"^(list|show|ls)\s+(files|directory|dir|folder)\s+(.+)", "Statement", new[]
            {
                "Listing directory contents. {tool:file_ops:list:{$3}}"
            }),
            (@"^(search|find|grep)\s+(in\s+)?(.+?)\s+(for\s+)?(.+)", "Statement", new[]
            {
                "Searching for '{4}' in {3}. {tool:file_ops:search:{$3}:{$4}}"
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

    private static void SeedCodingResponseRules(PokeChatDbContext context, string now)
    {
        if (context.ResponseRules.Any(r => r.Persona == "coding")) return;

        var rules = new (string Pattern, string InputType, string Response)[]
        {
            // Build
            (@"^(build|compile|make)\s+(the\s+)?project", "Statement", "Building the project. {tool:shell_command:dotnet:build}"),
            (@"^(build|compile|make)\s+(the\s+)?solution", "Statement", "Building the solution. {tool:shell_command:dotnet:build}"),
            (@"^rebuild\b", "Statement", "Rebuilding. {tool:shell_command:dotnet:clean} && dotnet build"),
            (@"^build\s+release", "Statement", "Building release. {tool:shell_command:dotnet:build:-c:Release}"),
            (@"^build\s+debug", "Statement", "Building debug. {tool:shell_command:dotnet:build:-c:Debug}"),
            (@"^restore\s+(packages|dependencies|nuget)", "Statement", "Restoring packages. {tool:shell_command:dotnet:restore}"),
            (@"^clean\s+(the\s+)?project", "Statement", "Cleaning project. {tool:shell_command:dotnet:clean}"),
            (@"^publish\s+(the\s+)?project", "Statement", "Publishing. {tool:shell_command:dotnet:publish}"),

            // Test
            (@"^(run\s+)?(the\s+)?tests?\b", "Statement", "Running tests. {tool:shell_command:dotnet:test}"),
            (@"^test\s+(a\s+)?specific\s+(test|file)\s+(.+)", "Statement", "Running specific test. {tool:shell_command:dotnet:test:--filter:{$3}}"),
            (@"^(run\s+)?all\s+(the\s+)?tests", "Statement", "Running all tests. {tool:shell_command:dotnet:test}"),
            (@"^(run\s+)?(unit|integration)\s+tests", "Statement", "Running tests. {tool:shell_command:dotnet:test}"),
            (@"^test\s+(category|tag)\s+(.+)", "Statement", "Running tests by category. {tool:shell_command:dotnet:test:--filter:Category={$2}}"),
            (@"^(coverage|code\s+coverage)\b", "Statement", "Running tests with coverage. {tool:shell_command:dotnet:test:--collect:XPlat Code Coverage}"),

            // Git
            (@"^(git\s+)?status\b", "Statement", "Checking status. {tool:shell_command:git:status}"),
            (@"^(what|show)\s+(is\s+)?(the\s+)?(current\s+)?branch", "Statement", "Checking branch. {tool:shell_command:git:branch:--show-current}"),
            (@"^(git\s+)?log\b", "Statement", "Showing log. {tool:shell_command:git:log:--oneline:-10}"),
            (@"^(git\s+)?diff\b(?!.*--cached)", "Statement", "Showing diff. {tool:shell_command:git:diff}"),
            (@"^(git\s+)?(staged|staging|index)\s+diff", "Statement", "Showing staged diff. {tool:shell_command:git:diff:--cached}"),
            (@"^(git\s+)?commit\s+(.+)$", "Statement", "Committing. {tool:shell_command:git:add:-A && git commit -m \"{$2}\"}"),
            (@"^(git\s+)?push\b", "Statement", "Pushing. {tool:shell_command:git:push}"),
            (@"^(git\s+)?pull\b", "Statement", "Pulling. {tool:shell_command:git:pull}"),
            (@"^(git\s+)?(fetch|sync)\b", "Statement", "Fetching. {tool:shell_command:git:fetch}"),
            (@"^(git\s+)?merge\s+(.+)", "Statement", "Merging. {tool:shell_command:git:merge:{$2}}"),
            (@"^(git\s+)?checkout\s+(.+)", "Statement", "Checking out. {tool:shell_command:git:checkout:{$2}}"),
            (@"^(git\s+)?(create|new)\s+branch\s+(.+)", "Statement", "Creating branch. {tool:shell_command:git:checkout:-b {$3}}"),
            (@"^(git\s+)?(delete|remove)\s+branch\s+(.+)", "Statement", "Deleting branch. {tool:shell_command:git:branch:-d {$3}}"),
            (@"^(git\s+)?stash\b(?!.*pop)", "Statement", "Stashing. {tool:shell_command:git:stash}"),
            (@"^(git\s+)?(stash\s+pop|unstash)", "Statement", "Popping stash. {tool:shell_command:git:stash:pop}"),
            (@"^(git\s+)?add\s+(all|\.|everything)", "Statement", "Adding all. {tool:shell_command:git:add:-A}"),
            (@"^(git\s+)?add\s+(.+)", "Statement", "Adding files. {tool:shell_command:git:add:{$2}}"),
            (@"^(git\s+)?reset\b(?!.*--hard)", "Statement", "Resetting. {tool:shell_command:git:reset}"),
            (@"^(git\s+)?(show|display)\s+(.+)", "Statement", "Showing. {tool:shell_command:git:show:{$3}}"),
            (@"^(git\s+)?(tag|tags)\b", "Statement", "Listing tags. {tool:shell_command:git:tag}"),
            (@"^(git\s+)?remote\b", "Statement", "Showing remotes. {tool:shell_command:git:remote:-v}"),
            (@"^(git\s+)?reflog\b", "Statement", "Showing reflog. {tool:shell_command:git:reflog}"),
            (@"^(git\s+)?blame\s+(.+)", "Statement", "Showing blame. {tool:shell_command:git:blame:{$2}}"),

            // Run
            (@"^run\s+(the\s+)?project\b", "Statement", "Running project. {tool:shell_command:dotnet:run}"),
            (@"^start\s+(.+)", "Statement", "Starting. {tool:shell_command:{$1}}"),
            (@"^execute\s+(.+)", "Statement", "Executing. {tool:shell_command:{$1}}"),

            // Package
            (@"^add\s+(nuget\s+)?package\s+(.+)", "Statement", "Adding package. {tool:shell_command:dotnet:add:package:{$2}}"),
            (@"^remove\s+(nuget\s+)?package\s+(.+)", "Statement", "Removing package. {tool:shell_command:dotnet:remove:package:{$2}}"),
            (@"^(list|show)\s+(nuget\s+)?packages", "Statement", "Listing packages. {tool:shell_command:dotnet:list:package}"),
            (@"^(update|upgrade)\s+(nuget\s+)?package\s+(.+)", "Statement", "Updating package. {tool:shell_command:dotnet:add:package:{$3}}"),
            (@"^(search|find)\s+nuget\s+(.+)", "Statement", "Searching NuGet. {tool:shell_command:dotnet:nuget:search:{$2}}"),
            (@"^(list|show)\s+outdated\s+packages", "Statement", "Checking outdated packages. {tool:shell_command:dotnet:list:package:--outdated}"),
            (@"^add\s+(npm|node)\s+package\s+(.+)", "Statement", "Adding npm package. {tool:shell_command:npm:install:{$3}}"),
            (@"^remove\s+(npm|node)\s+package\s+(.+)", "Statement", "Removing npm package. {tool:shell_command:npm:uninstall:{$3}}"),

            // Lint/format
            (@"^(format|fmt|beautify)\s+(code|files|project)", "Statement", "Formatting code. {tool:shell_command:dotnet:format}"),
            (@"^(analyze|analyse)\b", "Statement", "Analyzing. {tool:shell_command:dotnet:analyze}"),
            (@"^lint\b", "Statement", "Linting. {tool:shell_command:dotnet:format:--verify-no-changes}"),
            (@"^(check|verify)\s+types\b", "Statement", "Checking types. {tool:shell_command:npx:tsc:--noEmit}"),

            // DB/Migrations
            (@"^(add|create)\s+migration\s+(.+)", "Statement", "Adding migration. {tool:shell_command:dotnet:ef:migrations:add:{$2}}"),
            (@"^remove\s+(last\s+)?migration", "Statement", "Removing migration. {tool:shell_command:dotnet:ef:migrations:remove}"),
            (@"^(apply|run|execute)\s+migrations", "Statement", "Applying migrations. {tool:shell_command:dotnet:ef:database:update}"),
            (@"^(list|show)\s+migrations", "Statement", "Listing migrations. {tool:shell_command:dotnet:ef:migrations:list}"),
            (@"^(generate|create)\s+script\b", "Statement", "Generating script. {tool:shell_command:dotnet:ef:migrations:script}"),
            (@"^update\s+database", "Statement", "Updating database. {tool:shell_command:dotnet:ef:database:update}"),

            // Dotnet generic
            (@"^(check|verify)\s+(sdk|dotnet)\s+version", "Statement", "Checking version. {tool:shell_command:dotnet:--version}"),
            (@"^(list|show)\s+(sdk|dotnet)\s+(versions|sdks)", "Statement", "Listing SDKs. {tool:shell_command:dotnet:--list-sdks}"),
            (@"^(list|show)\s+runtimes", "Statement", "Listing runtimes. {tool:shell_command:dotnet:--list-runtimes}"),
            (@"^(new|create)\s+(console|app)\s+(.+)", "Statement", "Creating project. {tool:shell_command:dotnet:new:console:-n:{$3}}"),
            (@"^(new|create)\s+(class\s+)?library\s+(.+)", "Statement", "Creating library. {tool:shell_command:dotnet:new:classlib:-n:{$3}}"),
            (@"^(new|create)\s+(xunit|test)\s+project\s+(.+)", "Statement", "Creating test project. {tool:shell_command:dotnet:new:xunit:-n:{$3}}"),
            (@"^(new|create)\s+(web|api)\s+(.+)", "Statement", "Creating web API. {tool:shell_command:dotnet:new:webapi:-n:{$3}}"),
            (@"^(new|create)\s+(sln|solution)\s+(.+)", "Statement", "Creating solution. {tool:shell_command:dotnet:new:slnx:-n:{$3}}"),
            (@"^add\s+project\s+reference\s+(.+)", "Statement", "Adding reference. {tool:shell_command:dotnet:add:reference:{$3}}"),
            (@"^(list|show)\s+references", "Statement", "Listing references. {tool:shell_command:dotnet:list:reference}"),

            // File ops
            (@"^(list|show)\s+(files|directory|dir)\s+(.+)", "Statement", "Listing directory. {tool:shell_command:ls:-la:{$3}}"),
            (@"^(list|show)\s+(files|directory|dir)", "Statement", "Listing directory. {tool:shell_command:ls:-la}"),
            (@"^(find|search)\s+(for\s+)?(.+?)\s+in\s+(.+)", "Statement", "Searching. {tool:shell_command:grep:-r:\"{$3}\":{$5}}"),
            (@"^(count|wc)\s+(lines|words)\s+(in\s+)?(.+)", "Statement", "Counting. {tool:shell_command:wc:{$4}}"),
            (@"^show\s+(file\s+)?(tree|structure)", "Statement", "Showing tree. {tool:shell_command:tree}"),
            (@"^(disk|storage|space)\b", "Statement", "Checking disk. {tool:shell_command:df:-h}"),
            (@"^(current|working)\s+(directory|path|folder)", "Statement", "Showing path. {tool:shell_command:pwd}"),
            (@"^(size|usage)\s+(of\s+)?(.+)", "Statement", "Checking size. {tool:shell_command:du:-sh:{$3}}"),

            // Docker
            (@"^docker\s+(ps|processes|containers)\b", "Statement", "Listing containers. {tool:shell_command:docker:ps}"),
            (@"^docker\s+all\s+containers", "Statement", "Listing all containers. {tool:shell_command:docker:ps:-a}"),
            (@"^docker\s+(images|image\s+list)", "Statement", "Listing images. {tool:shell_command:docker:images}"),
            (@"^docker\s+build\s+(.+)", "Statement", "Building image. {tool:shell_command:docker:build:-t:{$2}:.}"),
            (@"^docker\s+(compose\s+)?up\b", "Statement", "Starting compose. {tool:shell_command:docker:compose:up:-d}"),
            (@"^docker\s+(compose\s+)?down", "Statement", "Stopping compose. {tool:shell_command:docker:compose:down}"),
            (@"^docker\s+logs\s+(.+)", "Statement", "Showing logs. {tool:shell_command:docker:logs:{$2}}"),
            (@"^docker\s+(stop|kill)\s+(.+)", "Statement", "Stopping container. {tool:shell_command:docker:stop:{$2}}"),

            // Misc
            (@"^(kill|stop)\s+(port|process)\s+(\d+)", "Statement", "Killing process. {tool:shell_command:kill:$(lsof -ti:{$3})}"),
            (@"^zip\s+(.+)", "Statement", "Zipping. {tool:shell_command:zip:-r:{$2}.zip:{$2}}"),
            (@"^unzip\s+(.+)", "Statement", "Unzipping. {tool:shell_command:unzip:{$2}}"),
            (@"^(whoami|who\s+am\s+i)", "Statement", "Checking user. {tool:shell_command:whoami}"),
            (@"^(date|time|now|today)", "Statement", "Checking date. {tool:shell_command:date}"),
            (@"^uptime", "Statement", "Checking uptime. {tool:shell_command:uptime}"),
            (@"^(system|os)\s+(info|version)", "Statement", "Checking system info. {tool:shell_command:uname:-a}"),
            (@"^(memory|ram|mem)\b", "Statement", "Checking memory. {tool:shell_command:free:-h}"),
            (@"^(network|ip|address)\b", "Statement", "Checking network. {tool:shell_command:ip:addr}"),
        };

        foreach (var (pattern, inputType, response) in rules)
        {
            var rule = new ResponseRule
            {
                Pattern = pattern,
                InputType = inputType,
                IsActive = true,
                Persona = "coding",
                CreatedAt = now,
                Responses = new List<ResponseRuleResponse>
                {
                    new() { ResponseText = response }
                }
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

    private static void SeedHangmanBotResponses(PokeChatDbContext context, string now)
    {
        if (context.BotResponses.Any(r => r.Category == "hangman_welcome")) return;

        var responses = new (string Category, string ResponseText)[]
        {
            ("hangman_welcome", "Let's play Hangman! The word has {0} letters.\n{1}\nWrong guesses: {2}"),
            ("hangman_welcome", "Time for Hangman! I'm thinking of a word with {0} letters.\n{1}\nWrong: {2}"),
            ("hangman_correct", "Good guess! The letter '{0}' is in the word.\n{1}"),
            ("hangman_correct", "Nice! '{0}' is there.\n{1}"),
            ("hangman_wrong", "Sorry, '{0}' is not in the word. {1} wrong guesses left.\n{2}\nWrong letters: {3}"),
            ("hangman_wrong", "Nope, no '{0}'. {1} attempts remaining.\n{2}\nWrong: {3}"),
            ("hangman_win", "Congratulations! You got it! The word was '{0}'."),
            ("hangman_win", "You win! '{0}' was the word. Well done!"),
            ("hangman_lose", "Game over! The word was '{0}'. Better luck next time!"),
            ("hangman_lose", "Sorry, you ran out of guesses. The word was '{0}'."),
            ("hangman_play_again", "Want to play again? Say 'yes' or 'no'."),
            ("hangman_already_active", "You're already playing Hangman! Guess a letter or the whole word."),
            ("hangman_surrender", "No problem! The word was '{0}'. Maybe next time!"),
            ("hangman_invalid", "Please guess a single letter or the whole word."),
            ("hangman_repeat_letter", "You already guessed '{0}'. Try a different letter."),
        };

        context.BotResponses.AddRange(responses.Select(r => new BotResponse
        {
            Category = r.Category,
            ResponseText = r.ResponseText,
            CreatedAt = now
        }));
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

    private static void SeedErrorKnowledgeEntries(PokeChatDbContext context, string now)
    {
        if (context.ErrorKnowledgeEntries.Any()) return;

        var entries = new (string Pattern, string Suggestion)[]
        {
            // ── CS compiler errors ──
            (@"CS1009", "Unrecognised escape sequence. Use a verbatim string literal with @\"...\" or double the backslash."),
            (@"CS0103.*does not exist", "The name isn't defined in the current context. Check spelling, add a using directive, or declare the variable."),
            (@"CS0117.*does not contain a definition", "The type doesn't have that member. Check the spelling and that you're using the right type."),
            (@"CS0120.*object reference", "You need an instance reference to access a non-static member. Create an instance or make the member static."),
            (@"CS0161.*not all code paths return", "Your method has a code path that doesn't return a value. Add a return statement or throw an exception at the end."),
            (@"CS0246.*could not be found", "A type or namespace is missing. Add a using directive, install the NuGet package, or fix the type name."),
            (@"CS0266.*cannot implicitly convert", "Type mismatch — add an explicit cast. Example: `(int)value` or use `.Cast<T>()`."),
            (@"CS0305.*requires.*type arguments", "You're using a generic type without specifying type arguments. Add `<...>` with the required types."),
            (@"CS0428.*cannot convert method group", "You're using a method name without parentheses or without converting it to a delegate. Add `()` to call it."),
            (@"CS0433.*exists in both", "The type is defined in two different assemblies. Use an extern alias or fully qualify the type name."),
            (@"CS0612|CS0619", "The member is marked as obsolete. Check the error message for the suggested replacement."),
            (@"CS0650.*bad array declarator", "Use `new int[] { ... }` or `new[] { ... }` syntax, not `int[] array = new[];`."),
            (@"CS0841.*cannot use local variable.*before it is declared", "Move the variable declaration before its usage. In C#, variables must be declared before use."),
            (@"CS1002.*; expected", "You're missing a semicolon at the end of a statement. Add `;`."),
            (@"CS1010.*newline in constant", "A string literal has an unescaped newline. Use `\\n` for line breaks or a verbatim string."),
            (@"CS1026.*\) expected", "Missing a closing parenthesis. Check that all opening `(` have matching `)`."),
            (@"CS1501.*no overload.*takes", "You're calling a method with the wrong number of arguments. Check the method signature."),
            (@"CS1502.*best overloaded match", "The method call doesn't match any overload. Check parameter types and count."),
            (@"CS1503.*cannot convert from.*to", "You're passing the wrong type to a method parameter. Convert the value or use the correct type."),
            (@"CS1513.*} expected", "Missing a closing brace. Make sure every opening `{` has a matching `}`."),
            (@"CS1525.*invalid expression term", "Unexpected token in expression. Check for missing operators or misplaced keywords."),
            (@"CS1579.*foreach.*cannot operate", "The type doesn't implement `IEnumerable` or `IEnumerable<T>`. Make sure it has a `GetEnumerator` method."),
            (@"CS1591.*missing XML comment", "Add an XML doc comment (`/// <summary>...</summary>`) to the public member, or disable the warning with `#pragma warning disable 1591`."),
            (@"CS1955.*non-invocable member", "You're trying to call something that isn't a method or delegate. Use it without `()`."),
            (@"CS7036.*no argument given", "You're missing a required parameter. Provide all required arguments to the method or constructor."),
            (@"CS8129.*No suitable *Find* method", "Use `.FirstOrDefault()` or `.SingleOrDefault()` instead of a custom `Find` pattern."),
            (@"CS8370.*not available in C#", "The feature requires a newer C# version. Update the `<LangVersion>` in your `.csproj` or target a newer framework."),
            (@"CS8600.*null literal.*non-nullable", "You're assigning null to a non-nullable reference type. Use `T?` for nullable or add a null check."),
            (@"CS8602.*dereference of a possibly null reference", "The variable could be null. Add a null check with `?.` or an `if (x != null)` guard."),
            (@"CS8604.*possible null reference argument", "You're passing a potentially null value to a method that expects non-null. Add a null check."),
            (@"CS8618.*non-nullable.*not initialized", "A non-nullable property/field isn't set by the constructor. Initialize it or make it nullable."),
            (@"CS8625.*cannot convert null literal", "You're passing null where a non-nullable type is expected. Use `null!` to suppress or fix the type."),
            (@"CS8981.*naming convention", "The type name doesn't follow C# naming conventions. Use PascalCase for types."),

            // ── Runtime exceptions ──
            (@"NullReferenceException", "An object reference was null. Check that you've initialised the object before using it with `.` or `[]`."),
            (@"InvalidOperationException", "The operation isn't valid in the current state. Check preconditions before calling the method."),
            (@"ArgumentNullException", "A required argument was null. Check that you're passing a valid value to the method."),
            (@"IndexOutOfRangeException", "You're trying to access an index that's outside the array or list bounds. Check your loop condition and array length."),
            (@"DivideByZeroException", "You're dividing by zero. Add a check for zero before the division."),
            (@"FileNotFoundException", "The file wasn't found. Check the file path, working directory, and that the file exists."),
            (@"DirectoryNotFoundException", "The directory wasn't found. Check the path and create the directory if needed."),
            (@"UnauthorizedAccessException", "You don't have permission to access that resource. Run with elevated privileges or check file permissions."),
            (@"PathTooLongException", "The file path exceeds the system maximum length. Use shorter path or file names."),
            (@"InvalidCastException", "An invalid cast was attempted. Use `as` with a null check or `is` pattern matching instead."),
            (@"FormatException", "The input string wasn't in the correct format. Use `TryParse` instead of `Parse` to handle bad input gracefully."),
            (@"OverflowException", "A numeric value overflowed. Use `checked` blocks or `TryParse` to detect overflows."),
            (@"TimeoutException", "The operation timed out. Increase the timeout duration or check network/server connectivity."),
            (@"TaskCanceledException", "The task was cancelled. Check for cancellation token usage or timeout settings."),
            ((@"HttpRequestException|WebException"), "A network request failed. Check your internet connection, the URL, and API endpoint availability."),
            (@"JsonException.*deserialize", "JSON couldn't be deserialised. Check that the JSON matches your model's structure and property names."),
            ((@"SqliteException|SqlException"), "A database error occurred. Check your connection string, database file path, and SQL syntax."),

            // ── Build / MSBuild / NuGet ──
            (@"NU1603.*dependency.*specified", "A NuGet package dependency version is not exactly specified. Add a `<PackageReference Version=\"...\" />` to pin it."),
            (@"NU1605.*detected package downgrade", "A NuGet package was downgraded. Add a direct `<PackageReference>` with the version you want."),
            (@"NETSDK1004.*Assets file not found", "NuGet assets are missing. Run `dotnet restore` to restore packages."),
            (@"NETSDK1045.*not installed", "The target framework isn't installed. Install the required .NET SDK or change the `<TargetFramework>`."),
            (@"NETSDK1083.*not found", "A file referenced in the project wasn't found. Check that the file exists at the specified path."),
            (@"MSB3030.*could not be copied", "A referenced file couldn't be copied — it may be locked by another process. Close other programs and rebuild."),
            (@"MSB4018.*unexpected error", "The MSBuild task failed unexpectedly. Check the error details above in the build output."),
            (@"MSB4062.*could not be loaded", "An MSBuild task assembly couldn't be loaded. Restore NuGet packages and check for version mismatches."),
            (@"warning MSB3270", "There's a bitness mismatch between your project and a referenced assembly. Check platform targets."),

            // ── .NET EF / CLI ──
            (@"No DbContext was found", "Ensure your DbContext class is public and in the startup project. Add `dotnet ef` package references."),
            (@"provider.*not found.*UseSqlServer", "Make sure you've installed the EF Core provider package for your database (e.g. `Microsoft.EntityFrameworkCore.Sqlite`)."),
            (@"migration.*already exists", "A migration with that name already exists. Use a different name or run `dotnet ef migrations remove` first."),
            (@"The binary operator.*not defined", "You can't use that operator with the given types. Check operand types — e.g. can't use `==` on structs without overload."),
            (@"Anonymous type.*not supported", "Anonymous types can't be used in that context. Create a named type or use a tuple."),
            (@"The name.*does not exist in.*current context", "A variable or type name isn't recognised. Check spelling, scope, and `using` directives."),

            // ── ASP.NET / Web ──
            (@"HTTP 404.*not found", "The URL route wasn't found. Check your controller name, action name, and route attributes."),
            (@"HTTP 500.*internal server", "The server encountered an error. Check your application logs for the full exception details."),
            (@"HTTP 400.*bad request", "The request was malformed. Check that your JSON body matches the expected model structure."),
            (@"HTTP 401.*unauthorized", "Authentication is required. Add an `Authorize` attribute or provide credentials."),
            (@"HTTP 403.*forbidden", "You don't have permission to access this resource. Check your authorisation roles and claims."),
            (@"HTTP 429.*too many requests", "You're being rate-limited. Add a delay between requests and respect the `Retry-After` header."),
            (@"Connection refused", "The server isn't running or isn't accepting connections. Make sure the service is started and the port is correct."),
            (@"SSL.*certificate.*invalid", "The SSL certificate is invalid or self-signed. For development, set `ServicePointManager.ServerCertificateValidationCallback`."),
        };

        context.ErrorKnowledgeEntries.AddRange(entries.Select(e => new ErrorKnowledgeEntry
        {
            Pattern = e.Pattern,
            Suggestion = e.Suggestion,
            Language = "general",
            IsLearned = false,
            UsedCount = 0,
            SuccessCount = 0,
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

    private static void SeedRhymeGroups(PokeChatDbContext context, string now)
    {
        if (context.RhymeGroups.Any()) return;

        var groups = new (string Key, string Word, string Type)[]
        {
            ("at", "cat", "noun"), ("at", "hat", "noun"), ("at", "bat", "noun"),
            ("at", "rat", "noun"), ("at", "mat", "noun"), ("at", "sat", "verb"),
            ("at", "that", "pronoun"), ("at", "flat", "adjective"),
            ("ake", "cake", "noun"), ("ake", "lake", "noun"), ("ake", "bake", "verb"),
            ("ake", "make", "verb"), ("ake", "take", "verb"), ("ake", "shake", "verb"),
            ("ake", "wake", "verb"), ("ake", "brake", "noun"),
            ("ight", "night", "noun"), ("ight", "light", "noun"), ("ight", "bright", "adjective"),
            ("ight", "sight", "noun"), ("ight", "fight", "verb"), ("ight", "might", "verb"),
            ("ight", "right", "adjective"), ("ight", "tight", "adjective"),
            ("ock", "rock", "noun"), ("ock", "lock", "noun"), ("ock", "clock", "noun"),
            ("ock", "block", "noun"), ("ock", "sock", "noun"), ("ock", "knock", "verb"),
            ("ock", "dock", "noun"), ("ock", "shock", "noun"),
            ("ing", "king", "noun"), ("ing", "ring", "noun"), ("ing", "wing", "noun"),
            ("ing", "thing", "noun"), ("ing", "spring", "noun"), ("ing", "sing", "verb"),
            ("ing", "bring", "verb"), ("ing", "fling", "verb"),
            ("ell", "bell", "noun"), ("ell", "tell", "verb"), ("ell", "sell", "verb"),
            ("ell", "well", "adverb"), ("ell", "fell", "verb"), ("ell", "shell", "noun"),
            ("ell", "smell", "verb"), ("ell", "yell", "verb"),
            ("ime", "time", "noun"), ("ime", "lime", "noun"), ("ime", "dime", "noun"),
            ("ime", "chime", "noun"), ("ime", "climb", "verb"), ("ime", "prime", "adjective"),
            ("ime", "sublime", "adjective"),
            ("one", "bone", "noun"), ("one", "stone", "noun"), ("one", "phone", "noun"),
            ("one", "throne", "noun"), ("one", "alone", "adjective"),
            ("ain", "rain", "noun"), ("ain", "pain", "noun"), ("ain", "train", "noun"),
            ("ain", "brain", "noun"), ("ain", "plain", "adjective"), ("ain", "main", "adjective"),
            ("ain", "explain", "verb"),
            ("ump", "jump", "verb"), ("ump", "bump", "noun"), ("ump", "pump", "noun"),
            ("ump", "lump", "noun"), ("ump", "dump", "verb"), ("ump", "stump", "noun"),
            ("ice", "nice", "adjective"), ("ice", "ice", "noun"), ("ice", "price", "noun"),
            ("ice", "mice", "noun"), ("ice", "spice", "noun"), ("ice", "advice", "noun"),
            ("ide", "ride", "verb"), ("ide", "hide", "verb"), ("ide", "side", "noun"),
            ("ide", "wide", "adjective"), ("ide", "glide", "verb"), ("ide", "pride", "noun"),
            ("ide", "inside", "noun"),
            ("unk", "sunk", "verb"), ("unk", "junk", "noun"), ("unk", "trunk", "noun"),
            ("unk", "drunk", "adjective"), ("unk", "chunk", "noun"),
            ("ink", "think", "verb"), ("ink", "sink", "verb"), ("ink", "drink", "verb"),
            ("ink", "pink", "adjective"), ("ink", "blink", "verb"), ("ink", "link", "noun"),
            ("ank", "bank", "noun"), ("ank", "tank", "noun"), ("ank", "rank", "noun"),
            ("ank", "sank", "verb"), ("ank", "plank", "noun"), ("ank", "blank", "adjective"),
            ("and", "hand", "noun"), ("and", "sand", "noun"), ("and", "land", "noun"),
            ("and", "band", "noun"), ("and", "stand", "verb"), ("and", "understand", "verb"),
            ("eam", "team", "noun"), ("eam", "dream", "noun"), ("eam", "stream", "noun"),
            ("eam", "cream", "noun"), ("eam", "scream", "verb"),
            ("eet", "meet", "verb"), ("eet", "feet", "noun"), ("eet", "sheet", "noun"),
            ("eet", "street", "noun"), ("eet", "sweet", "adjective"),
            ("op", "top", "noun"), ("op", "stop", "verb"), ("op", "drop", "verb"),
            ("op", "shop", "noun"), ("op", "pop", "noun"), ("op", "hop", "verb"),
            ("un", "fun", "noun"), ("un", "sun", "noun"), ("un", "run", "verb"),
            ("un", "gun", "noun"), ("un", "bun", "noun"), ("un", "done", "adjective"),
        };

        context.RhymeGroups.AddRange(groups.Select(g => new RhymeGroup
        {
            RhymeKey = g.Key,
            Word = g.Word,
            WordType = g.Type,
            CreatedAt = now
        }));
    }

    private static void SeedPoemTemplates(PokeChatDbContext context, string now)
    {
        if (context.PoemTemplates.Any()) return;

        var haikuTemplates = new[]
        {
            "an {adj} {noun} falls\n{adj} {noun} {verb}ing in the {noun}\n{adj} {noun} {verb}s",
            "the {adj} {noun} pond\n{art} {noun} jumps into the {noun}\n{noun} {verb}s {adv}",
            "{adj} {noun} {noun}\n{verb}ing through the {adj} {noun} {noun}\n{adj} {noun} {verb}s",
            "{noun} in the {noun}\n{adj} {noun} {verb}ing {adv} {prep} {noun}\n{noun} {verb}s again",
            "when {noun} {verb}s {adv}\n{art} {noun} {verb}s {prep} the {noun}\n{adj} {noun} {verb}s",
            "{adj} {noun} {verb}s\nover the {adj} {noun} and {noun}\n{adv} the {noun} {verb}s",
            "{noun} {verb}s in {place}\n{adj} {noun} {verb}ing {prep} the {noun}\n{noun} {verb}s no more",
            "under the {noun}\n{adj} {noun} {verb}ing for {noun}\n{noun} {verb}s alone",
            "{adj} {noun} morning\n{noun} {verb}s {prep} the {adj} {noun}\n{adj} {noun} {verb}s on",
            "above the {noun}\n{art} {noun} {verb}s {adv} and {verb}s\n{adv} the {noun} {verb}s",
            "{noun} after {noun}\n{adj} {noun} {verb}ing through the {noun}\n{noun} {verb}s {adv}",
            "the {adj} {noun} wind\n{verb}s through the {adj} {noun} {noun}\n{noun} {verb}s {adv}",
        };

        context.PoemTemplates.AddRange(haikuTemplates.Select(t => new PoemTemplate
        {
            Template = t,
            PoemType = "haiku",
            CreatedAt = now
        }));

        var limerickTemplates = new[]
        {
            "there once was {art} {a_rhyme} from {place}\nwho had {art} {a_rhyme} all over {pron} face\n{pron} would {verb} every {noun}\nin {art} {b_rhyme} {noun}\nand {verb} with {adj} {a_rhyme} grace",
            "a {adj} {a_rhyme} from {place}\nfound {art} {a_rhyme} with {adj} grace\n{pron} {verb}ed {art} {noun}\nand {art} {b_rhyme} {noun}\nand smiled with {art} {a_rhyme} face",
            "there lived {art} {adj} {a_rhyme}\nwho dreamed of {art} {a_rhyme} all the time\n{pron} {verb}ed every day\nin {art} {b_rhyme} way\nand said it was {adj} and sublime",
            "i knew {art} {adj} {a_rhyme} from {place}\nwho painted with {adj} {a_rhyme} grace\n{pron} {verb}ed all the {noun}\nwith {art} {b_rhyme} noun\nand smiled with a smile on {pron} face",
            "there was an old {a_rhyme} from {place}\nwho had a most {adj} {a_rhyme}\n{pron} {verb}ed every {noun}\nin {art} {b_rhyme} noun\ntill the {noun} fell {adv} on {pron} face",
            "a {adj} {a_rhyme} i once knew\n{verb}ed {adv} and then {verb}ed too\n{pron} {verb}ed {art} {noun}\nwith {art} {b_rhyme} noun\nand then {verb}ed away from view",
            "the {adj} {a_rhyme} of {place}\nhad a {adj} {a_rhyme} on {pron} face\n{pron} {verb}ed every {noun}\nwith {art} {b_rhyme} noun\nand {verb}ed with remarkable grace",
            "i met {art} {adj} {a_rhyme}\nwho {verb}ed all the {adj} {noun} time\n{pron} {verb}ed {art} {noun}\nwith {art} {b_rhyme} noun\nand said it was {adj} and sublime",
            "there once was {art} {a_rhyme} so {adj}\nwho {verb}ed {art} {adj} {a_rhyme}\n{pron} {verb}ed every {noun}\nin {art} {b_rhyme} noun\ntill the {noun} went {adv} and flat",
            "from the {adj} hills of {place}\ncame {art} {a_rhyme} with {adj} grace\n{pron} {verb}ed {art} {noun}\nwith {art} {b_rhyme} noun\nand {verb}ed a smile on {pron} face",
        };

        context.PoemTemplates.AddRange(limerickTemplates.Select(t => new PoemTemplate
        {
            Template = t,
            PoemType = "limerick",
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
            ("shell_blocked", "I'm not allowed to run that command."),
            ("shell_blocked", "That command isn't on my allowed list."),
            ("shell_blocked", "I can't execute that — it's not in my permitted commands."),
            ("shell_error", "That command returned an error."),
            ("shell_error", "The command didn't run successfully."),
            ("file_blocked", "I'm not allowed to access files outside the project directory."),
            ("file_blocked", "That file is outside my allowed directories."),
            ("file_blocked", "I can't operate on files in that location."),
            ("file_error", "I had trouble with that file operation."),
            ("file_error", "Something went wrong with the file operation."),
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
            ("haiku_response", "A haiku for you:\n\n{0}"),
            ("haiku_response", "Here is a haiku:\n\n{0}"),
            ("haiku_response", "A poem, if you will:\n\n{0}"),
            ("limerick_response", "A limerick, if you please:\n\n{0}"),
            ("limerick_response", "Here's a limerick:\n\n{0}"),
            ("limerick_response", "How about a limerick:\n\n{0}"),
            ("poem_time", "Would you like to hear a haiku or a limerick?"),
            ("poem_time", "I could write you a poem. Haiku or limerick?"),
            ("cross_session_recall", "Last time we spoke on {0}, you mentioned {1} {2} {3} — how's that going?"),
            ("cross_session_recall", "I recall that on {0}, you said {1} {2} {3}. What's new?"),
            ("cross_session_recall", "Last time you were here, we talked about {3}. Is that still a thing?"),
            ("cross_session_recall", "You told me {1} {2} {3} last time. Any updates?"),
            ("cross_session_recall", "I remember from {0} that you told me {1} {2} {3}. How are things?"),
            ("interview_intro", "Interview mode started! I'll chat with my AI to learn new things. Type 'stop' to end."),
            ("interview_intro", "Training mode activated! Sit back while I have a conversation with my AI."),
            ("interview_complete", "Interview finished! I learned {0} new facts and {1} new rules."),
            ("interview_complete", "That's the end of training. I picked up {0} facts and {1} new patterns."),
            ("interview_stopped", "Interview stopped. I'll keep what I learned so far."),
            ("interview_no_llm", "I need my AI available to run the interview. Try again later."),
            ("user_fact_list", "Here's what I know about you:\n{0}"),
            ("user_fact_list", "You told me:\n{0}"),
            ("user_fact_none", "I don't know much about you yet — we've only just met!"),
            ("user_fact_none", "I haven't learned much about you yet. Tell me something!"),
            ("user_stats", "Here's what I know about you:\n{0}"),
            ("user_stats", "Here are your stats:\n{0}"),
            ("compliment", "You're great at {0}!"),
            ("compliment", "I love that you {0}!"),
            ("compliment", "It's awesome that you {0}!"),
            ("compliment", "You have amazing taste — {0}!"),
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
            ("quiz_question", "Quiz time ({1}/{2}): {0}"),
            ("quiz_correct", "That's right! The answer was {0}."),
            ("quiz_correct", "Correct! {0} is right."),
            ("quiz_wrong", "Not quite! The answer was {0}."),
            ("quiz_wrong", "Sorry, the answer was {0}."),
            ("quiz_score", "Quiz complete! You got {0}/{1} correct."),
            ("quiz_score", "All done! Your score: {0}/{1}."),
            ("quiz_already_active", "You're already in a quiz! Answer the current question."),
            ("quiz_already_active", "One quiz at a time! Finish this one first."),
            ("quiz_no_facts", "I don't know enough about you to make a quiz yet."),
            ("quiz_no_facts", "I need to learn more about you before I can quiz you."),
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
            ("error_knowledge_found", "That looks like a {0} error. {1}"),
            ("error_knowledge_found", "I think I know this one — it's a {0}: {1}"),
            ("error_knowledge_found", "That's a {0} error. Try this: {1}"),
            ("error_knowledge_not_found", "I haven't seen that error before. Can you tell me what fixed it?"),
            ("error_knowledge_not_found", "I don't recognise that error. What's the fix?"),
            ("error_knowledge_learned", "Thanks! I'll remember that fix for next time."),
            ("error_knowledge_learned", "Got it! I'll add that to my error knowledge."),
            ("error_knowledge_unknown", "I don't know that error yet. Can you explain the fix?"),
            ("error_knowledge_followup", "Did that fix the problem?"),
            ("error_knowledge_followup", "Did that help resolve the error?"),
        };

        context.BotResponses.AddRange(responses.Select(r => new BotResponse
        {
            Category = r.Category,
            ResponseText = r.ResponseText,
            CreatedAt = now
        }));

        var codingResponses = new (string Category, string ResponseText)[]
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
        context.BotResponses.AddRange(codingResponses.Select(r => new BotResponse
        {
            Category = r.Category,
            ResponseText = r.ResponseText,
            CreatedAt = now,
            Persona = "coding"
        }));
    }
}

namespace PokeChat.Core;

public enum RouteHandler
{
    None,
    Math,
    Remind,
    Story,
    Poem,
    Haiku,
    Limerick,
    Joke,
    Riddle,
    Quiz,
    Game,
    Hangman,
    SwitchPersona,
    Stats,
    AboutMe,
    Reset,
    Help,
    Weather,
    Cleanup,
    Rate,
    Project,
    Plan,
    Plans
}

public class RouteResult
{
    public RouteHandler Handler { get; set; }
    public string? Argument { get; set; }
    public string? OriginalInput { get; set; }
    public bool IsBotCommand { get; set; }
    public string? IntentCategory { get; set; }
    public double Confidence { get; set; }
}

public class RouterService
{
    private static readonly Dictionary<string, RouteHandler> BotCommandMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["maths"] = RouteHandler.Math,
        ["math"] = RouteHandler.Math,
        ["remind"] = RouteHandler.Remind,
        ["story"] = RouteHandler.Story,
        ["poem"] = RouteHandler.Poem,
        ["haiku"] = RouteHandler.Haiku,
        ["limerick"] = RouteHandler.Limerick,
        ["joke"] = RouteHandler.Joke,
        ["riddle"] = RouteHandler.Riddle,
        ["quiz"] = RouteHandler.Quiz,
        ["game"] = RouteHandler.Game,
        ["hangman"] = RouteHandler.Hangman,
        ["switch"] = RouteHandler.SwitchPersona,
        ["stats"] = RouteHandler.Stats,
        ["about"] = RouteHandler.AboutMe,
        ["reset"] = RouteHandler.Reset,
        ["help"] = RouteHandler.Help,
        ["weather"] = RouteHandler.Weather,
        ["cleanup"] = RouteHandler.Cleanup,
        ["rate"] = RouteHandler.Rate,
        ["project"] = RouteHandler.Project,
        ["plan"] = RouteHandler.Plan,
        ["plans"] = RouteHandler.Plans
    };

    private static readonly Dictionary<string, RouteHandler> IntentHandlerMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["math_query"] = RouteHandler.Math,
        ["story_request"] = RouteHandler.Story,
        ["poetry_request"] = RouteHandler.Poem,
        ["joke_request"] = RouteHandler.Joke,
        ["riddle_start"] = RouteHandler.Riddle,
        ["game_start"] = RouteHandler.Game,
        ["hangman_start"] = RouteHandler.Hangman,
        ["reset_request"] = RouteHandler.Reset,
        ["compliment_request"] = RouteHandler.AboutMe,
        ["about_me_query"] = RouteHandler.AboutMe,
        ["stats_query"] = RouteHandler.Stats,
        ["weather_query"] = RouteHandler.Weather,
        ["plan_query"] = RouteHandler.Plans,
        ["farewell"] = RouteHandler.None,
    };

    private static readonly HashSet<string> HandlerIntentCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "math_query", "story_request", "poetry_request", "joke_request",
        "riddle_start", "game_start", "hangman_start", "reset_request",
        "compliment_request", "about_me_query", "stats_query", "weather_query", "plan_query"
    };

    private const float ConfidenceThreshold = 0.85f;

    public RouteResult Route(string input)
    {
        if (string.IsNullOrEmpty(input) || input.Length <= 1)
            return new RouteResult { Handler = RouteHandler.None, OriginalInput = input };

        if (input[0] == '~')
        {
            var botResult = TryParseBotCommand(input);
            if (botResult != null)
                return botResult;
        }

        return new RouteResult { Handler = RouteHandler.None, OriginalInput = input };
    }

    public RouteResult Route(string input, ML.IntentClassifier? classifier)
    {
        if (string.IsNullOrEmpty(input) || input.Length <= 1)
            return new RouteResult { Handler = RouteHandler.None, OriginalInput = input };

        if (input[0] == '~')
        {
            var botResult = TryParseBotCommand(input);
            if (botResult != null)
                return botResult;
        }

        if (classifier?.IsReady == true)
        {
            var probs = classifier.PredictProbabilities(input);
            var maxConf = probs.Length > 0 ? probs.Max() : 0f;
            var intent = classifier.Classify(input);

            if (intent != null && maxConf >= ConfidenceThreshold)
            {
                if (IntentHandlerMap.TryGetValue(intent, out var handler) && handler != RouteHandler.None)
                {
                    return new RouteResult
                    {
                        Handler = handler,
                        OriginalInput = input,
                        IntentCategory = intent,
                        Confidence = maxConf
                    };
                }
            }
        }

        return new RouteResult { Handler = RouteHandler.None, OriginalInput = input };
    }

    private static RouteResult? TryParseBotCommand(string input)
    {
        var afterTilde = input[1..];

        if (LooksLikePath(afterTilde))
            return null;

        var spaceIdx = input.IndexOf(' ', StringComparison.Ordinal);
        string cmd;
        string? arg;

        if (spaceIdx > 0)
        {
            cmd = input[1..spaceIdx];
            arg = input[(spaceIdx + 1)..].Trim();
        }
        else
        {
            cmd = input[1..];
            arg = null;
        }

        if (BotCommandMap.TryGetValue(cmd, out var handler))
        {
            return new RouteResult
            {
                Handler = handler,
                Argument = string.IsNullOrEmpty(arg) ? null : arg,
                OriginalInput = input,
                IsBotCommand = true
            };
        }

        if (cmd.Length > 1 && arg == null)
        {
            var plusIdx = cmd.IndexOf('+');
            var minusIdx = cmd.IndexOf('-');
            var sepIdx = plusIdx > 0 ? plusIdx : minusIdx;

            if (sepIdx > 0 && sepIdx < cmd.Length - 1)
            {
                var stripped = cmd[..sepIdx];
                var suffix = cmd[sepIdx..];
                if (BotCommandMap.TryGetValue(stripped, out handler))
                {
                    return new RouteResult
                    {
                        Handler = handler,
                        Argument = suffix,
                        OriginalInput = input,
                        IsBotCommand = true
                    };
                }
            }
        }

        return null;
    }

    private static bool LooksLikePath(string afterTilde)
    {
        if (afterTilde.Contains('/') || afterTilde.Contains('\\'))
            return true;

        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(homeDir))
        {
            var expanded = Path.Combine(homeDir, afterTilde);
            if (Directory.Exists(expanded) || File.Exists(expanded))
                return true;
        }

        return false;
    }
}

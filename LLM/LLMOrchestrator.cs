using System.Text.Json;

namespace PokeChat.LLM;

public class LLMConfig
{
    public bool Enabled { get; set; }
    public bool AlwaysOn { get; set; }
    public string Endpoint { get; set; } = "http://localhost:11434";
    public string Model { get; set; } = "llama3.2";
    public int TimeoutMs { get; set; } = 30000;
    public int MaxCallsPerSession { get; set; }
    public bool SummariseToolResults { get; set; } = true;
    public List<string> EnhancedCategories { get; set; } = new();
    public string SystemPrompt { get; set; } = string.Empty;
}

public class LLMOrchestrator : IDisposable
{
    private readonly ILLMProvider? _provider;
    public LLMConfig Config { get; } = new();
    public bool IsAvailable => _provider != null && Config.Enabled;
    public bool IsAccepted { get; private set; }
    public bool UserDeclined { get; private set; }
    public int CallsThisSession { get; private set; }

    public LLMOrchestrator(string configPath = "tools/llm.json")
    {
        Config = LoadConfig(configPath);
        if (Config != null && Config.Enabled && !string.IsNullOrEmpty(Config.Endpoint))
            _provider = new OllamaProvider(Config.Endpoint, Config.Model, Config.TimeoutMs);
    }

    internal LLMOrchestrator(ILLMProvider provider, LLMConfig config)
    {
        _provider = provider;
        Config = config;
    }

    public static readonly string HomeworkCheckSystemPrompt =
        "You are a QA assistant for a learning chatbot. Review the conversation below for mistakes.\n\n" +
        "If any learned response rules are incorrect or don't match what the user intended, flag them.\n" +
        "If the user taught words that could use a definition, suggest one.\n" +
        "If words were taught but not classified, suggest a category.\n\n" +
        "Return ONLY valid JSON (no markdown, no extra text):\n" +
        "{\n" +
        "  \"rules_to_remove\": [{\"rule_id\": 1, \"reason\": \"...\"}],\n" +
        "  \"definitions_to_add\": [{\"word\": \"...\", \"definition\": \"...\"}],\n" +
        "  \"classifications_to_add\": [{\"word\": \"...\", \"category\": \"person|place|thing|verb\"}]\n" +
        "}";

    public void MarkAccepted()
    {
        IsAccepted = true;
    }

    public void MarkDeclined()
    {
        UserDeclined = true;
    }

    public string? GenerateResponse(string input)
    {
        if (_provider == null || UserDeclined) return null;
        if (!Config.AlwaysOn && Config.MaxCallsPerSession > 0 && CallsThisSession >= Config.MaxCallsPerSession)
            return null;

        CallsThisSession++;
        return _provider.GenerateResponse(input, Config.SystemPrompt);
    }

    public string? GenerateWordForGame(string storySoFar)
    {
        if (_provider == null || UserDeclined) return null;

        var systemPrompt = "You are playing a word game. Add EXACTLY ONE word that continues the story in a " +
            "funny or interesting way, following proper grammar. Return ONLY that word, no punctuation or explanation.";
        var result = _provider.GenerateResponse($"The story so far is: '{storySoFar}'", systemPrompt);
        if (string.IsNullOrEmpty(result)) return null;

        var firstWord = result.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
        return firstWord.ToLowerInvariant();
    }

    public string? GenerateGameStorySummary(string storyWords)
    {
        if (_provider == null || UserDeclined) return null;

        var prompt = $"Here are some words chosen in a word game: '{storyWords}'. Write a short, funny story (2-3 sentences) using these words. Return only the story, no commentary.";
        return _provider.GenerateResponse(prompt, "");
    }

    public string? GenerateHomeworkCheck(string prompt)
    {
        if (_provider == null || UserDeclined) return null;
        return _provider.GenerateResponse(prompt, HomeworkCheckSystemPrompt);
    }

    public void Dispose()
    {
        if (_provider is IDisposable disposable)
            disposable.Dispose();
    }

    public string? GenerateInterviewInput(string prompt)
    {
        if (_provider == null || UserDeclined) return null;
        return _provider.GenerateResponse(prompt, InterviewSystemPrompt);
    }

    public string? GenerateTrainingLabels(string conversationJson)
    {
        if (_provider == null || UserDeclined) return null;

        var prompt = "Review this conversation and label each user turn with an intent category. " +
            "Categories: greeting, name_intro, preference_statement, dislike_statement, possession_statement, " +
            "belief_statement, personal_attribute, general_fact, math_query, dictionary_query, thesaurus_query, " +
            "story_request, poetry_request, joke_request, riddle_start, game_start, hangman_start, " +
            "correction_pattern, farewell, reset_request, compliment_request, about_me_query, stats_query, " +
            "yes_no_affirmation, question_about_bot.\n\n" +
            "Return ONLY valid JSON (no markdown):\n" +
            "{\"labels\":[{\"input\":\"user said this\",\"intent\":\"category\"}]}\n\n" +
            $"Conversation:\n{conversationJson}";

        return _provider.GenerateResponse(prompt, TrainingLabelsSystemPrompt);
    }

    public static readonly string TrainingLabelsSystemPrompt =
        "You are a conversation analyst. Given a chat log, label each user message " +
        "with the correct intent category from the provided list. Be accurate and specific. " +
        "Return only valid JSON.";

    public static readonly string InterviewSystemPrompt =
        "You are playing the role of a person being interviewed by a learning chatbot called PokeChat. " +
        "You are NOT an AI. You are a real person answering questions.\n\n" +
        "Your personality:\n" +
        "- You're friendly, curious, and happy to share about yourself\n" +
        "- Answer the current question naturally, building on what was discussed before\n" +
        "- If the bot asks for more details about something you said, provide more specific information\n" +
        "- If the bot asks about a new topic, switch to that new topic naturally\n" +
        "- Keep each answer to 1-2 simple sentences with clear subject-verb-object structure\n" +
        "- If you don't know something, say so naturally\n" +
        "- You never reveal you're an AI or mention being a language model\n\n" +
        "IMPORTANT: Only answer the question asked. Use simple single sentences " +
        "like 'I love pizza.' or 'I have two cats.' " +
        "Simple sentences with facts about yourself help the bot learn.";

    private static LLMConfig LoadConfig(string configPath)
    {
        if (!Path.IsPathRooted(configPath))
        {
            var root = Data.ProjectPathHelper.FindProjectRoot(AppContext.BaseDirectory);
            if (root != null)
                configPath = Path.Combine(root, configPath);
        }

        if (!File.Exists(configPath)) return new LLMConfig();

        try
        {
            var json = File.ReadAllText(configPath);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<LLMConfig>(json, options) ?? new LLMConfig();
        }
        catch
        {
            return new LLMConfig();
        }
    }
}

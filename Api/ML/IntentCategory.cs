namespace PokeChat.ML;

public static class IntentCategory
{
    public static readonly string[] DefaultCategories =
    {
        "greeting",
        "name_intro",
        "preference_statement",
        "dislike_statement",
        "possession_statement",
        "belief_statement",
        "personal_attribute",
        "general_fact",
        "math_query",
        "dictionary_query",
        "thesaurus_query",
        "story_request",
        "poetry_request",
        "joke_request",
        "riddle_start",
        "game_start",
        "hangman_start",
        "correction_pattern",
        "farewell",
        "reset_request",
        "compliment_request",
        "about_me_query",
        "stats_query",
        "plan_query",
        "train_task",
        "yes_no_affirmation",
        "question_about_bot",
        "complex_question",
        "unknown"
    };

    public static Dictionary<string, int> BuildIndex(string[] categories)
    {
        var idx = 0;
        return categories.ToDictionary(c => c, _ => idx++);
    }
}

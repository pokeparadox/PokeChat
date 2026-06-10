using PokeChat.LLM;

namespace PokeChat.Core;

public class InterviewEngine
{
    private readonly LLMOrchestrator _orchestrator;
    private readonly List<(string Role, string Text)> _conversationHistory;
    private readonly int _maxTurns;

    public int TurnsRemaining { get; private set; }
    public int FactsLearned { get; set; }
    public int RulesLearned { get; set; }

    public InterviewEngine(LLMOrchestrator orchestrator, int maxTurns = 8)
    {
        _orchestrator = orchestrator;
        _conversationHistory = new List<(string Role, string Text)>();
        _maxTurns = maxTurns;
        TurnsRemaining = maxTurns;
    }

    public string? GenerateUserInput()
    {
        if (TurnsRemaining <= 0) return null;

        var prompt = BuildPrompt();
        var result = _orchestrator.GenerateInterviewInput(prompt);

        if (string.IsNullOrEmpty(result)) return null;

        TurnsRemaining--;
        return result.Trim();
    }

    public void AddExchange(string userInput, string botResponse)
    {
        _conversationHistory.Add(("User", userInput));
        _conversationHistory.Add(("Bot", botResponse));
    }

    public void Reset()
    {
        _conversationHistory.Clear();
        TurnsRemaining = _maxTurns;
        FactsLearned = 0;
        RulesLearned = 0;
    }

    private string BuildPrompt()
    {
        if (_conversationHistory.Count == 0)
        {
            return "Start the conversation. Introduce yourself naturally — say hi, share something about yourself, " +
                   "and ask the bot a question. Keep it to 1-2 sentences.";
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Conversation so far:");

        foreach (var (role, text) in _conversationHistory)
        {
            sb.AppendLine($"{role}: {text}");
        }

        sb.Append("\nContinue the conversation as the user. Your next line:");
        return sb.ToString();
    }
}

namespace PokeChat.Core;

public interface IInterviewEngine
{
    int TurnsRemaining { get; }
    int FactsLearned { get; set; }
    int RulesLearned { get; set; }

    string? GenerateQuestion();
    string? GenerateAnswer(string question);
    void AddExchange(string question, string answer, string botResponse);
    void Reset();
}

namespace PokeChat.Core;

public interface IInterviewEngine
{
    int TurnsRemaining { get; }
    int FactsLearned { get; set; }
    int RulesLearned { get; set; }
    string? GenerateUserInput();
    void AddExchange(string userInput, string botResponse);
    void Reset();
}

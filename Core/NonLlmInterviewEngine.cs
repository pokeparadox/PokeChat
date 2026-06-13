namespace PokeChat.Core;

public class NonLlmInterviewEngine : IInterviewEngine
{
    private readonly List<string> _askedQuestions;
    private readonly int _maxTurns;

    public int TurnsRemaining { get; private set; }
    public int FactsLearned { get; set; }
    public int RulesLearned { get; set; }

    public NonLlmInterviewEngine(int maxTurns = 8)
    {
        _askedQuestions = new List<string>();
        _maxTurns = maxTurns;
        TurnsRemaining = maxTurns;
    }

    public string? GenerateUserInput()
    {
        if (TurnsRemaining <= 0) return null;

        var available = QuestionBank.Where(q => !_askedQuestions.Contains(q)).ToList();
        if (available.Count == 0) return null;

        var question = available[Random.Shared.Next(available.Count)];
        _askedQuestions.Add(question);
        TurnsRemaining--;
        return question;
    }

    public void AddExchange(string userInput, string botResponse)
    {
    }

    public void Reset()
    {
        _askedQuestions.Clear();
        TurnsRemaining = _maxTurns;
        FactsLearned = 0;
        RulesLearned = 0;
    }

    private static readonly string[] QuestionBank =
    {
        "What's your favourite colour?",
        "What's your favourite food?",
        "What's your favourite movie?",
        "What's your favourite book?",
        "What's your favourite animal?",
        "What's your favourite season?",
        "Do you have any hobbies?",
        "What do you do for fun?",
        "Do you like reading?",
        "Do you play any sports?",
        "Do you like cats or dogs?",
        "Do you prefer sweet or savoury?",
        "Do you prefer summer or winter?",
        "Are you a morning person or a night owl?",
        "Do you prefer tea or coffee?",
        "Have you travelled anywhere interesting?",
        "Do you play any musical instruments?",
        "Have you read any good books lately?",
        "What do you think about AI?",
        "Do you enjoy coding?",
        "What's your favourite place you've ever visited?",
        "Do you have any pets?",
        "What kind of music do you like?",
        "Do you prefer the mountains or the beach?",
        "What's something you've always wanted to learn?",
        "Do you like cooking?",
        "What's your favourite game?",
        "Do you enjoy spending time outdoors?",
        "What's the best advice you've ever received?",
        "If you could travel anywhere, where would you go?"
    };
}

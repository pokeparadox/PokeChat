using System.Text;
using PokeChat.Knowledge;
using PokeChat.LLM;

namespace PokeChat.Core;

public class InterviewEngine : IInterviewEngine
{
    private readonly LLMOrchestrator _orchestrator;
    private readonly KnowledgeStore _knowledgeStore;
    private readonly INounCategoriser _nounCategoriser;
    private readonly int _maxTurns;
    private readonly List<string> _askedNouns;
    private readonly List<(string Question, string Answer, string BotResponse)> _exchanges;

    public int TurnsRemaining { get; private set; }
    public int FactsLearned { get; set; }
    public int RulesLearned { get; set; }

    public InterviewEngine(LLMOrchestrator orchestrator, KnowledgeStore knowledgeStore, INounCategoriser nounCategoriser, int maxTurns = 8)
    {
        _orchestrator = orchestrator;
        _knowledgeStore = knowledgeStore;
        _nounCategoriser = nounCategoriser;
        _maxTurns = maxTurns;
        TurnsRemaining = maxTurns;
        _askedNouns = new List<string>();
        _exchanges = new List<(string, string, string)>();
    }

    public string? GenerateQuestion()
    {
        if (TurnsRemaining <= 0) return null;

        var nouns = GetAvailableNouns();
        var available = nouns.Where(n => !_askedNouns.Any(a => string.Equals(a, n.Word, StringComparison.OrdinalIgnoreCase))).ToList();
        if (available.Count == 0) return null;

        var noun = available[Random.Shared.Next(available.Count)];
        _askedNouns.Add(noun.Word);
        TurnsRemaining--;

        var category = noun.Category;
        if (category == null)
            category = _nounCategoriser.CategoriseNoun(noun.Word);

        return BuildQuestion(noun.Word, category);
    }

    public string? GenerateAnswer(string question)
    {
        var history = BuildHistory();
        var prompt = new StringBuilder();
        prompt.AppendLine("A chatbot named PokeChat is interviewing you. It asks questions and reacts to your answers.");
        if (history.Length > 0)
        {
            prompt.AppendLine("Here is the conversation so far. Pay attention to what was already discussed:");
            prompt.Append(history);
        }
        prompt.AppendLine($"\nThe bot's new question is: \"{question}\"");
        prompt.Append("Now respond as yourself. Continue naturally based on what was said before. " +
                      "Answer in one simple sentence with clear facts about yourself.");

        return _orchestrator.GenerateInterviewInput(prompt.ToString());
    }

    public void AddExchange(string question, string answer, string botResponse)
    {
        _exchanges.Add((question, answer, botResponse));
    }

    private string BuildHistory()
    {
        if (_exchanges.Count == 0) return string.Empty;

        var sb = new StringBuilder();
        foreach (var (q, a, r) in _exchanges)
        {
            sb.AppendLine($"Bot: {q}");
            sb.AppendLine($"You: {a}");
            sb.AppendLine($"Bot: {r}");
        }
        return sb.ToString();
    }

    private List<(string Word, string? Category)> GetAvailableNouns()
    {
        var nouns = new List<(string Word, string? Category)>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var categorised = _knowledgeStore.GetNounCategories();
        foreach (var nc in categorised)
        {
            if (seen.Add(nc.Noun))
                nouns.Add((nc.Noun, nc.Category));
        }

        var posEntries = _knowledgeStore.GetPosDictionary()
            .Where(e => e.WordType == "noun");

        foreach (var entry in posEntries)
        {
            if (seen.Add(entry.Word))
                nouns.Add((entry.Word, null));
        }

        return nouns;
    }

    private static string BuildQuestion(string noun, string category)
    {
        var templates = category.ToLowerInvariant() switch
        {
            "person" => PersonTemplates,
            "place" => PlaceTemplates,
            _ => ThingTemplates
        };

        var template = templates[Random.Shared.Next(templates.Length)];
        return string.Format(template, noun);
    }

    public void Reset()
    {
        _askedNouns.Clear();
        _exchanges.Clear();
        TurnsRemaining = _maxTurns;
        FactsLearned = 0;
        RulesLearned = 0;
    }

    private static readonly string[] PersonTemplates =
    [
        "Tell me about {0}.",
        "Who is {0}?",
        "What's {0} like?",
        "How do you know {0}?"
    ];

    private static readonly string[] PlaceTemplates =
    [
        "Have you been to {0}?",
        "What do you think about {0}?",
        "What's {0} like?"
    ];

    private static readonly string[] ThingTemplates =
    [
        "Do you like {0}?",
        "What do you think of {0}?",
        "Tell me more about {0}.",
        "What can you tell me about {0}?"
    ];
}

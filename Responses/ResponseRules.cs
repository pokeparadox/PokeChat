using System.Text.RegularExpressions;
using PokeChat.Knowledge;

namespace PokeChat.Responses;

public enum InputType
{
    Greeting,
    Question,
    Statement,
    Unknown
}

public record ResponseRuleRecord
{
    public string Pattern { get; set; } = string.Empty;
    public List<string> Responses { get; set; } = new();
    public InputType InputType { get; set; }
    public int RuleId { get; set; }
    public bool IsLearned { get; set; }
    public int Confidence { get; set; } = 8;
}

public static class ResponseRules
{
    public static ResponseRuleRecord? MatchRule(string input, KnowledgeStore knowledgeStore)
    {
        var lowerInput = input.ToLowerInvariant();

        var learnedRules = knowledgeStore.GetLearnedRules();
        ResponseRuleRecord? bestLearned = null;

        foreach (var rule in learnedRules)
        {
            if (rule.Pattern.Length <= 0) continue;
            if (!Regex.IsMatch(lowerInput, rule.Pattern)) continue;

            if (bestLearned == null || rule.Confidence > bestLearned.Confidence)
            {
                bestLearned = new ResponseRuleRecord
                {
                    Pattern = rule.Pattern,
                    InputType = ParseInputType(rule.InputType),
                    Responses = new List<string> { rule.ResponseTemplate },
                    RuleId = rule.Id,
                    IsLearned = true,
                    Confidence = rule.Confidence
                };
            }
        }

        var seededRules = knowledgeStore.GetResponseRules();
        ResponseRuleRecord? bestSeeded = null;
        var bestSeededLength = 0;

        foreach (var rule in seededRules)
        {
            if (rule.Pattern.Length <= 0) continue;
            var match = Regex.Match(lowerInput, rule.Pattern);
            if (!match.Success) continue;
            if (match.Length <= bestSeededLength) continue;

            bestSeededLength = match.Length;
            bestSeeded = new ResponseRuleRecord
            {
                Pattern = rule.Pattern,
                InputType = ParseInputType(rule.InputType),
                Responses = rule.Responses.Select(r => r.ResponseText).ToList(),
                RuleId = rule.Id,
                IsLearned = false,
                Confidence = 8
            };
        }

        if (bestLearned == null) return bestSeeded;
        if (bestSeeded == null) return bestLearned;

        return bestLearned.Confidence >= 7 ? bestLearned : bestSeeded;
    }

    private static InputType ParseInputType(string inputType)
    {
        return inputType.ToLowerInvariant() switch
        {
            "greeting" => InputType.Greeting,
            "question" => InputType.Question,
            "statement" => InputType.Statement,
            _ => InputType.Unknown
        };
    }
}

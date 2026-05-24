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
}

public static class ResponseRules
{
    public static ResponseRuleRecord? MatchRule(string input, KnowledgeStore knowledgeStore)
    {
        var lowerInput = input.ToLowerInvariant();
        var rules = knowledgeStore.GetResponseRules();

        foreach (var rule in rules)
        {
            if (rule.Pattern.Length > 0 && Regex.IsMatch(lowerInput, rule.Pattern))
            {
                return new ResponseRuleRecord
                {
                    Pattern = rule.Pattern,
                    InputType = ParseInputType(rule.InputType),
                    Responses = rule.Responses.Select(r => r.ResponseText).ToList()
                };
            }
        }

        return null;
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

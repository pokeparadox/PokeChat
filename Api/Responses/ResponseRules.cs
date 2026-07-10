using System.Text.RegularExpressions;
using PokeChat.Knowledge;
using PokeChat.Core;

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
        return MatchRule(input, knowledgeStore, null, null, null, null);
    }

    public static ResponseRuleRecord? MatchRule(string input, KnowledgeStore knowledgeStore, List<ResponseRuleRecord>? toolTriggers)
    {
        return MatchRule(input, knowledgeStore, toolTriggers, null, null, null);
    }

    public static ResponseRuleRecord? MatchRule(string input, KnowledgeStore knowledgeStore, List<ResponseRuleRecord>? toolTriggers, ML.IntentClassifier? classifier, ContextTracker? context, string? persona = null)
    {
        if (classifier != null && classifier.IsReady && context != null)
        {
            var probs = classifier.PredictProbabilities(input);
            var maxConf = probs.Length > 0 ? probs.Max() : 0f;
            var intent = classifier.Classify(input);
            if (intent != null)
            {
                context.SetContext(ContextKeys.CurrentIntent, intent);
                context.SetContext(ContextKeys.IntentConfidence, maxConf.ToString("F4"));
            }
            else
            {
                context.SetContext(ContextKeys.CurrentIntent, null);
                context.SetContext(ContextKeys.IntentConfidence, maxConf.ToString("F4"));
            }
        }
        else if (context != null)
        {
            context.SetContext(ContextKeys.CurrentIntent, null);
            context.SetContext(ContextKeys.IntentConfidence, null);
        }
        var lowerInput = input.ToLowerInvariant();

        var learnedRules = knowledgeStore.GetLearnedRules();
        ResponseRuleRecord? bestLearnedHigh = null;
        ResponseRuleRecord? bestLearnedLow = null;

        foreach (var rule in learnedRules)
        {
            if (rule.Pattern.Length <= 0) continue;
            if (!Regex.IsMatch(lowerInput, rule.Pattern)) continue;

            if (bestLearnedHigh == null && rule.Confidence >= 7)
            {
                bestLearnedHigh = new ResponseRuleRecord
                {
                    Pattern = rule.Pattern,
                    InputType = ParseInputType(rule.InputType),
                    Responses = new List<string> { rule.ResponseTemplate },
                    RuleId = rule.Id,
                    IsLearned = true,
                    Confidence = rule.Confidence
                };
            }
            else if (bestLearnedLow == null || rule.Confidence > (bestLearnedLow?.Confidence ?? 0))
            {
                if (rule.Confidence < 7)
                {
                    bestLearnedLow = new ResponseRuleRecord
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
        }

        var seededRules = knowledgeStore.GetResponseRules(persona);
        ResponseRuleRecord? bestSeededOrTrigger = null;
        var bestSeededOrTriggerLength = 0;

        foreach (var rule in seededRules)
        {
            if (rule.Pattern.Length <= 0) continue;
            var match = Regex.Match(lowerInput, rule.Pattern);
            if (!match.Success) continue;
            if (match.Length <= bestSeededOrTriggerLength) continue;

            bestSeededOrTriggerLength = match.Length;
            bestSeededOrTrigger = new ResponseRuleRecord
            {
                Pattern = rule.Pattern,
                InputType = ParseInputType(rule.InputType),
                Responses = rule.Responses.Select(r => r.ResponseText).ToList(),
                RuleId = rule.Id,
                IsLearned = false,
                Confidence = 8
            };
        }

        if (toolTriggers != null)
        {
            foreach (var trigger in toolTriggers)
            {
                if (trigger.Pattern.Length <= 0) continue;
                var match = Regex.Match(lowerInput, trigger.Pattern);
                if (!match.Success) continue;
                if (match.Length < bestSeededOrTriggerLength) continue;
                if (match.Length == bestSeededOrTriggerLength && bestSeededOrTrigger != null &&
                    trigger.Pattern.Length <= bestSeededOrTrigger.Pattern.Length) continue;

                bestSeededOrTriggerLength = match.Length;
                bestSeededOrTrigger = trigger;
            }
        }

        if (bestLearnedHigh != null) return bestLearnedHigh;
        if (bestSeededOrTrigger != null) return bestSeededOrTrigger;
        return bestLearnedLow;
    }

    internal static InputType ParseInputType(string inputType)
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

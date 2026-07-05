using PokeChat.Data.Entities;
using PokeChat.Knowledge;
using PokeChat.ML;
using PokeChat.Responses;
using PokeChat.Tests.Helpers;
using Shouldly;

namespace PokeChat.Tests.Responses;

public class ResponseRulesTests
{
    [Fact]
    public void MatchRule_Greeting_ReturnsRule()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);
        SeedRule(db.Context, "^(hi|hello)", "Greeting", ["Hello there!", "Hi!"]);
        var result = ResponseRules.MatchRule("hi", store);
        result.ShouldNotBeNull();
        result.InputType.ShouldBe(InputType.Greeting);
    }

    [Fact]
    public void MatchRule_NoMatch_ReturnsNull()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);
        SeedRule(db.Context, "^(hi|hello)", "Greeting", ["Hello!"]);
        var result = ResponseRules.MatchRule("goodbye", store);
        result.ShouldBeNull();
    }

    [Fact]
    public void MatchRule_ReturnsResponses()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);
        SeedRule(db.Context, "hello", "Greeting", ["Hi there!", "Hey!"]);
        var result = ResponseRules.MatchRule("hello", store);
        result.ShouldNotBeNull();
        result.Responses.Count.ShouldBe(2);
    }

    [Fact]
    public void MatchRule_ToolTrigger_MatchesBeforeGenericSeeded()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);
        SeedRule(db.Context, @"what do you (know|remember)", "Question", ["I don't know."]);
        var toolTriggers = new List<ResponseRuleRecord>
        {
            new()
            {
                Pattern = @"what do you know about (.+)",
                InputType = InputType.Question,
                Responses = new List<string> { "Let me check. {tool:search:{$1}}" },
                RuleId = -1,
                Confidence = 8
            }
        };
        var result = ResponseRules.MatchRule("what do you know about cats", store, toolTriggers);
        result.ShouldNotBeNull();
        result.Pattern.ShouldBe(@"what do you know about (.+)");
        result.Responses[0].ShouldBe("Let me check. {tool:search:{$1}}");
    }

    [Fact]
    public void MatchRule_ToolTrigger_LongestPatternWins()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);
        var toolTriggers = new List<ResponseRuleRecord>
        {
            new()
            {
                Pattern = @"search (.+)",
                InputType = InputType.Statement,
                Responses = new List<string> { "Short {tool:search:{$1}}" },
                RuleId = -1,
                Confidence = 8
            },
            new()
            {
                Pattern = @"search (your )?(memory|memories) for (.+)",
                InputType = InputType.Statement,
                Responses = new List<string> { "Long {tool:mempalace:{$3}}" },
                RuleId = -1,
                Confidence = 8
            }
        };
        var result = ResponseRules.MatchRule("search your memories for cats", store, toolTriggers);
        result.ShouldNotBeNull();
        result.Pattern.ShouldBe(@"search (your )?(memory|memories) for (.+)");
        result.Responses[0].ShouldBe("Long {tool:mempalace:{$3}}");
    }

    [Fact]
    public void MatchRule_LearnedRule_OutranksToolTrigger()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);
        store.LearnResponseRule(@"what do you know about (.+)", "I know a lot!", "Question", null);
        store.Save();
        var learnedRules = store.GetLearnedRules();
        var learnedId = learnedRules[0].Id;
        store.AdjustConfidence(learnedId, 2);
        store.Save();
        var toolTriggers = new List<ResponseRuleRecord>
        {
            new()
            {
                Pattern = @"what do you know about (.+)",
                InputType = InputType.Question,
                Responses = new List<string> { "Tool trigger response" },
                RuleId = -1,
                Confidence = 8
            }
        };
        var result = ResponseRules.MatchRule("what do you know about cats", store, toolTriggers);
        result.ShouldNotBeNull();
        result.IsLearned.ShouldBeTrue();
        result.Responses[0].ShouldBe("I know a lot!");
    }

    [Fact]
    public void MatchRule_ToolTrigger_NullTriggers_FallsBackToNormal()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);
        SeedRule(db.Context, "^(hi|hello)", "Greeting", ["Hello!"]);
        var result = ResponseRules.MatchRule("hi", store, null);
        result.ShouldNotBeNull();
        result.InputType.ShouldBe(InputType.Greeting);
    }

    [Fact]
    public void MatchRule_WithClassifier_SetsIntentAndConfidence()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);
        SeedRule(db.Context, "^(hi|hello)", "Greeting", ["Hello!"]);

        var classifier = new PokeChat.ML.IntentClassifier();
        classifier.Train(SeedTrainingData.Examples.ToList());

        var context = new PokeChat.Knowledge.ContextTracker();
        var result = ResponseRules.MatchRule("hello", store, null, classifier, context);

        result.ShouldNotBeNull();
        var intent = context.GetContext("current_intent");
        intent.ShouldBe("greeting");
        var confidence = context.GetContext("intent_confidence");
        confidence.ShouldNotBeNull();
        float.Parse(confidence).ShouldBeGreaterThan(0.5f);
    }

    [Fact]
    public void MatchRule_WithClassifier_LowConfidence_SetsConfidence()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);
        SeedRule(db.Context, "^(.+)", "Statement", ["You said something."]);

        var classifier = new PokeChat.ML.IntentClassifier();
        classifier.Train(SeedTrainingData.Examples.ToList());

        var context = new PokeChat.Knowledge.ContextTracker();
        var result = ResponseRules.MatchRule("xylophone quantum nebula", store, null, classifier, context);

        result.ShouldNotBeNull();
        var intent = context.GetContext("current_intent");
        intent.ShouldBe("unknown");
        var confidence = context.GetContext("intent_confidence");
        confidence.ShouldNotBeNull();
        float.Parse(confidence).ShouldBeGreaterThan(0f);
    }

    [Fact]
    public void MatchRule_WithClassifierNotReady_DoesNotSetIntentOrConfidence()
    {
        using var db = new FreshDbContext();
        var store = new KnowledgeStore(db.Context);
        SeedRule(db.Context, "^(hi|hello)", "Greeting", ["Hello!"]);

        var classifier = new PokeChat.ML.IntentClassifier();
        var context = new PokeChat.Knowledge.ContextTracker();
        var result = ResponseRules.MatchRule("hi", store, null, classifier, context);

        result.ShouldNotBeNull();
        var intent = context.GetContext("current_intent");
        intent.ShouldBeNull();
        var confidence = context.GetContext("intent_confidence");
        confidence.ShouldBeNull();
    }

    private static void SeedRule(PokeChat.Data.PokeChatDbContext context, string pattern, string inputType, string[] responses)
    {
        var rule = new ResponseRule
        {
            Pattern = pattern,
            InputType = inputType,
            IsActive = true,
            CreatedAt = DateTime.UtcNow.ToString("o"),
            Responses = responses.Select(r => new ResponseRuleResponse { ResponseText = r }).ToList()
        };
        context.ResponseRules.Add(rule);
        context.SaveChanges();
    }
}

using System.Text.RegularExpressions;
using PokeChat.Core;
using PokeChat.Knowledge;
using PokeChat.Math;
using PokeChat.NLP;

namespace PokeChat.Responses;

public class ResponseEngine
{
    private readonly KnowledgeStore _knowledgeStore;
    private readonly ContextTracker _context;
    private readonly SpellChecker _spellChecker;
    private readonly IPosTagger _posTagger;
    private readonly ITokeniser _tokeniser;
    private readonly ISvoExtractor _svoExtractor;
    private readonly IMathEngine _mathEngine;
    private readonly Dictionary<string, List<string>> _botResponses;

    public ResponseEngine(KnowledgeStore knowledgeStore, ContextTracker context, SpellChecker spellChecker, IPosTagger posTagger, ITokeniser tokeniser, ISvoExtractor svoExtractor, IMathEngine? mathEngine = null)
    {
        _knowledgeStore = knowledgeStore;
        _context = context;
        _spellChecker = spellChecker;
        _posTagger = posTagger;
        _tokeniser = tokeniser;
        _svoExtractor = svoExtractor;
        _mathEngine = mathEngine ?? new SimpleMath();
        _botResponses = knowledgeStore.GetBotResponses();
    }

    internal static string ConjugateVerb(string verb, string subject)
    {
        var lowerVerb = verb.ToLowerInvariant();
        var lowerSubject = subject.ToLowerInvariant();

        if (lowerSubject is "i" or "you" or "we" or "they")
            return verb;

        if (lowerVerb is "is" or "am" or "are") return "is";
        if (lowerVerb is "was") return "was";
        if (lowerVerb is "were") return "were";
        if (lowerVerb is "have") return "has";
        if (lowerVerb is "do") return "does";
        if (lowerVerb is "go") return "goes";
        if (lowerVerb is "say") return "says";

        if (lowerVerb.EndsWith("s") || lowerVerb.EndsWith("sh") ||
            lowerVerb.EndsWith("ch") || lowerVerb.EndsWith("x") ||
            lowerVerb.EndsWith("z") || lowerVerb.EndsWith("o"))
            return verb + "es";

        if (lowerVerb.Length > 1 && lowerVerb.EndsWith("y") && !"aeiou".Contains(lowerVerb[lowerVerb.Length - 2]))
            return verb[..^1] + "ies";

        return verb + "s";
    }

    private string GetRandomResponse(string category, params object[] args)
    {
        if (_botResponses.TryGetValue(category, out var responses) && responses.Count > 0)
        {
            var template = responses[Random.Shared.Next(responses.Count)];
            return args.Length > 0 ? string.Format(template, args) : template;
        }

        return string.Empty;
    }

    public string GenerateResponse(string input, int? userId)
    {
        var summaryResult = HandleSessionSummaryRequest(input, userId);
        if (summaryResult != null) return summaryResult;

        var unknownWordsRaw = _context.GetContext(ContextKeys.UnknownWords);
        if (!string.IsNullOrEmpty(unknownWordsRaw))
        {
            var unknownWords = unknownWordsRaw
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Distinct()
                .ToList();

            if (unknownWords.Count > 0)
            {
                var word = unknownWords[0];

                if (unknownWords.Count > 1)
                    _context.SetContext(ContextKeys.UnknownWords, string.Join(",", unknownWords.Skip(1)));
                else
                    _context.SetContext(ContextKeys.UnknownWords, null);
                if (_spellChecker.HasSuggestions(word))
                {
                    var suggestions = _spellChecker.SuggestCorrections(word);
                    var suggestion = suggestions[0];
                    _context.SetContext(ContextKeys.PendingClarificationWord, word);
                    _context.SetContext(ContextKeys.PendingClarificationSuggestion, suggestion);
                    return GetRandomResponse("unknown_word_suggestion", suggestion, word);
                }
                else
                {
                    _context.SetContext(ContextKeys.PendingClarificationWord, word);
                    _context.SetContext(ContextKeys.PendingClarificationSuggestion, null);
                    return GetRandomResponse("unknown_word_no_suggestion", word);
                }
            }
        }

        var pendingSentiment = _context.GetContext(ContextKeys.PendingSentimentFollowUp);
        if (!string.IsNullOrEmpty(pendingSentiment))
        {
            _context.SetContext(ContextKeys.PendingSentimentFollowUp, null);
            var intensityRaw = _context.GetContext(ContextKeys.LastSentimentIntensity);
            if (int.TryParse(intensityRaw, out var intensity) && intensity >= 1)
            {
                var currentSentiment = _context.GetContext(ContextKeys.CurrentSentiment);
                var ackCat = currentSentiment switch
                {
                    "positive" => "sentiment_ack_positive",
                    "negative" => "sentiment_ack_negative",
                    _ => "sentiment_ack"
                };
                var ack = GetRandomResponse(ackCat);
                if (!string.IsNullOrEmpty(ack))
                    return ack;
            }
        }

        var sentimentResult = HandleSentiment();
        if (sentimentResult != null) return sentimentResult;

        var mathResult = _mathEngine.Evaluate(input);
        if (mathResult != null)
        {
            if (mathResult.StatedResult.HasValue)
            {
                if (System.Math.Abs(mathResult.Value - mathResult.StatedResult.Value) > 0.0001)
                    return GetRandomResponse("math_correction", mathResult.Expression, mathResult.Value, mathResult.StatedResult);
                return GetRandomResponse("math_confirmation", mathResult.Expression, mathResult.Value);
            }
            return GetRandomResponse("math_result", mathResult.Expression, mathResult.Value);
        }

        var dictResult = HandleDictionaryQuery(input);
        if (dictResult != null) return dictResult;

        var thesaurusResult = HandleThesaurusQuery(input);
        if (thesaurusResult != null) return thesaurusResult;

        var linkResult = HandleLinkCreation(input);
        if (linkResult != null) return linkResult;

        var temporalResult = HandleTemporalQuery(input, userId);
        if (temporalResult != null) return temporalResult;

        var inferenceResult = HandleInferenceResponse();
        if (inferenceResult != null) return inferenceResult;

        var rule = ResponseRules.MatchRule(input, _knowledgeStore);

        if (rule != null && rule.Responses.Count > 0)
        {
            _context.SetContext(ContextKeys.LastRuleId, rule.RuleId.ToString());
            _context.SetContext(ContextKeys.LastRuleIsLearned, rule.IsLearned ? "true" : "false");
            return rule.Responses[Random.Shared.Next(rule.Responses.Count)];
        }

        var tokens = _tokeniser.Tokenise(input);
        var correctedTokens = _spellChecker.AutoCorrect(tokens);
        var tags = _posTagger.Tag(correctedTokens);
        var triples = _svoExtractor.Extract(correctedTokens, tags);

        foreach (var triple in triples)
        {
            var existingFact = _knowledgeStore.GetFact(triple.Subject, triple.Verb, triple.Object);
            if (existingFact != null)
            {
                var conjVerb = ConjugateVerb(triple.Verb, triple.Subject);
                return GetRandomResponse("existing_fact", triple.Subject, conjVerb, triple.Object);
            }
        }

        if (!string.IsNullOrEmpty(_context.LastSubject))
        {
            var countRaw = _context.GetContext(ContextKeys.ContextFollowUpCount);
            int.TryParse(countRaw, out var followUpCount);
            followUpCount++;
            _context.SetContext(ContextKeys.ContextFollowUpCount, followUpCount.ToString());

            if (followUpCount < 3)
            {
                var subject = _context.LastSubject;
                var subjCat = _context.GetContext(ContextKeys.SubjectCategory);

                if (!string.IsNullOrEmpty(_context.LastObject))
                {
                    var obj = _context.LastObject;
                    return GetRandomResponse("context_followup_with_object", subject, obj);
                }

                if (!string.IsNullOrEmpty(subjCat))
                {
                    var catResponse = GetRandomResponse($"context_followup_{subjCat}", subject);
                    if (!string.IsNullOrEmpty(catResponse))
                        return catResponse;
                }

                return GetRandomResponse("context_followup", subject);
            }

            var topicRef = BuildTopicFollowUp();
            if (topicRef != null)
                return topicRef;
        }

        var facts = userId.HasValue ? _knowledgeStore.GetFactsByUser(userId.Value) : new List<Fact>();
        if (facts.Count > 0 && Random.Shared.Next(3) == 0)
        {
            var randomFact = facts[Random.Shared.Next(facts.Count)];
            var conjVerb = ConjugateVerb(randomFact.Verb, randomFact.Subject);
            return GetRandomResponse("random_fact_followup", randomFact.Subject, conjVerb, randomFact.Object);
        }

        return GenerateProactiveQuestion(userId);
    }

    private string? BuildTopicFollowUp()
    {
        var topics = _context.GetRecentTopics(5);
        if (topics.Count == 0) return null;

        var lastSubject = _context.LastSubject;

        var olderTopic = topics.FirstOrDefault(t =>
            !string.IsNullOrEmpty(t.Subject) &&
            (lastSubject == null ||
             !string.Equals(t.Subject, lastSubject, StringComparison.OrdinalIgnoreCase)));

        if (olderTopic == null) return null;

        if (olderTopic.PredicateType is PredicateType.GeneralFact or PredicateType.PersonalAttribute or PredicateType.Belief)
        {
            var conjVerb = ConjugateVerb(olderTopic.Verb, olderTopic.Subject);
            return GetRandomResponse("topic_reference_fact", olderTopic.Subject, conjVerb, olderTopic.Object);
        }

        if (olderTopic.PredicateType is PredicateType.Preference or PredicateType.Dislike or PredicateType.Possession)
            return GetRandomResponse("topic_reference_old", olderTopic.Subject);

        return GetRandomResponse("topic_reference_old", olderTopic.Subject);
    }

    private string GenerateProactiveQuestion(int? userId)
    {
        if (userId == null)
            return GetRandomResponse("default_response");

        var allFacts = _knowledgeStore.GetFactsByUser(userId.Value);
        if (allFacts.Count == 0)
            return GetRandomResponse("default_response");

        var recentRaw = _context.GetContext(ContextKeys.RecentlyUsedFacts);
        var recent = string.IsNullOrEmpty(recentRaw)
            ? new HashSet<string>()
            : recentRaw.Split(',', StringSplitOptions.RemoveEmptyEntries).ToHashSet();

        var available = allFacts
            .Where(f => !recent.Contains($"{f.Subject}|{f.Verb}|{f.Object}"))
            .ToList();

        if (available.Count == 0)
            return GetRandomResponse("default_response");

        var fact = available[Random.Shared.Next(available.Count)];
        var signature = $"{fact.Subject}|{fact.Verb}|{fact.Object}";

        var updated = recent.TakeLast(4).Append(signature).ToList();
        _context.SetContext(ContextKeys.RecentlyUsedFacts, string.Join(",", updated));

        var (cat, args) = BuildProactiveQuestion(fact);
        var response = GetRandomResponse(cat, args);
        if (!string.IsNullOrEmpty(response))
            return response;

        return GetRandomResponse("default_response");
    }

    private static (string Category, object[] Args) BuildProactiveQuestion(Fact fact)
    {
        var (subj, verb, obj) = (fact.Subject, fact.Verb, fact.Object);
        var conjVerb = ConjugateVerb(verb, subj);

        return fact.PredicateType switch
        {
            nameof(PredicateType.Preference) => ("proactive_preference", new object[] { obj, subj, verb }),
            nameof(PredicateType.Dislike) => ("proactive_dislike", new object[] { obj, subj, verb }),
            nameof(PredicateType.Possession) => ("proactive_possession", new object[] { obj, subj, verb }),
            nameof(PredicateType.Belief) => ("proactive_belief", new object[] { obj, subj, verb }),
            nameof(PredicateType.PersonalAttribute) => ("proactive_personal", new object[] { obj, subj, verb }),
            nameof(PredicateType.GeneralFact) => ("proactive_general_fact", new object[] { subj, conjVerb, obj }),
            _ => ("proactive_general", new object[] { obj, subj, verb })
        };
    }

    private string? HandleSentiment()
    {
        var currentSentiment = _context.GetContext(ContextKeys.CurrentSentiment);
        if (string.IsNullOrEmpty(currentSentiment) || currentSentiment == "neutral")
            return null;

        var intensityRaw = _context.GetContext(ContextKeys.LastSentimentIntensity);
        int.TryParse(intensityRaw, out var intensity);
        if (intensity < 2)
            return null;

        var previousSentiment = _context.GetContext(ContextKeys.PreviousSentiment);

        if (previousSentiment != null && previousSentiment != currentSentiment)
        {
            _context.SetContext(ContextKeys.PendingSentimentFollowUp, "true");
            return GetRandomResponse("emotion_followup", previousSentiment);
        }

        var category = currentSentiment switch
        {
            "positive" => "empathy_happy",
            "negative" => "empathy_sad",
            "anger" => "empathy_angry",
            "fear" => "empathy_afraid",
            "surprise" => "empathy_surprised",
            _ => "emotion_unknown"
        };

        var response = GetRandomResponse(category);
        if (!string.IsNullOrEmpty(response))
            return response;

        return null;
    }

    private string? HandleDictionaryQuery(string input)
    {
        var lower = input.ToLowerInvariant().Trim();

        var patterns = new (Regex Regex, int WordGroup)[]
        {
            (new Regex(@"^what is the (?:definition|meaning) of (?:a|an|the\s+)?(\w+)"), 1),
            (new Regex(@"^what (?:is|are|was|were) (?:a|an|the\s+)?(\w+)$"), 1),
            (new Regex(@"^what does (\w+) mean"), 1),
            (new Regex(@"^what do (\w+) mean"), 1),
            (new Regex(@"^define (\w+)"), 1),
            (new Regex(@"^tell me about (\w+)"), 1),
        };

        foreach (var (regex, group) in patterns)
        {
            var match = regex.Match(lower);
            if (match.Success)
            {
                var word = match.Groups[group].Value.ToLowerInvariant();

                var definitions = _knowledgeStore.GetDefinitions(word);
                if (definitions.Count > 0)
                {
                    var def = definitions[0].Definition;
                    return GetRandomResponse("dictionary_query_found", word, def);
                }

                _context.SetContext(ContextKeys.PendingDictionaryWord, word);
                return GetRandomResponse("dictionary_query_not_found", word);
            }
        }

        return null;
    }

    private string? HandleThesaurusQuery(string input)
    {
        var lower = input.ToLowerInvariant().Trim();

        var patterns = new (Regex Regex, int WordGroup)[]
        {
            (new Regex(@"^(?:another word|synonyms?|words?) (?:for|like|similar to) (\w+)"), 1),
            (new Regex(@"^what (?:is|are) (?:another word|a synonym|synonyms) for (\w+)"), 1),
            (new Regex(@"^give me (?:another word|a synonym|synonyms) for (\w+)"), 1),
        };

        foreach (var (regex, group) in patterns)
        {
            var match = regex.Match(lower);
            if (match.Success)
            {
                var word = match.Groups[group].Value.ToLowerInvariant();
                var related = _knowledgeStore.GetRelatedWords(word);

                if (related.Count > 0)
                {
                    var joined = string.Join(", ", related.Take(5));
                    return GetRandomResponse("thesaurus_query_found", word, joined);
                }

                return GetRandomResponse("thesaurus_query_none", word);
            }
        }

        return null;
    }

    private string? HandleTemporalQuery(string input, int? userId)
    {
        if (userId == null) return null;

        var lower = input.ToLowerInvariant().Trim();
        var match = Regex.Match(lower,
            @"(?:what did I do|what happened|tell me about)\s+(yesterday|today|earlier|last night|this week|last week|this month|last month|recently|lately|a while ago|long ago|last year)");
        if (!match.Success)
        {
            match = Regex.Match(lower, @"(?:what did I do|what happened|tell me about)\s+(.+)");
            if (!match.Success) return null;
        }

        var timeExpr = match.Groups[1].Value.ToLowerInvariant();
        var facts = _knowledgeStore.GetFactsWithTimeContext(userId.Value, timeExpr);

        if (facts.Count == 0)
        {
            facts = _knowledgeStore.GetFactsByUser(userId.Value);
            if (facts.Count == 0)
                return GetRandomResponse("temporal_fact_none", timeExpr);
        }

        if (facts.Count == 1)
        {
            var f = facts[0];
            var conjVerb = ConjugateVerb(f.Verb, f.Subject);
            return GetRandomResponse("temporal_fact_found", timeExpr, f.Subject, conjVerb, f.Object);
        }

        var summaries = facts.Take(3).Select(f => $"{f.Subject} {f.Verb} {f.Object}");
        var joined = string.Join("; ", summaries);
        return GetRandomResponse("temporal_fact_list", timeExpr, joined);
    }

    private string? HandleInferenceResponse()
    {
        var contradictionRaw = _context.GetContext(ContextKeys.LastContradiction);
        if (!string.IsNullOrEmpty(contradictionRaw))
        {
            _context.SetContext(ContextKeys.LastContradiction, null);

            var parts = contradictionRaw.Split('|');
            if (parts.Length == 4)
            {
                return GetRandomResponse("inference_contradiction", parts[0], parts[1], parts[2], parts[3]);
            }
        }

        var generalisationRaw = _context.GetContext(ContextKeys.InferredGeneralisation);
        if (!string.IsNullOrEmpty(generalisationRaw) && Random.Shared.Next(2) == 0)
        {
            _context.SetContext(ContextKeys.InferredGeneralisation, null);

            var parts = generalisationRaw.Split('|');
            if (parts.Length == 2)
            {
                return GetRandomResponse("inference_generalisation", parts[0], parts[1]);
            }
        }

        return null;
    }

    private string? HandleSessionSummaryRequest(string input, int? userId)
    {
        if (userId == null) return null;

        var lower = input.ToLowerInvariant().Trim();
        var isSummaryRequest = lower.Contains("what did we talk about") ||
                               lower.Contains("summarise our conversation") ||
                               lower.Contains("summarize our conversation") ||
                               lower.Contains("what have we discussed") ||
                               lower.Contains("tell me what we talked about") ||
                               lower.Equals("summary") ||
                               lower.StartsWith("summary of");

        if (!isSummaryRequest) return null;

        var sessionId = _context.GetContext(ContextKeys.SessionId);
        if (string.IsNullOrEmpty(sessionId))
            return GetRandomResponse("session_summary_empty");

        var summary = _knowledgeStore.BuildSessionSummary(userId!.Value, sessionId);
        if (string.IsNullOrEmpty(summary))
            return GetRandomResponse("session_summary_empty");

        var factCount = summary.Split(new[] { ';', ')' }, StringSplitOptions.RemoveEmptyEntries).Length;
        if (factCount <= 2)
            return GetRandomResponse("session_summary_short", summary);

        return GetRandomResponse("session_summary_long", summary);
    }

    private string? HandleLinkCreation(string input)
    {
        var lower = input.ToLowerInvariant().Trim();

        var patterns = new (Regex Regex, int SourceGroup, int TargetGroup, string LinkType)[]
        {
            (new Regex(@"^(\w+) (?:is like|is similar to|is related to) (\w+)"), 1, 2, "similar"),
            (new Regex(@"^(\w+) (?:and|&) (\w+) are (?:similar|related|alike)"), 1, 2, "similar"),
            (new Regex(@"^(\w+) is a (?:type|kind|form) of (\w+)"), 1, 2, "type_of"),
            (new Regex(@"^(\w+) is a (?:synonym for|synonym of) (\w+)"), 1, 2, "synonym"),
            (new Regex(@"^(\w+) is the opposite of (\w+)"), 1, 2, "antonym"),
        };

        foreach (var (regex, sourceGroup, targetGroup, linkType) in patterns)
        {
            var match = regex.Match(lower);
            if (match.Success)
            {
                var source = match.Groups[sourceGroup].Value.ToLowerInvariant();
                var target = match.Groups[targetGroup].Value.ToLowerInvariant();

                if (source == target) continue;

                _knowledgeStore.AddWordLink(source, target, linkType);
                _knowledgeStore.AddWordLink(target, source, linkType);

                return GetRandomResponse("link_saved", source, target);
            }
        }

        return null;
    }
}

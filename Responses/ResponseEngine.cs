using System.Text.RegularExpressions;
using PokeChat.Core;
using PokeChat.Knowledge;
using PokeChat.Math;
using PokeChat.NLP;
using PokeChat.Stories;
using PokeChat.Tools;

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
    private readonly StoryGenerator _storyGenerator;
    private readonly ToolRegistry? _toolRegistry;
    private readonly List<ResponseRuleRecord>? _toolTriggers;
    private readonly Dictionary<string, List<string>> _botResponses;
    private readonly Func<string, string?>? _llmGenerator;
    private readonly HashSet<string> _enhancedCategories;
    private readonly bool _summariseToolResults;
    private string _currentUserName = string.Empty;
    private string _botName = "PokeChat";
    private string? _currentUserInput;

    private static readonly HashSet<string> ObjectPronouns = new(StringComparer.OrdinalIgnoreCase)
        { "you", "me", "him", "her", "them", "it", "us", "this", "that" };

    public void SetCurrentUserName(string name)
    {
        _currentUserName = name;
    }

    public void SetBotName(string name)
    {
        _botName = name;
    }

    public static bool IsDefaultCategory(string category)
    {
        return category == "default_response";
    }

    public static bool IsDeadEndCategory(string category)
    {
        return category == "default_response"
            || category == "story_response"
            || category == "random_fact_followup"
            || (category != null && category.StartsWith("proactive_"));
    }

    public ResponseEngine(KnowledgeStore knowledgeStore, ContextTracker context, SpellChecker spellChecker, IPosTagger posTagger, ITokeniser tokeniser, ISvoExtractor svoExtractor, IMathEngine? mathEngine = null, StoryGenerator? storyGenerator = null, ToolRegistry? toolRegistry = null, List<ResponseRuleRecord>? toolTriggers = null, Func<string, string?>? llmGenerator = null, HashSet<string>? enhancedCategories = null, bool summariseToolResults = false)
    {
        _knowledgeStore = knowledgeStore;
        _context = context;
        _spellChecker = spellChecker;
        _posTagger = posTagger;
        _tokeniser = tokeniser;
        _svoExtractor = svoExtractor;
        _mathEngine = mathEngine ?? new SimpleMath();
        _storyGenerator = storyGenerator ?? new StoryGenerator(knowledgeStore);
        _toolRegistry = toolRegistry;
        _toolTriggers = toolTriggers;
        _botResponses = knowledgeStore.GetBotResponses();
        _llmGenerator = llmGenerator;
        _enhancedCategories = enhancedCategories ?? new HashSet<string>();
        _summariseToolResults = summariseToolResults;
    }

    private static readonly HashSet<string> ModalVerbs = new(StringComparer.OrdinalIgnoreCase)
        { "can", "could", "will", "would", "shall", "should", "may", "might", "must" };

    public static string ConjugateVerb(string verb, string subject)
    {
        var lowerVerb = verb.ToLowerInvariant();
        var lowerSubject = subject.ToLowerInvariant();

        if (lowerSubject is "i" or "you" or "we" or "they")
            return verb;

        if (ModalVerbs.Contains(lowerVerb))
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
        _context.SetContext(ContextKeys.CurrentResponseCategory, category);

        if (_llmGenerator != null && _enhancedCategories.Contains(category) && !string.IsNullOrEmpty(_currentUserInput))
        {
            var prompt = BuildCategoryPrompt(category, args, _currentUserInput);
            var llmResult = _llmGenerator(prompt);
            if (!string.IsNullOrEmpty(llmResult))
                return llmResult;
        }

        if (_botResponses.TryGetValue(category, out var responses) && responses.Count > 0)
        {
            var template = responses[Random.Shared.Next(responses.Count)];
            return args.Length > 0 ? string.Format(template, args) : template;
        }

        return string.Empty;
    }

    private static string BuildCategoryPrompt(string category, object[] args, string userInput)
    {
        var arg = (int i) => i < args.Length ? (args[i]?.ToString() ?? "") : "";
        var subj = arg(0);
        var verb = arg(1);
        var obj = arg(2);

        var basePrompt = category switch
        {
            "existing_fact" =>
                $"The user once told you: {subj} {verb} {obj}. " +
                "Acknowledge this naturally and ask a brief follow-up question. 1-2 sentences.",

            "context_followup" or "context_followup_self" =>
                $"The user recently mentioned '{subj}'. " +
                "Ask a natural, specific follow-up question about it. 1 sentence.",

            "context_followup_with_object" or "context_followup_with_object_self" =>
                $"The user was talking about '{obj}' (related to {subj}). " +
                "Ask a specific follow-up question about it. 1 sentence.",

            "context_followup_person" =>
                $"The user mentioned a person: {subj}. " +
                "Ask a natural follow-up question about them. 1 sentence.",

            "context_followup_place" =>
                $"The user mentioned a place: {subj}. " +
                "Ask a natural follow-up question about it. 1 sentence.",

            "context_followup_thing" =>
                $"The user mentioned a thing: {subj}. " +
                "Ask a natural follow-up question about it. 1 sentence.",

            "random_fact_followup" =>
                $"The user once said: {subj} {verb} {obj}. " +
                "Bring this up naturally and ask if they still feel that way. 1-2 sentences.",

            "topic_reference_fact" =>
                $"Earlier, the user talked about: {subj} {verb} {obj}. " +
                "Refer back to this naturally and ask a follow-up. 1 sentence.",

            "topic_reference_old" =>
                $"The user mentioned '{subj}' earlier in the conversation. " +
                "Refer back to this and ask a natural question about it. 1 sentence.",

            "proactive_preference" =>
                $"The user likes '{obj}'. Ask a fresh, specific question about this interest. " +
                "Be creative and natural. 1 sentence.",

            "proactive_dislike" =>
                $"The user dislikes '{obj}'. Ask why or what they prefer instead. " +
                "Be natural. 1 sentence.",

            "proactive_possession" =>
                $"The user has '{obj}'. Ask a natural question about it. 1 sentence.",

            "proactive_belief" =>
                $"The user believes '{obj}'. Ask a follow-up about this belief. 1 sentence.",

            "proactive_personal" =>
                $"{subj} is '{obj}' (a personal attribute of the user). " +
                "Ask a natural follow-up. 1 sentence.",

            "proactive_general_fact" =>
                $"You know this fact: {subj} {verb} {obj}. " +
                "Ask a creative follow-up question about it. 1 sentence.",

            "proactive_general" =>
                $"You know something about {subj}: {verb} {obj}. " +
                "Ask a natural follow-up question. 1 sentence.",

            "session_summary_short" or "session_summary_long" =>
                $"The user discussed: {string.Join(", ", args.Select(a => a?.ToString()))}. " +
                "Summarize their conversation in a warm, natural way. " +
                "Don't list facts dryly — make it conversational. 1-2 sentences.",

            "empathy_happy" =>
                "The user expressed happiness or positivity. " +
                $"Their input: \"{userInput}\". " +
                "Respond with warm, genuine empathy that references what they said. 1 sentence.",

            "empathy_sad" =>
                "The user expressed sadness or negativity. " +
                $"Their input: \"{userInput}\". " +
                "Respond with gentle, supportive empathy. 1 sentence.",

            "empathy_angry" =>
                "The user expressed anger. " +
                $"Their input: \"{userInput}\". " +
                "Respond with calm understanding and support. 1 sentence.",

            "empathy_afraid" =>
                "The user expressed fear. " +
                $"Their input: \"{userInput}\". " +
                "Respond with reassurance and support. 1 sentence.",

            "empathy_surprised" =>
                "The user expressed surprise. " +
                $"Their input: \"{userInput}\". " +
                "Respond with warm interest. 1 sentence.",

            "story_response" =>
                $"Tell a very short original story (3-5 sentences) about {subj}. " +
                "Make it fun and lighthearted.",

            _ => $"The user said: \"{userInput}\". Respond naturally and conversationally. 1 sentence."
        };

        return basePrompt + " Be brief, conversational, and do not mention that you are an AI.";
    }

    public string GenerateResponse(string input, int? userId)
    {
        _currentUserInput = input;
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
            var intensityRaw = _context.GetContext(ContextKeys.PendingSentimentIntensity);
            _context.SetContext(ContextKeys.PendingSentimentIntensity, null);
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

        var storyResult = HandleStoryRequest(input, userId);
        if (storyResult != null) return storyResult;

        var inferenceResult = HandleInferenceResponse();
        if (inferenceResult != null) return inferenceResult;

        var rule = ResponseRules.MatchRule(input, _knowledgeStore, _toolTriggers);

        if (rule != null && rule.Responses.Count > 0)
        {
            _context.SetContext(ContextKeys.CurrentResponseCategory, "rule_match");
            _context.SetContext(ContextKeys.LastRuleId, rule.RuleId.ToString());
            _context.SetContext(ContextKeys.LastRuleIsLearned, rule.IsLearned ? "true" : "false");
            var response = rule.Responses[Random.Shared.Next(rule.Responses.Count)];
            var match = Regex.Match(input.ToLowerInvariant(), rule.Pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                for (int i = 1; i < match.Groups.Count; i++)
                    response = response.Replace("{$" + i + "}", match.Groups[i].Value);
            }
            var withBotName = response.Replace("{BOTNAME}", _botName);
            return ProcessToolMarkers(withBotName);
        }

        var predictionResult = HandlePredictionRequest(input);
        if (predictionResult != null) return predictionResult;

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
                var isSelf = !string.IsNullOrEmpty(_currentUserName) &&
                    string.Equals(subject, _currentUserName, StringComparison.OrdinalIgnoreCase);

                if (!string.IsNullOrEmpty(_context.LastObject) && !ObjectPronouns.Contains(_context.LastObject))
                {
                    var obj = _context.LastObject;
                    if (isSelf)
                    {
                        var selfResponse = GetRandomResponse("context_followup_with_object_self", subject, obj);
                        if (!string.IsNullOrEmpty(selfResponse))
                            return selfResponse;
                    }
                    return GetRandomResponse("context_followup_with_object", subject, obj);
                }

                if (!string.IsNullOrEmpty(subjCat))
                {
                    var catResponse = GetRandomResponse($"context_followup_{subjCat}", subject);
                    if (!string.IsNullOrEmpty(catResponse))
                        return catResponse;
                }

                if (isSelf)
                {
                    var selfResponse = GetRandomResponse("context_followup_self", subject);
                    if (!string.IsNullOrEmpty(selfResponse))
                        return selfResponse;
                }
                return GetRandomResponse("context_followup", subject);
            }

            var topicRefCountRaw = _context.GetContext(ContextKeys.TopicReferenceCount);
            int.TryParse(topicRefCountRaw, out var topicRefCount);
            topicRefCount++;
            _context.SetContext(ContextKeys.TopicReferenceCount, topicRefCount.ToString());

            if (topicRefCount < 3)
            {
                var topicRef = BuildTopicFollowUp();
                if (topicRef != null)
                    return topicRef;
            }
        }

        var facts = userId.HasValue ? _knowledgeStore.GetFactsByUser(userId.Value) : new List<Fact>();
        if (facts.Count > 0 && Random.Shared.Next(3) == 0)
        {
            var randomFact = facts[Random.Shared.Next(facts.Count)];
            var conjVerb = ConjugateVerb(randomFact.Verb, randomFact.Subject);
            return GetRandomResponse("random_fact_followup", randomFact.Subject, conjVerb, randomFact.Object);
        }

        if (Random.Shared.Next(6) == 0)
        {
            var story = _storyGenerator.GenerateStory(_currentUserName, userId);
            if (!string.IsNullOrEmpty(story))
            {
                var storyResponse = GetRandomResponse("story_response", story);
                if (!string.IsNullOrEmpty(storyResponse))
                    return storyResponse;
            }
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

    public string? BuildWyrQuestion(int? userId)
    {
        if (userId == null) return null;
        var (factA, factB) = _knowledgeStore.GetTwoRandomUserFacts(userId.Value);
        if (factA == null || factB == null) return null;

        var optionA = BuildWyrOption(factA);
        var optionB = BuildWyrOption(factB);

        _context.SetContext(ContextKeys.WyrActive, "true");
        _context.SetContext(ContextKeys.PendingWyrOptionA, optionA);
        _context.SetContext(ContextKeys.PendingWyrOptionB, optionB);

        var question = GetRandomResponse("wyr_question", optionA, optionB);
        _context.SetContext(ContextKeys.PendingWyrQuestion, question);

        return question;
    }

    public string HandleWouldYouRatherAcknowledge()
    {
        _context.SetContext(ContextKeys.WyrActive, null);
        _context.SetContext(ContextKeys.PendingWyrQuestion, null);
        var optionA = _context.GetContext(ContextKeys.PendingWyrOptionA);
        _context.SetContext(ContextKeys.PendingWyrOptionA, null);
        _context.SetContext(ContextKeys.PendingWyrOptionB, null);

        var chosen = Random.Shared.Next(2) == 0 ? optionA : null;
        return GetRandomResponse("wyr_acknowledgement", chosen ?? "");
    }

    private static string BuildWyrOption(Fact fact)
    {
        return fact.PredicateType switch
        {
            nameof(PredicateType.Preference) => $"having {fact.Object}",
            nameof(PredicateType.Dislike) => $"avoiding {fact.Object}",
            nameof(PredicateType.Possession) => $"owning {fact.Object}",
            nameof(PredicateType.PersonalAttribute) => $"being {fact.Object}",
            _ => $"{fact.Verb} {fact.Object}"
        };
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
            nameof(PredicateType.Preference) => ("proactive_preference", new object[] { obj, subj, conjVerb }),
            nameof(PredicateType.Dislike) => ("proactive_dislike", new object[] { obj, subj, conjVerb }),
            nameof(PredicateType.Possession) => ("proactive_possession", new object[] { obj, subj, conjVerb }),
            nameof(PredicateType.Belief) => ("proactive_belief", new object[] { obj, subj, conjVerb }),
            nameof(PredicateType.PersonalAttribute) => ("proactive_personal", new object[] { obj, subj, conjVerb }),
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

        if (previousSentiment != null && previousSentiment != currentSentiment && previousSentiment != "neutral")
        {
            _context.SetContext(ContextKeys.PendingSentimentFollowUp, "true");
            _context.SetContext(ContextKeys.PendingSentimentIntensity, intensityRaw);
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

        _context.SetContext(ContextKeys.PendingSentimentFollowUp, "true");
        _context.SetContext(ContextKeys.PendingSentimentIntensity, intensityRaw);

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

                if (_llmGenerator != null)
                {
                    var prompt = $"Define the word '{word}' in 1-2 concise sentences. Just the definition, no extra commentary.";
                    var llmDef = _llmGenerator(prompt);
                    if (!string.IsNullOrEmpty(llmDef))
                    {
                        _context.SetContext(ContextKeys.PendingDictionarySave, $"{word}|{llmDef}");
                        return $"{word}: {llmDef} Do you want me to remember that?";
                    }
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
                if (_llmGenerator != null)
                {
                    var prompt = $"The user previously said they {parts[0]} {parts[1]}, but now they {parts[2]} {parts[3]}. " +
                        $"Naturally ask about this apparent change of mind. Don't accuse, just be curious. 1 sentence.";
                    var llmResult = _llmGenerator(prompt);
                    if (!string.IsNullOrEmpty(llmResult))
                        return llmResult;
                }

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
                if (_llmGenerator != null)
                {
                    var prompt = $"The user's fact '{parts[0]}' might mean they also '{parts[1]}'. " +
                        $"Ask a natural question to check if this connection is correct. 1 sentence.";
                    var llmResult = _llmGenerator(prompt);
                    if (!string.IsNullOrEmpty(llmResult))
                        return llmResult;
                }

                return GetRandomResponse("inference_generalisation", parts[0], parts[1]);
            }

            return null;
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

    private string? HandleStoryRequest(string input, int? userId)
    {
        var lower = input.ToLowerInvariant().Trim();

        var isStoryRequest = lower.Contains("tell me a story") ||
                             lower.Contains("make up a story") ||
                             lower.Contains("tell me a tale") ||
                             lower.Contains("tell us a story") ||
                             lower.Contains("story time") ||
                             lower == "story" ||
                             lower.StartsWith("tell me another story");

        if (!isStoryRequest) return null;

        if (_llmGenerator != null)
        {
            var topic = !string.IsNullOrEmpty(_currentUserName)
                ? $"featuring {_currentUserName}" : "about a curious adventurer";
            var prompt = $"Tell a very short original story (3-5 sentences) {topic}. Make it fun and lighthearted. Just the story, no commentary.";
            var llmStory = _llmGenerator(prompt);
            if (!string.IsNullOrEmpty(llmStory))
                return GetRandomResponse("story_response", llmStory);
        }

        var story = _storyGenerator.GenerateStory(_currentUserName, userId);
        if (string.IsNullOrEmpty(story))
            return GetRandomResponse("story_response", "Once upon a time, there was a curious explorer who set out to discover new things. The end.");

        return GetRandomResponse("story_response", story);
    }

    private static readonly Regex ToolMarkerRegex = new(@"\{tool:(\w+)(?::([^}]+))?\}", RegexOptions.Compiled);

    private string ProcessToolMarkers(string response)
    {
        if (_toolRegistry == null)
            return ToolMarkerRegex.Replace(response, "");

        return ToolMarkerRegex.Replace(response, match =>
        {
            var toolName = match.Groups[1].Value;
            var argsRaw = match.Groups[2].Success ? match.Groups[2].Value : "";
            var args = string.IsNullOrEmpty(argsRaw) ? Array.Empty<string>() : new[] { argsRaw };

            var result = _toolRegistry.TryExecute(toolName, args);
            if (result == null || !result.Success)
            {
                if (result?.ErrorMessage == "timeout")
                    return GetRandomResponse("tool_timeout");
                return GetRandomResponse("tool_unavailable");
            }

            if (_llmGenerator != null && _summariseToolResults && !string.IsNullOrEmpty(result.Output))
            {
                var prompt = $"The user asked to use the tool '{toolName}'" +
                    (args.Length > 0 ? $" with query '{args[0]}'" : "") +
                    $". Summarise this result naturally and concisely in 1-3 sentences:\n{result.Output}";
                var summary = _llmGenerator(prompt);
                if (!string.IsNullOrEmpty(summary))
                    return summary;
            }

            return result.Output;
        });
    }

    private static readonly HashSet<string> PredictionTriggers = new(StringComparer.OrdinalIgnoreCase)
    {
        "magic 8 ball", "8 ball", "magic eight ball", "shake the ball",
        "predict", "my fortune", "tell my fortune"
    };

    private string? HandlePredictionRequest(string input)
    {
        var lower = input.ToLowerInvariant().Trim();

        foreach (var trigger in PredictionTriggers)
        {
            if (lower.Contains(trigger))
                return Get8BallResponse();
        }

        if (lower.EndsWith("?") && lower != "?")
            return Get8BallResponse();

        return null;
    }

    private string Get8BallResponse()
    {
        var preamble = Random.Shared.Next(2) == 0 ? "*shakes the magic 8 ball* " : "";
        var answer = GetRandomResponse("magic_8ball");
        return answer != null ? preamble + answer : preamble + "Ask again later.";
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

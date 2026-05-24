using System.Text.RegularExpressions;
using PokeChat.Core;
using PokeChat.Knowledge;
using PokeChat.Maths;
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
        var unknownWordsRaw = _context.GetContext(ContextKeys.UnknownWords);
        if (!string.IsNullOrEmpty(unknownWordsRaw))
        {
            var unknownWords = unknownWordsRaw
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Distinct()
                .ToList();

            _context.SetContext(ContextKeys.UnknownWords, null);

            if (unknownWords.Count > 0)
            {
                var word = unknownWords[0];
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

        var mathResult = _mathEngine.Evaluate(input);
        if (mathResult != null)
        {
            if (mathResult.StatedResult.HasValue)
            {
                if (Math.Abs(mathResult.Value - mathResult.StatedResult.Value) > 0.0001)
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

        var rule = ResponseRules.MatchRule(input, _knowledgeStore);

        if (rule != null && rule.Responses.Count > 0)
        {
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

    private string? HandleDictionaryQuery(string input)
    {
        var lower = input.ToLowerInvariant().Trim();

        var patterns = new (Regex Regex, int WordGroup)[]
        {
            (new Regex(@"^what (?:is|are|was|were) (?:a|an|the\s+)?(\w+)"), 1),
            (new Regex(@"^what does (\w+) mean"), 1),
            (new Regex(@"^what do (\w+) mean"), 1),
            (new Regex(@"^define (\w+)"), 1),
            (new Regex(@"^what is the (?:definition|meaning) of (?:a|an|the\s+)?(\w+)"), 1),
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

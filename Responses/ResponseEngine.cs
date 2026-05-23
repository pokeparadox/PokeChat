using PokeChat.Core;
using PokeChat.Knowledge;
using PokeChat.NLP;

namespace PokeChat.Responses;

public class ResponseEngine
{
    private readonly KnowledgeStore _knowledgeStore;
    private readonly ContextTracker _context;
    private readonly SpellChecker _spellChecker;
    private readonly IPosTagger _posTagger;
    private readonly ITokenizer _tokenizer;
    private readonly ISvoExtractor _svoExtractor;
    private readonly Dictionary<string, List<string>> _botResponses;

    public ResponseEngine(KnowledgeStore knowledgeStore, ContextTracker context, SpellChecker spellChecker, IPosTagger posTagger, ITokenizer tokenizer, ISvoExtractor svoExtractor)
    {
        _knowledgeStore = knowledgeStore;
        _context = context;
        _spellChecker = spellChecker;
        _posTagger = posTagger;
        _tokenizer = tokenizer;
        _svoExtractor = svoExtractor;
        _botResponses = knowledgeStore.GetBotResponses();
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

        var rule = ResponseRules.MatchRule(input, _knowledgeStore);

        if (rule != null && rule.Responses.Count > 0)
        {
            return rule.Responses[Random.Shared.Next(rule.Responses.Count)];
        }

        var tokens = _tokenizer.Tokenize(input);
        var correctedTokens = _spellChecker.AutoCorrect(tokens);
        var tags = _posTagger.Tag(correctedTokens);
        var triples = _svoExtractor.Extract(correctedTokens, tags);

        foreach (var triple in triples)
        {
            var existingFact = _knowledgeStore.GetFact(triple.Subject, triple.Verb, triple.Object);
            if (existingFact != null)
            {
                return GetRandomResponse("existing_fact", triple.Subject, triple.Verb, triple.Object);
            }
        }

        if (!string.IsNullOrEmpty(_context.LastSubject))
        {
            var subject = _context.LastSubject;

            if (!string.IsNullOrEmpty(_context.LastObject))
            {
                var obj = _context.LastObject;
                return GetRandomResponse("context_followup_with_object", subject, obj);
            }

            return GetRandomResponse("context_followup", subject);
        }

        var facts = userId.HasValue ? _knowledgeStore.GetFactsByUser(userId.Value) : new List<Fact>();
        if (facts.Count > 0 && Random.Shared.Next(3) == 0)
        {
            var randomFact = facts[Random.Shared.Next(facts.Count)];
            return GetRandomResponse("random_fact_followup", randomFact.Subject, randomFact.Verb, randomFact.Object);
        }

        return GetRandomResponse("default_response");
    }
}

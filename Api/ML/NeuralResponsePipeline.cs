using PokeChat.Knowledge;

namespace PokeChat.ML;

public class NeuralResponsePipeline
{
    private readonly ResponseTier _tier;
    private readonly NeuralResponseReranker? _reranker;
    private readonly NextWordPredictor? _predictor;
    private readonly NeuralConfig _config;
    private readonly string _modelBasePath;

    public ResponseTier Tier => _tier;
    public NeuralResponseReranker? Reranker => _reranker;
    public NextWordPredictor? Predictor => _predictor;

    public NeuralResponsePipeline(NeuralConfig config, string modelBasePath)
    {
        _config = config;
        _tier = config.ResponseTier;
        _modelBasePath = modelBasePath;

        if (config.RerankerEnabled)
        {
            _reranker = new NeuralResponseReranker();
            var rerankerPath = Path.Combine(modelBasePath, config.RerankerModel);
            _reranker.LoadModel(rerankerPath);
        }

        if (config.PredictorEnabled)
        {
            _predictor = new NextWordPredictor();
            var predictorPath = Path.Combine(modelBasePath, config.PredictorModelDir);
            _predictor.LoadModel(predictorPath);
        }
    }

    public NeuralResponsePipeline(ResponseTier tier, NeuralResponseReranker? reranker,
        NextWordPredictor? predictor, string modelBasePath)
    {
        _tier = tier;
        _reranker = reranker;
        _predictor = predictor;
        _modelBasePath = modelBasePath;
        _config = new NeuralConfig { ResponseTier = tier };
    }

    public string GetResponse(string category, List<string> candidates,
        ResponseContext context, Func<string, string?>? llmFallback = null)
    {
        if (candidates.Count == 0) return string.Empty;

        return _tier switch
        {
            ResponseTier.RulesOnly => PickRandom(candidates),
            ResponseTier.NeuralRerank => RerankOrRandom(candidates, context),
            ResponseTier.NeuralGenerate => RerankOrGenerate(candidates, context),
            ResponseTier.Llm => RerankOrLlm(candidates, context, llmFallback),
            _ => PickRandom(candidates)
        };
    }

    private string RerankOrRandom(List<string> candidates, ResponseContext context)
    {
        if (_reranker == null || !_reranker.IsTrained || candidates.Count < 2)
            return PickRandom(candidates);
        return _reranker.Rerank(candidates, context);
    }

    private string RerankOrGenerate(List<string> candidates, ResponseContext context)
    {
        var reranked = RerankOrRandom(candidates, context);

        if (_predictor != null && _predictor.IsTrained)
        {
            var generated = _predictor.Generate(null, context, _config.MaxResponseLength);
            if (!string.IsNullOrEmpty(generated) && generated.Split(' ').Length >= 3)
            {
                var rerankedScore = _reranker?.Score(reranked, context) ?? 0.5f;
                var generatedScore = _reranker?.Score(generated, context) ?? 0.5f;
                return generatedScore > rerankedScore ? generated : reranked;
            }
        }

        return reranked;
    }

    private string RerankOrLlm(List<string> candidates, ResponseContext context,
        Func<string, string?>? llmFallback)
    {
        var generated = RerankOrGenerate(candidates, context);

        if (llmFallback != null && !string.IsNullOrEmpty(context.UserInput))
        {
            var llmResult = llmFallback(context.UserInput);
            if (!string.IsNullOrEmpty(llmResult))
            {
                var genScore = _reranker?.Score(generated, context) ?? 0.5f;
                var llmScore = _reranker?.Score(llmResult, context) ?? 0.6f;
                return llmScore > genScore ? llmResult : generated;
            }
        }

        return generated;
    }

    public void TrainReranker(KnowledgeStore knowledgeStore)
    {
        if (_reranker == null) return;

        var trainingData = knowledgeStore.GetRerankerTrainingData();
        if (trainingData.Count >= 10)
            _reranker.Train(trainingData);

        _reranker.SaveModel(Path.Combine(_modelBasePath, _config.RerankerModel));
    }

    public void TrainPredictor(KnowledgeStore knowledgeStore)
    {
        if (_predictor == null) return;

        var responses = knowledgeStore.GetBotResponseTexts();
        if (responses.Count >= 5)
            _predictor.Train(responses);

        _predictor.SaveModel(Path.Combine(_modelBasePath, _config.PredictorModelDir));
    }

    private static string PickRandom(List<string> candidates) =>
        candidates[Random.Shared.Next(candidates.Count)];
}

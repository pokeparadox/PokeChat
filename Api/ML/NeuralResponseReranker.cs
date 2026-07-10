using PokeChat.Knowledge;

namespace PokeChat.ML;

public class NeuralResponseReranker
{
    private const int FeatureCount = 15;
    private const int HiddenSize = 32;
    private const int DefaultEpochs = 100;
    private const float DefaultLearningRate = 0.05f;
    private const float MinScoreThreshold = 0.5f;

    private SimpleNeuralNet? _net;
    private readonly Dictionary<string, int> _categoryIndices = new();
    private int _nextCategoryIndex;
    private bool _trained;

    private static readonly HashSet<string> VerbIndicators = new(StringComparer.OrdinalIgnoreCase)
        { "is", "are", "was", "were", "has", "have", "had", "do", "does", "did",
          "can", "could", "will", "would", "should", "may", "might", "shall",
          "likes", "loves", "wants", "needs", "knows", "thinks", "feels" };

    public bool IsTrained => _trained;

    public NeuralResponseReranker()
    {
        EnsureCategoryIndices();
    }

    private void EnsureCategoryIndices()
    {
        string[] categories = {
            "greeting", "default_response", "context_followup", "proactive_question",
            "existing_fact", "empathy_positive", "empathy_negative", "unknown_word",
            "math_result", "dictionary_result", "story_response", "poetry_response",
            "rule_match", "sentiment_acknowledgment", "recommender", "timeline_response"
        };
        foreach (var cat in categories)
            if (!_categoryIndices.ContainsKey(cat))
                _categoryIndices[cat] = _nextCategoryIndex++;
    }

    public float[] ExtractFeatures(string response, ResponseContext context)
    {
        var features = new float[FeatureCount];

        features[0] = _categoryIndices.TryGetValue(context.Category, out var catIdx)
            ? (float)catIdx / System.Math.Max(1, _categoryIndices.Count) : 0f;

        features[1] = context.CurrentIntent != null ? 0.8f : 0f;

        features[2] = context.SentimentScore;

        var wordCount = response.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        features[3] = wordCount <= 4 ? 0f : wordCount <= 14 ? 0.5f : 1f;

        if (!string.IsNullOrEmpty(context.PreviousResponse))
        {
            var overlap = WordOverlap(response, context.PreviousResponse);
            features[4] = overlap;
        }

        features[5] = HasVerb(response) ? 1f : 0f;

        var slotCount = CountSlots(response);
        var filledSlots = CountFilledSlots(response);
        features[6] = slotCount > 0 ? (float)filledSlots / slotCount : 1f;

        features[7] = WordDiversity(response);

        features[8] = !string.IsNullOrEmpty(context.UserName) &&
            response.Contains(context.UserName, StringComparison.OrdinalIgnoreCase) ? 1f : 0f;

        features[9] = response.Contains('?') ? 1f : 0f;

        features[10] = (float)context.CategoryFollowUpRate;

        features[11] = context.Category.StartsWith("context_followup") ||
            context.Category.StartsWith("proactive_") ? 1f : 0f;

        features[12] = System.Math.Min(1f, wordCount / 30f);

        features[13] = response.Contains("you ", StringComparison.OrdinalIgnoreCase) ||
            response.Contains("your ", StringComparison.OrdinalIgnoreCase) ? 0.5f : 0f;

        features[14] = 1f;

        return features;
    }

    public float Score(string response, ResponseContext context)
    {
        if (_net == null) return 0.5f;
        var features = ExtractFeatures(response, context);
        return _net.PredictScore(features);
    }

    public string Rerank(List<string> candidates, ResponseContext context)
    {
        if (_net == null || !_trained || candidates.Count < 2)
            return candidates[Random.Shared.Next(candidates.Count)];

        string best = candidates[0];
        float bestScore = Score(candidates[0], context);

        for (int i = 1; i < candidates.Count; i++)
        {
            var score = Score(candidates[i], context);
            if (score > bestScore)
            {
                bestScore = score;
                best = candidates[i];
            }
        }

        return bestScore >= MinScoreThreshold ? best : candidates[Random.Shared.Next(candidates.Count)];
    }

    public void Train(List<(string Response, ResponseContext Context, float Label)> examples,
        int epochs = DefaultEpochs, float learningRate = DefaultLearningRate)
    {
        if (examples.Count < 10) return;

        EnsureCategoryIndices();
        var trainingData = new List<(float[] Input, float Label)>();

        foreach (var (response, context, label) in examples)
        {
            var features = ExtractFeatures(response, context);
            trainingData.Add((features, label));
        }

        _net = new SimpleNeuralNet(FeatureCount, HiddenSize, 1);
        _net.TrainForScore(trainingData, epochs, learningRate);
        _trained = true;
    }

    public void SaveModel(string directory)
    {
        if (_net == null) return;
        var dir = Path.GetDirectoryName(directory);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        _net.Save(directory);
    }

    public bool LoadModel(string path)
    {
        _net = SimpleNeuralNet.Load(path);
        if (_net != null) { _trained = true; return true; }
        return false;
    }

    private static float WordOverlap(string a, string b)
    {
        var wordsA = new HashSet<string>(a.Split(' ', StringSplitOptions.RemoveEmptyEntries),
            StringComparer.OrdinalIgnoreCase);
        var wordsB = new HashSet<string>(b.Split(' ', StringSplitOptions.RemoveEmptyEntries),
            StringComparer.OrdinalIgnoreCase);
        if (wordsA.Count == 0 || wordsB.Count == 0) return 0f;
        var intersection = wordsA.Intersect(wordsB).Count();
        return (float)intersection / System.Math.Max(wordsA.Count, wordsB.Count);
    }

    private static bool HasVerb(string response)
    {
        var words = response.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return words.Any(w => VerbIndicators.Contains(w.Trim('.', '!', '?', ',')));
    }

    private static int CountSlots(string response)
    {
        int count = 0;
        for (int i = 0; i < response.Length - 1; i++)
            if (response[i] == '{' && char.IsDigit(response[i + 1]))
                count++;
        return count;
    }

    private static int CountFilledSlots(string response)
    {
        int count = 0;
        for (int i = 0; i < response.Length - 2; i++)
            if (response[i] == '{' && char.IsDigit(response[i + 1]) && response[i + 2] == '}')
                count++;
        return count;
    }

    private static float WordDiversity(string response)
    {
        var words = response.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length <= 1) return 0f;
        var unique = new HashSet<string>(words, StringComparer.OrdinalIgnoreCase);
        return (float)unique.Count / words.Length;
    }
}

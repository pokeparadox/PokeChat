using System.Text.Json;

namespace PokeChat.ML;

public class IntentClassifier
{
    private SimpleNeuralNet? _net;
    private Dictionary<string, int> _vocab = new();
    private string[] _categories = Array.Empty<string>();
    private int _vocabSize;
    private const int DefaultVocabSize = 2000;
    private const float ConfidenceThreshold = 0.85f;
    private const int HiddenSize = 64;
    private const int TrainingEpochs = 300;
    private const float LearningRate = 0.1f;
    private static readonly string? ModelPath;

    public bool IsReady => _net != null;

    static IntentClassifier()
    {
        try
        {
            var root = Data.ProjectPathHelper.FindProjectRoot(AppContext.BaseDirectory);
            ModelPath = root != null ? Path.Combine(root, "intent_model.bin") : null;
        }
        catch
        {
            ModelPath = null;
        }
    }

    public string? Classify(string input)
    {
        if (_net == null || _vocab.Count == 0) return null;
        var vec = Vectorise(input);
        var probs = _net.Predict(vec);
        var maxIdx = 0;
        var maxProb = probs[0];
        for (int i = 1; i < probs.Length; i++)
        {
            if (probs[i] > maxProb)
            {
                maxProb = probs[i];
                maxIdx = i;
            }
        }
        return maxProb >= ConfidenceThreshold && maxIdx < _categories.Length
            ? _categories[maxIdx]
            : null;
    }

    public float[] PredictProbabilities(string input)
    {
        if (_net == null || _vocab.Count == 0)
            return Array.Empty<float>();
        return _net.Predict(Vectorise(input));
    }

    public void Train(IReadOnlyList<(string Input, string Category)> examples)
    {
        if (examples.Count == 0) return;

        BuildVocab(examples.Select(e => e.Input));
        _categories = examples.Select(e => e.Category).Distinct().ToArray();
        if (_categories.Length == 0) return;

        var catIndex = new Dictionary<string, int>();
        for (int i = 0; i < _categories.Length; i++)
            catIndex[_categories[i]] = i;

        var vectors = new List<(float[] Input, int Label)>();
        foreach (var (input, category) in examples)
        {
            if (catIndex.TryGetValue(category, out var idx))
                vectors.Add((Vectorise(input), idx));
        }

        if (vectors.Count == 0) return;

        _net = new SimpleNeuralNet(_vocabSize, HiddenSize, _categories.Length);
        _net.Train(vectors, TrainingEpochs, LearningRate);
    }

    public void BuildVocab(IEnumerable<string> texts)
    {
        var freq = new Dictionary<string, int>();
        foreach (var text in texts)
        {
            var tokens = text.ToLowerInvariant()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var token in tokens)
            {
                if (token.Length > 0)
                    freq[token] = freq.GetValueOrDefault(token, 0) + 1;
            }
        }

        _vocab = freq.OrderByDescending(kv => kv.Value)
            .Take(DefaultVocabSize)
            .Select((kv, i) => (kv.Key, i))
            .ToDictionary(x => x.Key, x => x.i);
        _vocabSize = _vocab.Count;
    }

    public float[] Vectorise(string input)
    {
        if (_vocabSize == 0) return Array.Empty<float>();
        var vec = new float[_vocabSize];
        var tokens = input.ToLowerInvariant()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var token in tokens)
        {
            if (_vocab.TryGetValue(token, out var idx) && idx < _vocabSize)
                vec[idx] = 1.0f;
        }
        return vec;
    }

    public void LoadModel()
    {
        if (ModelPath == null) return;
        var loaded = SimpleNeuralNet.Load(ModelPath);
        if (loaded == null) return;

        var cfgPath = ModelPath + ".json";
        if (!File.Exists(cfgPath)) return;

        try
        {
            var json = File.ReadAllText(cfgPath);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var vocabArray = root.GetProperty("vocab").EnumerateArray()
                .Select(e => new { Word = e[0].GetString()!, Index = e[1].GetInt32() })
                .OrderBy(x => x.Index)
                .ToList();

            _vocab = vocabArray.ToDictionary(x => x.Word, x => x.Index);
            _vocabSize = _vocab.Count;

            var catsArray = root.GetProperty("categories").EnumerateArray()
                .Select(e => e.GetString()!)
                .ToArray();
            _categories = catsArray;

            _net = loaded;
        }
        catch
        {
            _net = null;
        }
    }

    public void SaveModel()
    {
        if (_net == null || ModelPath == null) return;
        try
        {
            _net.Save(ModelPath);

            var cfg = new Dictionary<string, object>
            {
                ["vocab"] = _vocab.OrderBy(kv => kv.Value)
                    .Select(kv => new object[] { kv.Key, kv.Value })
                    .ToList(),
                ["categories"] = _categories.ToList()
            };
            var json = JsonSerializer.Serialize(cfg);
            File.WriteAllText(ModelPath + ".json", json);
        }
        catch
        {
        }
    }

    public void LoadOrCreate(IReadOnlyList<(string Input, string Category)>? seedExamples = null)
    {
        LoadModel();
        if (_net != null) return;

        if (seedExamples != null && seedExamples.Count > 0)
        {
            Train(seedExamples);
            SaveModel();
        }
    }

    public string[] GetCategories() => _categories;
    public int VocabSize => _vocabSize;
}

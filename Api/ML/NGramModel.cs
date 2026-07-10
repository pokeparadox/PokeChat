namespace PokeChat.ML;

public class NGramModel
{
    private readonly Dictionary<(int, int, int), Dictionary<int, int>> _trigrams = new();
    private readonly Dictionary<(int, int), Dictionary<int, int>> _bigrams = new();
    private readonly Dictionary<int, int> _unigrams = new();
    private int _totalUnigrams;

    public int TrigramCount => _trigrams.Count;
    public int BigramCount => _bigrams.Count;
    public int UnigramCount => _unigrams.Count;

    public void Train(int[] tokenIds)
    {
        for (int i = 0; i < tokenIds.Length; i++)
        {
            _unigrams.TryGetValue(tokenIds[i], out var uc);
            _unigrams[tokenIds[i]] = uc + 1;
            _totalUnigrams++;

            if (i >= 1)
            {
                var bigramKey = (tokenIds[i - 1], tokenIds[i]);
                if (!_bigrams.ContainsKey(bigramKey))
                    _bigrams[bigramKey] = new Dictionary<int, int>();
                _bigrams[bigramKey].TryGetValue(tokenIds[i], out var bc);
                _bigrams[bigramKey][tokenIds[i]] = bc + 1;
            }

            if (i >= 2)
            {
                var trigramKey = (tokenIds[i - 2], tokenIds[i - 1], tokenIds[i]);
                if (!_trigrams.ContainsKey(trigramKey))
                    _trigrams[trigramKey] = new Dictionary<int, int>();
                _trigrams[trigramKey].TryGetValue(tokenIds[i], out var tc);
                _trigrams[trigramKey][tokenIds[i]] = tc + 1;
            }
        }
    }

    public List<(int WordId, float Probability)> GetCandidates(int[] context, int topK = 10)
    {
        var scores = new Dictionary<int, float>();

        if (context.Length >= 2)
        {
            foreach (var kv in _trigrams)
            {
                if (kv.Key.Item1 == context[^2] && kv.Key.Item2 == context[^1])
                {
                    var total = kv.Value.Values.Sum();
                    foreach (var word in kv.Value)
                        scores[word.Key] = (float)word.Value / total;
                }
            }
        }

        if (context.Length >= 1 && scores.Count == 0)
        {
            foreach (var kv in _bigrams)
            {
                if (kv.Key.Item1 == context[^1])
                {
                    var total = kv.Value.Values.Sum();
                    foreach (var word in kv.Value)
                    {
                        scores.TryGetValue(word.Key, out var existing);
                        scores[word.Key] = existing * 0.6f + ((float)word.Value / total) * 0.4f;
                    }
                }
            }
        }

        if (scores.Count == 0 && _totalUnigrams > 0)
        {
            foreach (var kv in _unigrams)
                scores[kv.Key] = (float)kv.Value / _totalUnigrams;
        }

        return scores
            .OrderByDescending(kv => kv.Value)
            .Take(topK)
            .Select(kv => (kv.Key, kv.Value))
            .ToList();
    }

    public void Save(string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        using var writer = new BinaryWriter(File.OpenWrite(path));

        writer.Write(_unigrams.Count);
        foreach (var kv in _unigrams)
        {
            writer.Write(kv.Key);
            writer.Write(kv.Value);
        }

        writer.Write(_bigrams.Count);
        foreach (var kv in _bigrams)
        {
            writer.Write(kv.Key.Item1);
            writer.Write(kv.Key.Item2);
            writer.Write(kv.Value.Count);
            foreach (var kv2 in kv.Value)
            {
                writer.Write(kv2.Key);
                writer.Write(kv2.Value);
            }
        }

        writer.Write(_trigrams.Count);
        foreach (var kv in _trigrams)
        {
            writer.Write(kv.Key.Item1);
            writer.Write(kv.Key.Item2);
            writer.Write(kv.Key.Item3);
            writer.Write(kv.Value.Count);
            foreach (var kv2 in kv.Value)
            {
                writer.Write(kv2.Key);
                writer.Write(kv2.Value);
            }
        }
    }

    public static NGramModel? Load(string path)
    {
        if (!File.Exists(path)) return null;
        try
        {
            using var reader = new BinaryReader(File.OpenRead(path));
            var model = new NGramModel();

            var uniCount = reader.ReadInt32();
            for (int i = 0; i < uniCount; i++)
            {
                var wordId = reader.ReadInt32();
                var count = reader.ReadInt32();
                model._unigrams[wordId] = count;
                model._totalUnigrams += count;
            }

            var biCount = reader.ReadInt32();
            for (int i = 0; i < biCount; i++)
            {
                var k1 = reader.ReadInt32();
                var k2 = reader.ReadInt32();
                var innerCount = reader.ReadInt32();
                var inner = new Dictionary<int, int>();
                for (int j = 0; j < innerCount; j++)
                {
                    var wk = reader.ReadInt32();
                    var wc = reader.ReadInt32();
                    inner[wk] = wc;
                }
                model._bigrams[(k1, k2)] = inner;
            }

            var triCount = reader.ReadInt32();
            for (int i = 0; i < triCount; i++)
            {
                var k1 = reader.ReadInt32();
                var k2 = reader.ReadInt32();
                var k3 = reader.ReadInt32();
                var innerCount = reader.ReadInt32();
                var inner = new Dictionary<int, int>();
                for (int j = 0; j < innerCount; j++)
                {
                    var wk = reader.ReadInt32();
                    var wc = reader.ReadInt32();
                    inner[wk] = wc;
                }
                model._trigrams[(k1, k2, k3)] = inner;
            }

            return model;
        }
        catch
        {
            return null;
        }
    }
}

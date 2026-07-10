namespace PokeChat.ML;

public class NextWordPredictor
{
    private const int HiddenSize = 32;
    private const int DefaultEpochs = 50;
    private const float DefaultLearningRate = 0.05f;
    private const float InterpolationAlpha = 0.6f;
    private const int RepetitionWindow = 3;

    private WordVocab? _vocab;
    private NGramModel? _ngramModel;
    private SimpleNeuralNet? _neuralSmoother;
    private bool _trained;

    public bool IsTrained => _trained;
    public WordVocab? Vocab => _vocab;

    public void Train(List<string> responses, int vocabSize = WordVocab.DefaultMaxSize)
    {
        if (responses.Count < 5) return;

        _vocab = new WordVocab();
        foreach (var response in responses)
        {
            var words = response.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var word in words)
                _vocab.CountWord(word);
        }
        _vocab.BuildFromCounts(vocabSize);

        _ngramModel = new NGramModel();
        foreach (var response in responses)
        {
            var tokenIds = _vocab.Tokenise(response);
            _ngramModel.Train(tokenIds);
        }

        TrainNeuralSmoother(responses);
        _trained = true;
    }

    private void TrainNeuralSmoother(List<string> responses)
    {
        if (_vocab == null || _ngramModel == null) return;

        var trainingData = new List<(float[] Input, float Label)>();
        foreach (var response in responses)
        {
            var tokenIds = _vocab.Tokenise(response);
            for (int i = 1; i < tokenIds.Length; i++)
            {
                var context = tokenIds[..i];
                var target = tokenIds[i];
                var input = BuildSmootherInput(context, target);
                trainingData.Add((input, 1.0f));

                var negTarget = Random.Shared.Next(_vocab.Size);
                if (negTarget != target)
                {
                    var negInput = BuildSmootherInput(context, negTarget);
                    trainingData.Add((negInput, 0.0f));
                }
            }
        }

        if (trainingData.Count > 0)
        {
            _neuralSmoother = new SimpleNeuralNet(_vocab.Size + 1, HiddenSize, 1);
            _neuralSmoother.TrainForScore(trainingData, DefaultEpochs, DefaultLearningRate);
        }
    }

    private float[] BuildSmootherInput(int[] context, int candidateWord)
    {
        if (_vocab == null) return Array.Empty<float>();

        var input = new float[_vocab.Size + 1];
        if (context.Length > 0)
            input[context[^1] % _vocab.Size] = 1f;
        input[_vocab.Size] = (float)candidateWord / _vocab.Size;
        return input;
    }

    public string Generate(string? seed, ResponseContext context, int maxLength = 20)
    {
        if (_vocab == null || _ngramModel == null) return string.Empty;

        var tokenIds = string.IsNullOrEmpty(seed)
            ? new[] { _vocab.GetIndex(WordVocab.BosToken) }
            : _vocab.Tokenise(seed);

        var generated = new List<int>(tokenIds);
        var recentWords = new HashSet<int>();

        for (int step = 0; step < maxLength; step++)
        {
            var hasWords = generated.Skip(1).Any(t => _vocab.GetWord(t) != WordVocab.BosToken && _vocab.GetWord(t) != WordVocab.EosToken);
            var candidates = _ngramModel.GetCandidates(generated.ToArray(), 20)
                .Where(c => _vocab.GetWord(c.WordId) != WordVocab.BosToken
                         && (hasWords || _vocab.GetWord(c.WordId) != WordVocab.EosToken))
                .ToList();
            if (candidates.Count == 0) break;

            var scored = ApplySmoothing(candidates, generated.ToArray());
            ApplyRepetitionPenalty(scored, recentWords);

            var nextToken = SampleFromScored(scored);
            if (_vocab.GetWord(nextToken) == WordVocab.EosToken) break;

            generated.Add(nextToken);
            recentWords.Add(nextToken);
            if (recentWords.Count > RepetitionWindow)
                recentWords.Remove(recentWords.First());
        }

        return _vocab.Detokenise(generated.Skip(1).ToArray());
    }

    public string GenerateBeamSearch(string? seed, ResponseContext context,
        int beamWidth = 3, int maxLength = 20)
    {
        if (_vocab == null || _ngramModel == null) return string.Empty;

        var startTokens = string.IsNullOrEmpty(seed)
            ? new[] { _vocab.GetIndex(WordVocab.BosToken) }
            : _vocab.Tokenise(seed);

        var beams = new List<(int[] Tokens, float Score)>
        {
            (startTokens, 0f)
        };

        for (int step = 0; step < maxLength; step++)
        {
            var newBeams = new List<(int[] Tokens, float Score)>();

            foreach (var (tokens, score) in beams)
            {
                if (tokens.Length > 0 && _vocab.GetWord(tokens[^1]) == WordVocab.EosToken)
                {
                    newBeams.Add((tokens, score));
                    continue;
                }

                var hasWords = tokens.Skip(1).Any(t => _vocab.GetWord(t) != WordVocab.BosToken && _vocab.GetWord(t) != WordVocab.EosToken);
                var candidates = _ngramModel.GetCandidates(tokens, 10)
                    .Where(c => _vocab.GetWord(c.WordId) != WordVocab.BosToken
                             && (hasWords || _vocab.GetWord(c.WordId) != WordVocab.EosToken))
                    .ToList();
                if (candidates.Count == 0) continue;

                var scored = ApplySmoothing(candidates, tokens);
                ApplyRepetitionPenalty(scored, new HashSet<int>(tokens.Skip(System.Math.Max(0, tokens.Length - RepetitionWindow))));

                foreach (var (wordId, prob) in scored.Take(beamWidth))
                {
                    var newTokens = new int[tokens.Length + 1];
                    Array.Copy(tokens, newTokens, tokens.Length);
                    newTokens[^1] = wordId;
                    newBeams.Add((newTokens, score + (float)System.Math.Log(prob + 1e-10f)));
                }
            }

            beams = newBeams
                .OrderByDescending(b => b.Score)
                .Take(beamWidth)
                .ToList();

            if (beams.Count == 0) break;
        }

        if (beams.Count == 0) return string.Empty;
        var best = beams.OrderByDescending(b => b.Score).First();
        return _vocab.Detokenise(best.Tokens.Skip(1).ToArray());
    }

    private List<(int WordId, float Probability)> ApplySmoothing(
        List<(int WordId, float Probability)> candidates, int[] context)
    {
        if (_neuralSmoother == null || _vocab == null)
            return candidates;

        return candidates.Select(c =>
        {
            var input = BuildSmootherInput(context, c.WordId);
            var neuralScore = _neuralSmoother.PredictScore(input);
            var blended = InterpolationAlpha * neuralScore + (1 - InterpolationAlpha) * c.Probability;
            return (c.WordId, Probability: blended);
        })
        .OrderByDescending(c => c.Probability)
        .ToList();
    }

    private static void ApplyRepetitionPenalty(
        List<(int WordId, float Probability)> scored, HashSet<int> recentWords)
    {
        for (int i = 0; i < scored.Count; i++)
        {
            if (recentWords.Contains(scored[i].WordId))
                scored[i] = (scored[i].WordId, scored[i].Probability * 0.3f);
        }
    }

    private static int SampleFromScored(List<(int WordId, float Probability)> scored)
    {
        if (scored.Count == 0) return 0;

        var total = scored.Sum(s => s.Probability);
        if (total <= 0) return scored[0].WordId;

        var r = (float)(Random.Shared.NextDouble() * total);
        float cumulative = 0;
        foreach (var (wordId, prob) in scored)
        {
            cumulative += prob;
            if (r <= cumulative) return wordId;
        }
        return scored[^1].WordId;
    }

    public void SaveModel(string directory)
    {
        var dir = Path.GetDirectoryName(directory);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        _vocab?.Save(Path.Combine(directory, "vocab.bin"));
        _ngramModel?.Save(Path.Combine(directory, "ngram.bin"));
        _neuralSmoother?.Save(Path.Combine(directory, "smoother.bin"));
    }

    public bool LoadModel(string directory)
    {
        _vocab = WordVocab.Load(Path.Combine(directory, "vocab.bin"));
        _ngramModel = NGramModel.Load(Path.Combine(directory, "ngram.bin"));
        _neuralSmoother = SimpleNeuralNet.Load(Path.Combine(directory, "smoother.bin"));

        if (_vocab != null && _ngramModel != null)
        {
            _trained = true;
            return true;
        }
        return false;
    }
}

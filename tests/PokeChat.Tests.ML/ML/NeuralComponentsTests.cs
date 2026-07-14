using PokeChat.ML;
using Shouldly;

namespace PokeChat.Tests.ML;

public class WordVocabTests
{
    [Fact]
    public void Constructor_ContainsSpecialTokens()
    {
        var vocab = new WordVocab();
        vocab.GetIndex(WordVocab.BosToken).ShouldBe(0);
        vocab.GetIndex(WordVocab.EosToken).ShouldBe(1);
        vocab.GetIndex(WordVocab.UnkToken).ShouldBe(2);
        vocab.Size.ShouldBe(3);
    }

    [Fact]
    public void AddToken_ReturnsConsistentIndex()
    {
        var vocab = new WordVocab();
        var idx1 = vocab.AddToken("hello");
        var idx2 = vocab.AddToken("hello");
        idx1.ShouldBe(idx2);
        vocab.Size.ShouldBe(4);
    }

    [Fact]
    public void GetIndex_UnknownWord_ReturnsUnkIndex()
    {
        var vocab = new WordVocab();
        var unkIdx = vocab.GetIndex(WordVocab.UnkToken);
        vocab.GetIndex("nonexistent").ShouldBe(unkIdx);
    }

    [Fact]
    public void Tokenise_ProducesIndices()
    {
        var vocab = new WordVocab();
        vocab.AddToken("hello");
        vocab.AddToken("world");
        var ids = vocab.Tokenise("hello world");
        ids.Length.ShouldBe(3);
        ids[0].ShouldBe(vocab.GetIndex("hello"));
        ids[1].ShouldBe(vocab.GetIndex("world"));
        ids[2].ShouldBe(vocab.GetIndex(WordVocab.EosToken));
    }

    [Fact]
    public void Detokenise_ReconstructsText()
    {
        var vocab = new WordVocab();
        vocab.AddToken("hello");
        vocab.AddToken("world");
        var ids = vocab.Tokenise("hello world");
        var text = vocab.Detokenise(ids);
        text.ShouldBe("hello world");
    }

    [Fact]
    public void SaveAndLoad_RoundTrip()
    {
        var dir = Path.Combine(Path.GetTempPath(), "vocab_test_" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var vocab = new WordVocab();
            vocab.AddToken("hello");
            vocab.AddToken("world");
            vocab.AddToken("test");

            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "vocab.bin");
            vocab.Save(path);

            var loaded = WordVocab.Load(path);
            loaded.ShouldNotBeNull();
            loaded.Size.ShouldBe(vocab.Size);
            loaded.GetIndex("hello").ShouldBe(vocab.GetIndex("hello"));
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }
}

public class NGramModelTests
{
    [Fact]
    public void Train_BuildsUnigrams()
    {
        var model = new NGramModel();
        model.Train(new[] { 1, 2, 3, 4, 5 });
        model.UnigramCount.ShouldBe(5);
    }

    [Fact]
    public void Train_BuildsBigrams()
    {
        var model = new NGramModel();
        model.Train(new[] { 1, 2, 3 });
        model.BigramCount.ShouldBe(2);
    }

    [Fact]
    public void Train_BuildsTrigrams()
    {
        var model = new NGramModel();
        model.Train(new[] { 1, 2, 3, 4 });
        model.TrigramCount.ShouldBe(2);
    }

    [Fact]
    public void GetCandidates_ReturnsTopK()
    {
        var model = new NGramModel();
        model.Train(new[] { 1, 2, 3, 1, 2, 4, 1, 2, 5 });
        var candidates = model.GetCandidates(new[] { 1, 2 }, topK: 3);
        candidates.Count.ShouldBeLessThanOrEqualTo(3);
        candidates.Count.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void GetCandidates_WithNoMatch_FallsBackToUnigrams()
    {
        var model = new NGramModel();
        model.Train(new[] { 1, 2, 3 });
        var candidates = model.GetCandidates(new[] { 99, 98 });
        candidates.Count.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void SaveAndLoad_RoundTrip()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ngram_test_" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var model = new NGramModel();
            model.Train(new[] { 1, 2, 3, 4, 5 });

            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "ngram.bin");
            model.Save(path);

            var loaded = NGramModel.Load(path);
            loaded.ShouldNotBeNull();
            loaded.UnigramCount.ShouldBe(model.UnigramCount);
            loaded.BigramCount.ShouldBe(model.BigramCount);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }
}

public class NextWordPredictorTests
{
    [Fact]
    public void IsTrained_FalseByDefault()
    {
        var predictor = new NextWordPredictor();
        predictor.IsTrained.ShouldBeFalse();
    }

    [Fact]
    public void Train_WithEnoughData_SetsTrained()
    {
        var predictor = new NextWordPredictor();
        var responses = new List<string>
        {
            "hello there",
            "how are you",
            "nice to meet you",
            "good morning",
            "see you later",
        };
        predictor.Train(responses);
        predictor.IsTrained.ShouldBeTrue();
    }

    [Fact]
    public void Train_WithFewerThan5Responses_DoesNotTrain()
    {
        var predictor = new NextWordPredictor();
        predictor.Train(new List<string> { "hi", "hello" });
        predictor.IsTrained.ShouldBeFalse();
    }

    [Fact]
    public void Generate_WhenTrained_ReturnsNonEmpty()
    {
        var predictor = new NextWordPredictor();
        var responses = new List<string>
        {
            "hello there friend",
            "how are you today",
            "nice to meet you",
            "good morning world",
            "see you later",
            "hello there",
            "how are you",
            "nice day",
        };
        predictor.Train(responses);
        var result = predictor.Generate(null, new ResponseContext { Category = "greeting" });
        result.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void GenerateBeamSearch_WhenTrained_ReturnsNonEmpty()
    {
        var predictor = new NextWordPredictor();
        var responses = new List<string>
        {
            "hello there friend",
            "how are you today",
            "nice to meet you",
            "good morning world",
            "see you later",
            "hello there",
            "how are you",
            "nice day",
        };
        predictor.Train(responses);
        var result = predictor.GenerateBeamSearch(null, new ResponseContext { Category = "greeting" });
        result.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void SaveAndLoad_RoundTrip()
    {
        var dir = Path.Combine(Path.GetTempPath(), "predictor_test_" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var predictor = new NextWordPredictor();
            var responses = new List<string>
            {
                "hello there friend", "how are you today", "nice to meet you",
                "good morning world", "see you later", "hello there",
                "how are you", "nice day",
            };
            predictor.Train(responses);

            Directory.CreateDirectory(dir);
            predictor.SaveModel(dir);

            var loaded = new NextWordPredictor();
            loaded.LoadModel(dir);
            loaded.IsTrained.ShouldBeTrue();
            loaded.Vocab.ShouldNotBeNull();
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }
}

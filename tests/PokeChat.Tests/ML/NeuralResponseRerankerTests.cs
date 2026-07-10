using PokeChat.ML;
using Shouldly;

namespace PokeChat.Tests.ML;

public class NeuralResponseRerankerTests
{
    [Fact]
    public void ExtractFeatures_ReturnsCorrectLength()
    {
        var reranker = new NeuralResponseReranker();
        var context = new ResponseContext { Category = "greeting" };
        var features = reranker.ExtractFeatures("Hello there!", context);
        features.Length.ShouldBe(15);
    }

    [Fact]
    public void Score_ReturnsDefaultWhenNotTrained()
    {
        var reranker = new NeuralResponseReranker();
        var context = new ResponseContext { Category = "greeting" };
        var score = reranker.Score("Hello!", context);
        score.ShouldBe(0.5f);
    }

    [Fact]
    public void Rerank_ReturnsRandomWhenNotTrained()
    {
        var reranker = new NeuralResponseReranker();
        var context = new ResponseContext { Category = "greeting" };
        var candidates = new List<string> { "Hi!", "Hello!", "Hey!" };
        var result = reranker.Rerank(candidates, context);
        candidates.ShouldContain(result);
    }

    [Fact]
    public void Rerank_ReturnsCandidateFromList()
    {
        var reranker = new NeuralResponseReranker();
        var context = new ResponseContext { Category = "greeting" };
        var candidates = new List<string> { "Hi!", "Hello!", "Hey!" };
        var result = reranker.Rerank(candidates, context);
        candidates.ShouldContain(result);
    }

    [Fact]
    public void Train_WithEnoughExamples_ProducesTrainedModel()
    {
        var reranker = new NeuralResponseReranker();
        var examples = new List<(string Response, ResponseContext Context, float Label)>();

        for (int i = 0; i < 20; i++)
        {
            examples.Add(("Great response!", new ResponseContext { Category = "greeting" }, 1.0f));
            examples.Add(("Bad response", new ResponseContext { Category = "default_response" }, 0.0f));
        }

        reranker.Train(examples);
        reranker.IsTrained.ShouldBeTrue();
    }

    [Fact]
    public void Train_WithFewerThan10Examples_DoesNotTrain()
    {
        var reranker = new NeuralResponseReranker();
        var examples = new List<(string Response, ResponseContext Context, float Label)>
        {
            ("Hi", new ResponseContext { Category = "greeting" }, 1.0f),
        };
        reranker.Train(examples);
        reranker.IsTrained.ShouldBeFalse();
    }

    [Fact]
    public void TrainedModel_ScoresHigherForPositiveExamples()
    {
        var reranker = new NeuralResponseReranker();
        var examples = new List<(string Response, ResponseContext Context, float Label)>();

        for (int i = 0; i < 30; i++)
        {
            examples.Add(("Nice to meet you!", new ResponseContext { Category = "greeting" }, 1.0f));
            examples.Add(("whatever", new ResponseContext { Category = "default_response" }, 0.0f));
        }

        reranker.Train(examples, epochs: 200);
        var goodScore = reranker.Score("Nice to meet you!", new ResponseContext { Category = "greeting" });
        var badScore = reranker.Score("whatever", new ResponseContext { Category = "default_response" });
        goodScore.ShouldBeGreaterThan(badScore);
    }

    [Fact]
    public void SaveAndLoad_RoundTrip()
    {
        var dir = Path.Combine(Path.GetTempPath(), "reranker_test_" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var reranker = new NeuralResponseReranker();
            var examples = new List<(string Response, ResponseContext Context, float Label)>
            {
                ("Hello!", new ResponseContext { Category = "greeting" }, 1.0f),
                ("Hello!", new ResponseContext { Category = "greeting" }, 1.0f),
                ("Hello!", new ResponseContext { Category = "greeting" }, 1.0f),
                ("Hello!", new ResponseContext { Category = "greeting" }, 1.0f),
                ("Hello!", new ResponseContext { Category = "greeting" }, 1.0f),
                ("Bad", new ResponseContext { Category = "default_response" }, 0.0f),
                ("Bad", new ResponseContext { Category = "default_response" }, 0.0f),
                ("Bad", new ResponseContext { Category = "default_response" }, 0.0f),
                ("Bad", new ResponseContext { Category = "default_response" }, 0.0f),
                ("Bad", new ResponseContext { Category = "default_response" }, 0.0f),
                ("Good", new ResponseContext { Category = "greeting" }, 1.0f),
                ("Good", new ResponseContext { Category = "greeting" }, 1.0f),
                ("Good", new ResponseContext { Category = "greeting" }, 1.0f),
                ("Good", new ResponseContext { Category = "greeting" }, 1.0f),
                ("Good", new ResponseContext { Category = "greeting" }, 1.0f),
            };
            reranker.Train(examples);
            reranker.IsTrained.ShouldBeTrue();

            Directory.CreateDirectory(dir);
            reranker.SaveModel(Path.Combine(dir, "reranker_model.bin"));

            var loaded = new NeuralResponseReranker();
            loaded.LoadModel(Path.Combine(dir, "reranker_model.bin"));
            loaded.IsTrained.ShouldBeTrue();

            var ctx = new ResponseContext { Category = "greeting" };
            var originalScore = reranker.Score("Hello!", ctx);
            var loadedScore = loaded.Score("Hello!", ctx);
            loadedScore.ShouldBe(originalScore, 0.001f);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }
}

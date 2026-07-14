using PokeChat.ML;
using Shouldly;

namespace PokeChat.Tests.ML;

public class NeuralResponsePipelineTests
{
    [Fact]
    public void Tier_DefaultsToNeuralRerank()
    {
        var config = new NeuralConfig();
        config.ResponseTier.ShouldBe(ResponseTier.NeuralRerank);
    }

    [Fact]
    public void GetResponse_RulesOnly_PicksRandom()
    {
        var pipeline = new NeuralResponsePipeline(ResponseTier.RulesOnly, null, null, "/tmp");
        var candidates = new List<string> { "Hi!", "Hello!", "Hey!" };
        var result = pipeline.GetResponse("greeting", candidates, new ResponseContext { Category = "greeting" });
        candidates.ShouldContain(result);
    }

    [Fact]
    public void GetResponse_NeuralRerank_WithoutModel_PicksRandom()
    {
        var pipeline = new NeuralResponsePipeline(ResponseTier.NeuralRerank, null, null, "/tmp");
        var candidates = new List<string> { "Hi!", "Hello!", "Hey!" };
        var result = pipeline.GetResponse("greeting", candidates, new ResponseContext { Category = "greeting" });
        candidates.ShouldContain(result);
    }

    [Fact]
    public void GetResponse_EmptyCandidates_ReturnsEmpty()
    {
        var pipeline = new NeuralResponsePipeline(ResponseTier.RulesOnly, null, null, "/tmp");
        var result = pipeline.GetResponse("greeting", new List<string>(), new ResponseContext { Category = "greeting" });
        result.ShouldBeEmpty();
    }

    [Fact]
    public void GetResponse_SingleCandidate_ReturnsIt()
    {
        var pipeline = new NeuralResponsePipeline(ResponseTier.NeuralRerank, null, null, "/tmp");
        var result = pipeline.GetResponse("greeting", new List<string> { "Hello!" }, new ResponseContext { Category = "greeting" });
        result.ShouldBe("Hello!");
    }

    [Fact]
    public void GetResponse_NeuralGenerate_WithoutPredictor_FallsBackToRerank()
    {
        var pipeline = new NeuralResponsePipeline(ResponseTier.NeuralGenerate, null, null, "/tmp");
        var candidates = new List<string> { "Hi!", "Hello!" };
        var result = pipeline.GetResponse("greeting", candidates, new ResponseContext { Category = "greeting" });
        candidates.ShouldContain(result);
    }

    [Fact]
    public void NeuralConfig_Load_FromFile()
    {
        var dir = Path.Combine(Path.GetTempPath(), "config_test_" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "response.json");
            File.WriteAllText(path, """
            {
              "responseTier": "NeuralGenerate",
              "neural": {
                "rerankerModel": "test.bin",
                "beamWidth": 5,
                "maxResponseLength": 30,
                "rerankerEnabled": false,
                "predictorEnabled": true
              }
            }
            """);

            var config = NeuralConfig.Load(path);
            config.ResponseTier.ShouldBe(ResponseTier.NeuralGenerate);
            config.RerankerModel.ShouldBe("test.bin");
            config.BeamWidth.ShouldBe(5);
            config.MaxResponseLength.ShouldBe(30);
            config.RerankerEnabled.ShouldBeFalse();
            config.PredictorEnabled.ShouldBeTrue();
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void NeuralConfig_Load_NonexistentFile_ReturnsDefaults()
    {
        var config = NeuralConfig.Load("/nonexistent/path.json");
        config.ResponseTier.ShouldBe(ResponseTier.NeuralRerank);
        config.BeamWidth.ShouldBe(3);
    }

    [Fact]
    public void Pipeline_WithTrainedReranker_UsesReranker()
    {
        var reranker = new NeuralResponseReranker();
        var examples = new List<(string Response, ResponseContext Context, float Label)>
        {
            ("Great!", new ResponseContext { Category = "greeting" }, 1.0f),
            ("Great!", new ResponseContext { Category = "greeting" }, 1.0f),
            ("Great!", new ResponseContext { Category = "greeting" }, 1.0f),
            ("Great!", new ResponseContext { Category = "greeting" }, 1.0f),
            ("Great!", new ResponseContext { Category = "greeting" }, 1.0f),
            ("Meh", new ResponseContext { Category = "default_response" }, 0.0f),
            ("Meh", new ResponseContext { Category = "default_response" }, 0.0f),
            ("Meh", new ResponseContext { Category = "default_response" }, 0.0f),
            ("Meh", new ResponseContext { Category = "default_response" }, 0.0f),
            ("Meh", new ResponseContext { Category = "default_response" }, 0.0f),
            ("Awesome", new ResponseContext { Category = "greeting" }, 1.0f),
            ("Awesome", new ResponseContext { Category = "greeting" }, 1.0f),
            ("Awesome", new ResponseContext { Category = "greeting" }, 1.0f),
            ("Awesome", new ResponseContext { Category = "greeting" }, 1.0f),
            ("Awesome", new ResponseContext { Category = "greeting" }, 1.0f),
        };
        reranker.Train(examples);

        var pipeline = new NeuralResponsePipeline(ResponseTier.NeuralRerank, reranker, null, "/tmp");
        var candidates = new List<string> { "Great!", "Meh", "Awesome" };
        var result = pipeline.GetResponse("greeting", candidates, new ResponseContext { Category = "greeting" });
        result.ShouldBe("Great!");
    }
}

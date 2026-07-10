namespace PokeChat.ML;

public enum ResponseTier
{
    RulesOnly,
    NeuralRerank,
    NeuralGenerate,
    Llm
}

public class NeuralConfig
{
    public ResponseTier ResponseTier { get; set; } = ResponseTier.NeuralRerank;
    public string RerankerModel { get; set; } = "reranker_model.bin";
    public string PredictorModelDir { get; set; } = "predictor_model";
    public int BeamWidth { get; set; } = 3;
    public int MaxResponseLength { get; set; } = 20;
    public bool RerankerEnabled { get; set; } = true;
    public bool PredictorEnabled { get; set; } = false;
    public int AutoRetrainAfterConversations { get; set; } = 50;

    public static NeuralConfig Load(string path)
    {
        if (!File.Exists(path)) return new NeuralConfig();
        try
        {
            var json = File.ReadAllText(path);
            var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;
            var config = new NeuralConfig();

            if (root.TryGetProperty("responseTier", out var tier))
                config.ResponseTier = Enum.Parse<ResponseTier>(tier.GetString() ?? "NeuralRerank", true);
            if (root.TryGetProperty("neural", out var neural))
            {
                if (neural.TryGetProperty("rerankerModel", out var rm))
                    config.RerankerModel = rm.GetString() ?? config.RerankerModel;
                if (neural.TryGetProperty("predictorModel", out var pm))
                    config.PredictorModelDir = pm.GetString() ?? config.PredictorModelDir;
                if (neural.TryGetProperty("beamWidth", out var bw))
                    config.BeamWidth = bw.GetInt32();
                if (neural.TryGetProperty("maxResponseLength", out var ml))
                    config.MaxResponseLength = ml.GetInt32();
                if (neural.TryGetProperty("rerankerEnabled", out var re))
                    config.RerankerEnabled = re.GetBoolean();
                if (neural.TryGetProperty("predictorEnabled", out var pe))
                    config.PredictorEnabled = pe.GetBoolean();
                if (neural.TryGetProperty("autoRetrainAfterConversations", out var ar))
                    config.AutoRetrainAfterConversations = ar.GetInt32();
            }
            return config;
        }
        catch
        {
            return new NeuralConfig();
        }
    }
}

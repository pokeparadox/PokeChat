namespace PokeChat.Enrichment;

public class MemPalaceOptions
{
    public bool Enabled { get; set; }
    public string Wing { get; set; } = "pokechat";
    public bool EnrichFacts { get; set; } = true;
    public bool EnrichDefinitions { get; set; }
    public int MaxRetries { get; set; } = 3;
    public int RetryDelayMs { get; set; } = 500;
    public int TimeoutMs { get; set; } = 3000;
}

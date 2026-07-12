namespace PokeChat.Api.Services;

public class UpstreamOptions
{
    public string? Endpoint { get; set; }
    public string? ApiKey { get; set; }
    public string Model { get; set; } = "gpt-4o-mini";
    public int TimeoutMs { get; set; } = 30000;
    public bool StreamByDefault { get; set; } = true;
    public bool Enabled => !string.IsNullOrWhiteSpace(Endpoint);
}

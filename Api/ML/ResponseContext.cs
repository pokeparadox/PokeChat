namespace PokeChat.ML;

public class ResponseContext
{
    public string Category { get; set; } = string.Empty;
    public string? CurrentIntent { get; set; }
    public float SentimentScore { get; set; }
    public string? PreviousResponse { get; set; }
    public int TurnNumber { get; set; }
    public string? UserInput { get; set; }
    public string? UserName { get; set; }
    public double CategoryFollowUpRate { get; set; }
}

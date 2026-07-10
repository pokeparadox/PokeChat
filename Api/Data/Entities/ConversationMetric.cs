namespace PokeChat.Data.Entities;

public class ConversationMetric
{
    public int Id { get; set; }
    public string SessionId { get; set; } = string.Empty;
    public int? UserId { get; set; }
    public int TurnCount { get; set; }
    public int FactsLearned { get; set; }
    public string? DominantSentiment { get; set; }
    public string? SentimentTrend { get; set; }
    public int TopicsDiscussed { get; set; }
    public string? BotResponseStats { get; set; }
    public int AvgResponseLength { get; set; }
    public int SessionLength { get; set; }
    public string StartedAt { get; set; } = string.Empty;
    public string EndedAt { get; set; } = string.Empty;
}

namespace PokeChat.Data.Entities;

public class FactEntity
{
    public int Id { get; set; }
    public int? UserId { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Verb { get; set; } = string.Empty;
    public string Object { get; set; } = string.Empty;
    public string PredicateType { get; set; } = string.Empty;
    public string? Sentiment { get; set; }
    public int EmotionIntensity { get; set; }
    public string CreatedAt { get; set; } = string.Empty;
}

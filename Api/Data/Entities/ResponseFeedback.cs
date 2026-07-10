namespace PokeChat.Data.Entities;

public class ResponseFeedback
{
    public int Id { get; set; }
    public int RuleId { get; set; }
    public bool IsLearnedRule { get; set; }
    public int UserId { get; set; }
    public string Feedback { get; set; } = string.Empty;
    public string? CorrectionText { get; set; }
    public string CreatedAt { get; set; } = string.Empty;
}

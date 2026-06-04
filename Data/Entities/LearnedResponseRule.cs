namespace PokeChat.Data.Entities;

public class LearnedResponseRule
{
    public int Id { get; set; }
    public string Pattern { get; set; } = string.Empty;
    public string ResponseTemplate { get; set; } = string.Empty;
    public string InputType { get; set; } = string.Empty;
    public int? LearnedFromUserId { get; set; }
    public int Confidence { get; set; } = 5;
    public bool IsActive { get; set; } = true;
    public string CreatedAt { get; set; } = string.Empty;
}

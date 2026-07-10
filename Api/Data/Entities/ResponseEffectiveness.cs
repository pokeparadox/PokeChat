namespace PokeChat.Data.Entities;

public class ResponseEffectiveness
{
    public int Id { get; set; }
    public string Category { get; set; } = string.Empty;
    public int AvgSessionLengthAfter { get; set; }
    public int UsedCount { get; set; }
    public double FollowUpRate { get; set; }
    public string LastUsed { get; set; } = string.Empty;
}

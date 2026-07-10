namespace PokeChat.Data.Entities;

public class NounCategory
{
    public int Id { get; set; }
    public string Noun { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int? LearnedFromUserId { get; set; }
    public string CreatedAt { get; set; } = string.Empty;
}

namespace PokeChat.Data.Entities;

public class GreetingWord
{
    public int Id { get; set; }
    public string Word { get; set; } = string.Empty;
    public int? LearnedFromUserId { get; set; }
    public string CreatedAt { get; set; } = string.Empty;
}

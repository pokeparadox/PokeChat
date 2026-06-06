namespace PokeChat.Data.Entities;

public class StoryTemplate
{
    public int Id { get; set; }
    public string Template { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string CreatedAt { get; set; } = string.Empty;
}

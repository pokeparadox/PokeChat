namespace PokeChat.Data.Entities;

public class BotResponse
{
    public int Id { get; set; }
    public string Category { get; set; } = string.Empty;
    public string ResponseText { get; set; } = string.Empty;
    public string CreatedAt { get; set; } = string.Empty;
}

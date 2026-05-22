namespace PokeChat.Data.Entities;

public class Conversation
{
    public int Id { get; set; }
    public int? UserId { get; set; }
    public string UserInput { get; set; } = string.Empty;
    public string BotResponse { get; set; } = string.Empty;
    public string Timestamp { get; set; } = string.Empty;
}

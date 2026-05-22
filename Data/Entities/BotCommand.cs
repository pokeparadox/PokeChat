namespace PokeChat.Data.Entities;

public class BotCommand
{
    public int Id { get; set; }
    public string Command { get; set; } = string.Empty;
    public string CreatedAt { get; set; } = string.Empty;
}

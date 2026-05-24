namespace PokeChat.Data.Entities;

public class BotRenamePattern
{
    public int Id { get; set; }
    public string Pattern { get; set; } = string.Empty;
    public string CreatedAt { get; set; } = string.Empty;
}

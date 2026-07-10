namespace PokeChat.Data.Entities;

public class UserBotName
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string BotName { get; set; } = string.Empty;
    public string CreatedAt { get; set; } = string.Empty;
}

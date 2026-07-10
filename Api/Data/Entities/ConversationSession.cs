namespace PokeChat.Data.Entities;

public class ConversationSession
{
    public int Id { get; set; }
    public string SessionGuid { get; set; } = string.Empty;
    public int? UserId { get; set; }
    public string StartedAt { get; set; } = string.Empty;
    public string? EndedAt { get; set; }
    public int TurnCount { get; set; }
    public string? LastActiveAt { get; set; }
    public string? BotName { get; set; }
    public string? Persona { get; set; }
}

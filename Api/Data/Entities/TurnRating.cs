namespace PokeChat.Data.Entities;

public class TurnRating
{
    public int Id { get; set; }
    public int ConversationId { get; set; }
    public int? UserId { get; set; }
    public int Rating { get; set; }
    public string CreatedAt { get; set; } = string.Empty;
}

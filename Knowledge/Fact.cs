namespace PokeChat.Knowledge;

public class Fact
{
    public int Id { get; set; }
    public int? UserId { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Verb { get; set; } = string.Empty;
    public string Object { get; set; } = string.Empty;
    public string PredicateType { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

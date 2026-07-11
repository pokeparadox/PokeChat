namespace PokeChat.Data.Entities;

public class FactEndorsement
{
    public int Id { get; set; }
    public int FactId { get; set; }
    public int UserId { get; set; }
    public string CreatedAt { get; set; } = string.Empty;
}

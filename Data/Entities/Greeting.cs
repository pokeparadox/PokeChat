namespace PokeChat.Data.Entities;

public class Greeting
{
    public int Id { get; set; }
    public string Text { get; set; } = string.Empty;
    public bool IsSystem { get; set; }
    public string CreatedAt { get; set; } = string.Empty;
}

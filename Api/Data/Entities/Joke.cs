namespace PokeChat.Data.Entities;

public class Joke
{
    public int Id { get; set; }
    public string Setup { get; set; } = "";
    public string Punchline { get; set; } = "";
    public string? Category { get; set; }
    public string CreatedAt { get; set; } = "";
}

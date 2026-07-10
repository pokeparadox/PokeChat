namespace PokeChat.Data.Entities;

public class Riddle
{
    public int Id { get; set; }
    public string Question { get; set; } = "";
    public string Answer { get; set; } = "";
    public string? Hint { get; set; }
    public int Difficulty { get; set; } = 1;
    public string CreatedAt { get; set; } = "";
}

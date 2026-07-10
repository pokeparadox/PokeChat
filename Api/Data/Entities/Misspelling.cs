namespace PokeChat.Data.Entities;

public class Misspelling
{
    public int Id { get; set; }
    public string WrongWord { get; set; } = string.Empty;
    public string Correction { get; set; } = string.Empty;
    public string CreatedAt { get; set; } = string.Empty;
}

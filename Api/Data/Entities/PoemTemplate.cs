namespace PokeChat.Data.Entities;

public class PoemTemplate
{
    public int Id { get; set; }
    public string Template { get; set; } = string.Empty;
    public string PoemType { get; set; } = string.Empty;
    public string CreatedAt { get; set; } = string.Empty;
}

namespace PokeChat.Data.Entities;

public class WordDefinition
{
    public int Id { get; set; }
    public string Word { get; set; } = string.Empty;
    public string Definition { get; set; } = string.Empty;
    public int? DefinedByUserId { get; set; }
    public string CreatedAt { get; set; } = string.Empty;
}

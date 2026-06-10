namespace PokeChat.Data.Entities;

public class RhymeGroup
{
    public int Id { get; set; }
    public string RhymeKey { get; set; } = string.Empty;
    public string Word { get; set; } = string.Empty;
    public string WordType { get; set; } = string.Empty;
    public string CreatedAt { get; set; } = string.Empty;
}

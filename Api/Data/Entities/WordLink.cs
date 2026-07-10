namespace PokeChat.Data.Entities;

public class WordLink
{
    public int Id { get; set; }
    public string SourceWord { get; set; } = string.Empty;
    public string TargetWord { get; set; } = string.Empty;
    public string LinkType { get; set; } = string.Empty;
    public int? CreatedByUserId { get; set; }
    public string CreatedAt { get; set; } = string.Empty;
}

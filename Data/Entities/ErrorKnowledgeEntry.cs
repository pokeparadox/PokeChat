namespace PokeChat.Data.Entities;

public class ErrorKnowledgeEntry
{
    public int Id { get; set; }
    public string Pattern { get; set; } = "";
    public string Suggestion { get; set; } = "";
    public string Language { get; set; } = "general";
    public bool IsLearned { get; set; }
    public int UsedCount { get; set; }
    public int SuccessCount { get; set; }
    public string CreatedAt { get; set; } = "";
}

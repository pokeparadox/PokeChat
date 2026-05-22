namespace PokeChat.Data.Entities;

public class ResponseRule
{
    public int Id { get; set; }
    public string Pattern { get; set; } = string.Empty;
    public string InputType { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string CreatedAt { get; set; } = string.Empty;
    public List<ResponseRuleResponse> Responses { get; set; } = new();
}

namespace PokeChat.Data.Entities;

public class ResponseRuleResponse
{
    public int Id { get; set; }
    public int RuleId { get; set; }
    public string ResponseText { get; set; } = string.Empty;
    public ResponseRule Rule { get; set; } = null!;
}

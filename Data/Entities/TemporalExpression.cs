namespace PokeChat.Data.Entities;

public class TemporalExpression
{
    public int Id { get; set; }
    public string Expression { get; set; } = string.Empty;
    public int DaysOffset { get; set; }
    public bool IsRange { get; set; }
}

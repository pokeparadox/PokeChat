namespace PokeChat.Data.Entities;

public class EmotionKeyword
{
    public int Id { get; set; }
    public string Word { get; set; } = "";
    public string Sentiment { get; set; } = "";
    public int Intensity { get; set; } = 1;
    public string CreatedAt { get; set; } = "";
}

namespace PokeChat.Data.Entities;

public class Reminder
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Task { get; set; } = string.Empty;
    public string DueAt { get; set; } = string.Empty;
    public string Status { get; set; } = "pending";
    public string CreatedAt { get; set; } = string.Empty;
}

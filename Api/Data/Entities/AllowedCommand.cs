namespace PokeChat.Data.Entities;

public class AllowedCommand
{
    public int Id { get; set; }
    public string Command { get; set; } = string.Empty;
    public bool IsPermanent { get; set; }
    public string? ExpiresAt { get; set; }
    public int? AddedByUserId { get; set; }
    public string CreatedAt { get; set; } = string.Empty;
}

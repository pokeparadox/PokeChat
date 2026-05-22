namespace PokeChat.Data.Entities;

public class User
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string FirstSeen { get; set; } = string.Empty;
    public string LastSeen { get; set; } = string.Empty;
}

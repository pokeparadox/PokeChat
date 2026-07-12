namespace PokeChat.Api.Services;

public class SessionQuotaOptions
{
    public int MaxSessions { get; set; } = 50;
    public int MaxSessionsPerUser { get; set; } = 10;
    public int MaxTurnsPerSession { get; set; } = 100;
    public int MaxUpstreamCallsPerSession { get; set; } = 20;
    public int SessionTtlMinutes { get; set; } = 60;
}

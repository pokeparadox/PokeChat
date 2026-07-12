namespace PokeChat.Math;

public record TimeResult(string FormattedTime, string TimeOfDayPhrase, string? Timezone);

public interface ITimeEngine
{
    TimeResult? Evaluate(string input, int? userId = null);
    string? ExtractTimezone(string input);
}

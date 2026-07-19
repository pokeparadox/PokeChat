using System.Text.RegularExpressions;

namespace PokeChat.Math;

public partial class SystemTimeEngine : ITimeEngine
{
    private static readonly Regex TimeQueryPattern = TimeQueryRegex();
    private static readonly Regex DateQueryPattern = DateQueryRegex();
    private static readonly Regex DayQueryPattern = DayQueryRegex();
    private static readonly Regex TimezoneSetPattern = TimezoneSetRegex();
    private static readonly Regex TimezoneExtractPattern = TimezoneExtractRegex();

    private static readonly HashSet<string> KnownTimezoneNames =
    [
        "utc", "gmt", "est", "edt", "cst", "cdt", "mst", "mdt", "pst", "pdt",
        "aest", "aedt", "bst", "cet", "cest", "eet", "eest", "ist", "jst", "kst"
    ];

    public TimeResult? Evaluate(string input, int? userId = null)
    {
        var lower = input.Trim().ToLowerInvariant();

        if (!TimeQueryPattern.IsMatch(lower) &&
            !DateQueryPattern.IsMatch(lower) &&
            !DayQueryPattern.IsMatch(lower))
            return null;

        var now = DateTime.UtcNow;
        var timezone = ExtractTimezone(lower);
        TimeZoneInfo? tz = null;

        if (timezone != null)
        {
            try { tz = TimeZoneInfo.FindSystemTimeZoneById(timezone); }
            catch
            {
                try
                {
                    tz = TimeZoneInfo.GetSystemTimeZones()
                        .FirstOrDefault(z =>
                            string.Equals(z.Id, timezone, StringComparison.OrdinalIgnoreCase) ||
                            z.Id.EndsWith("/" + timezone, StringComparison.OrdinalIgnoreCase));
                }
                catch { }
            }
        }

        string formatted;

        if (DateQueryPattern.IsMatch(lower) && !TimeQueryPattern.IsMatch(lower))
        {
            var localDate = tz ?? TimeZoneInfo.Utc;
            formatted = TimeZoneInfo.ConvertTimeFromUtc(now, localDate).ToString("dddd, MMMM dd yyyy");
        }
        else if (DayQueryPattern.IsMatch(lower))
        {
            var localDate = tz ?? TimeZoneInfo.Utc;
            formatted = TimeZoneInfo.ConvertTimeFromUtc(now, localDate).ToString("dddd");
        }
        else
        {
            var localTime = tz ?? TimeZoneInfo.Utc;
            formatted = TimeZoneInfo.ConvertTimeFromUtc(now, localTime).ToString("HH:mm:ss");
        }

        var timeOfDay = GetTimeOfDayPhrase(TimeZoneInfo.ConvertTimeFromUtc(now, tz ?? TimeZoneInfo.Utc).Hour);
        var tzDisplay = tz?.Id ?? "UTC";

        return new TimeResult(formatted, timeOfDay, tzDisplay);
    }

    public string? ExtractTimezone(string input)
    {
        var match = TimezoneExtractPattern.Match(input);
        if (!match.Success) return null;

        var candidate = match.Groups[1].Value.ToLowerInvariant();

        if (KnownTimezoneNames.Contains(candidate))
        {
            return candidate.ToUpperInvariant() switch
            {
                "EST" => "America/New_York",
                "EDT" => "America/New_York",
                "CST" => "America/Chicago",
                "CDT" => "America/Chicago",
                "MST" => "America/Denver",
                "MDT" => "America/Denver",
                "PST" => "America/Los_Angeles",
                "PDT" => "America/Los_Angeles",
                "UTC" => "UTC",
                "GMT" => "GMT",
                "AEST" => "Australia/Sydney",
                "AEDT" => "Australia/Sydney",
                "BST" => "Europe/London",
                "CET" => "Europe/Paris",
                "CEST" => "Europe/Paris",
                "EET" => "Europe/Helsinki",
                "EEST" => "Europe/Helsinki",
                "IST" => "Asia/Kolkata",
                "JST" => "Asia/Tokyo",
                "KST" => "Asia/Seoul",
                _ => candidate
            };
        }

        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(candidate);
            return candidate;
        }
        catch
        {
            return null;
        }
    }

    private static string GetTimeOfDayPhrase(int hour)
    {
        return hour switch
        {
            < 12 => "Good morning",
            < 17 => "Good afternoon",
            < 21 => "Good evening",
            _ => "Good night"
        };
    }

    [GeneratedRegex(@"\b(what(?:'s| is)? the time|what time is it|tell me the time|current time|do you know the time)\b", RegexOptions.IgnoreCase)]
    private static partial Regex TimeQueryRegex();

    [GeneratedRegex(@"\b(what(?:'s| is)? the date|what date is it|today'?s date|current date|what day of the (?:month|year) is it)\b", RegexOptions.IgnoreCase)]
    private static partial Regex DateQueryRegex();

    [GeneratedRegex(@"\b(what day is it|what day of the week|today'?s day)\b", RegexOptions.IgnoreCase)]
    private static partial Regex DayQueryRegex();

    [GeneratedRegex(@"\b(my timezone is |my time zone is |i'm in |i live in |set my timezone to |set my time zone to )(.+)", RegexOptions.IgnoreCase)]
    private static partial Regex TimezoneSetRegex();

    [GeneratedRegex(@"\bin\s+(\w+(?:\/\w+)?)\s*(?:timezone|time zone|time)?\b", RegexOptions.IgnoreCase)]
    private static partial Regex TimezoneExtractRegex();
}

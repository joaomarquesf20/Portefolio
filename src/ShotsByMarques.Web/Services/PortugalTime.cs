namespace ShotsByMarques.Web.Services;

/// <summary>Calendar clock for Portugal mainland, including daylight-saving changes.</summary>
public static class PortugalTime
{
    private static readonly TimeZoneInfo Zone = ResolveZone();

    public static DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Zone);
    public static DateTime Today => Now.Date;

    public static DateTime ToUtc(DateTime local)
    {
        var wallClock = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(wallClock, Zone);
    }

    public static DateTime FromUtc(DateTime value)
    {
        var utc = value.Kind == DateTimeKind.Utc
            ? value
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);
        return TimeZoneInfo.ConvertTimeFromUtc(utc, Zone);
    }

    private static TimeZoneInfo ResolveZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Europe/Lisbon"); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("GMT Standard Time"); }
        catch (InvalidTimeZoneException) { return TimeZoneInfo.Utc; }
    }
}

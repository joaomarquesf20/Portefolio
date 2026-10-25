using System.Globalization;
using System.Text;
using ShotsByMarques.Web.Models;

namespace ShotsByMarques.Web.Services;

public class BookingCalendarService
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public byte[] CreateIcs(Booking booking)
    {
        var start = AsUtc(booking.Start);
        var end = AsUtc(booking.End);
        var title = string.IsNullOrWhiteSpace(booking.SessionType?.Name)
            ? "Marcação — Shots By Marques"
            : $"{booking.SessionType.Name} — Shots By Marques";
        var description = "Marcação de fotografia com Shots By Marques.";
        if (!string.IsNullOrWhiteSpace(booking.SessionType?.Description))
            description += $" {booking.SessionType.Description}";
        if (!string.IsNullOrWhiteSpace(booking.Location))
            description += $" Local: {booking.Location}.";

        var lines = new[]
        {
            "BEGIN:VCALENDAR",
            "VERSION:2.0",
            "PRODID:-//Shots By Marques//Bookings//PT",
            "CALSCALE:GREGORIAN",
            "METHOD:PUBLISH",
            "BEGIN:VEVENT",
            $"UID:shotsbymarques-booking-{booking.Id}@shotsbymarques",
            $"SEQUENCE:{booking.Id}",
            $"DTSTAMP:{DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmss'Z'", Invariant)}",
            $"DTSTART:{start.ToString("yyyyMMdd'T'HHmmss'Z'", Invariant)}",
            $"DTEND:{end.ToString("yyyyMMdd'T'HHmmss'Z'", Invariant)}",
            $"SUMMARY:{Escape(title)}",
            $"DESCRIPTION:{Escape(description)}",
            $"LOCATION:{Escape(booking.Location ?? string.Empty)}",
            "STATUS:CONFIRMED",
            "TRANSP:OPAQUE",
            // Gentle reminder 24 hours before the session (client + admin).
            "BEGIN:VALARM",
            "ACTION:DISPLAY",
            $"DESCRIPTION:{Escape(title)}",
            "TRIGGER:-P1D",
            "END:VALARM",
            "END:VEVENT",
            "END:VCALENDAR"
        };

        return Encoding.UTF8.GetBytes(string.Join("\r\n", lines) + "\r\n");
    }

    public string CreateGoogleCalendarUrl(Booking booking)
    {
        var start = AsUtc(booking.Start);
        var end = AsUtc(booking.End);
        var title = string.IsNullOrWhiteSpace(booking.SessionType?.Name)
            ? "Marcação — Shots By Marques"
            : $"{booking.SessionType.Name} — Shots By Marques";
        var details = "Marcação de fotografia com Shots By Marques.";
        if (!string.IsNullOrWhiteSpace(booking.SessionType?.Description))
            details += $" {booking.SessionType.Description}";
        if (!string.IsNullOrWhiteSpace(booking.Location))
            details += $" Local: {booking.Location}.";

        var query = string.Join("&", new Dictionary<string, string>
        {
            ["action"] = "TEMPLATE",
            ["text"] = title,
            ["dates"] = $"{start.ToString("yyyyMMdd'T'HHmmss'Z'", Invariant)}/{end.ToString("yyyyMMdd'T'HHmmss'Z'", Invariant)}",
            ["details"] = details,
            ["location"] = booking.Location ?? string.Empty
        }.Select(pair => $"{pair.Key}={Uri.EscapeDataString(pair.Value)}"));

        return $"https://calendar.google.com/calendar/render?{query}";
    }

    public string CreateFileName(Booking booking)
    {
        var name = string.IsNullOrWhiteSpace(booking.SessionType?.Name)
            ? "marcacao"
            : booking.SessionType.Name;
        var safe = new string(name
            .Where(c => char.IsLetterOrDigit(c) || c is ' ' or '-' or '_')
            .ToArray())
            .Trim()
            .Replace(' ', '-');
        return $"shots-by-marques-{(string.IsNullOrWhiteSpace(safe) ? "marcacao" : safe)}-{booking.Start:yyyyMMdd}.ics";
    }

    private static DateTime AsUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    private static string Escape(string value) => value
        .Replace("\\", "\\\\")
        .Replace(";", "\\;")
        .Replace(",", "\\,")
        .Replace("\r\n", "\\n")
        .Replace("\n", "\\n")
        .Replace("\r", "\\n");
}

namespace LoopQuest.Domain.Time;

/// <summary>A Monday→Sunday calendar week. Built only through these factories, so a Week is always
/// exactly seven days, Monday to Sunday — it can never be malformed.</summary>
public readonly record struct Week(DateOnly Start, DateOnly End)
{
    /// <summary>The Monday→Sunday week containing <paramref name="date"/>. Pure: no clock, no time zone.</summary>
    public static Week ContainingDate(DateOnly date)
    {
        // Our weeks start Monday, but DayOfWeek counts Sunday as 0. The "+ 6) % 7" re-bases it to
        // days-since-Monday (Mon=0 … Sun=6) so Sundays stay in the current week, not the next one.
        int daysSinceMonday = ((int)date.DayOfWeek + 6) % 7;
        var start = date.AddDays(-daysSinceMonday);
        return new Week(start, start.AddDays(6));
    }

    /// <summary>The Monday→Sunday week containing <paramref name="utcNow"/>, in the user's time zone.
    /// Takes the time as a parameter so tests can pin it to a boundary (e.g. Sunday 23:59).</summary>
    public static Week Current(string ianaTimeZoneId, DateTimeOffset utcNow)
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById(ianaTimeZoneId);

        // Convert to local time before taking the date: a Sunday-night UTC instant can already be
        // Monday locally, so it belongs to the next week.
        var local = TimeZoneInfo.ConvertTime(utcNow, tz);

        return ContainingDate(DateOnly.FromDateTime(local.DateTime));
    }
}

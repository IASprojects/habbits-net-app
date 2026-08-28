using System.Globalization;
using HabitsApp.Domain.Enums;

namespace HabitsApp.Application.Services;

public static class HabitPeriodCalculator
{
    public static DateTime GetWindowStartUtc(FrequencyType frequency, DateTime utcNow)
        => frequency switch
        {
            FrequencyType.Daily => utcNow.Date,
            FrequencyType.Weekly => StartOfWeek(utcNow.Date),
            FrequencyType.Monthly => new DateTime(utcNow.Year, utcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc),
            _ => throw new ArgumentOutOfRangeException(nameof(frequency), frequency, "Unsupported frequency type.")
        };

    public static DateTime GetWindowEndUtc(FrequencyType frequency, DateTime utcNow)
        => frequency switch
        {
            FrequencyType.Daily => utcNow.Date.AddDays(1),
            FrequencyType.Weekly => StartOfWeek(utcNow.Date).AddDays(7),
            FrequencyType.Monthly => new DateTime(utcNow.Year, utcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(1),
            _ => throw new ArgumentOutOfRangeException(nameof(frequency), frequency, "Unsupported frequency type.")
        };

    public static string GetPeriodKey(FrequencyType frequency, DateTime utcNow)
    {
        var windowStart = GetWindowStartUtc(frequency, utcNow);

        return frequency switch
        {
            FrequencyType.Daily => GetDayKey(windowStart),
            FrequencyType.Weekly => $"{ISOWeek.GetYear(windowStart)}-W{ISOWeek.GetWeekOfYear(windowStart):D2}",
            FrequencyType.Monthly => windowStart.ToString("yyyy-MM", CultureInfo.InvariantCulture),
            _ => throw new ArgumentOutOfRangeException(nameof(frequency), frequency, "Unsupported frequency type.")
        };
    }

    public static string GetDayKey(DateTime utcNow)
        => utcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static DateTime GetLocalNow(TimeZoneInfo tz, DateTime utcNow)
        => TimeZoneInfo.ConvertTimeFromUtc(utcNow, tz);

    public static DateTime GetWindowStartUtc(FrequencyType frequency, TimeZoneInfo tz, DateTime utcNow)
    {
        var localNow = GetLocalNow(tz, utcNow);

        var localStart = frequency switch
        {
            FrequencyType.Daily => localNow.Date,
            FrequencyType.Weekly => StartOfWeek(localNow.Date),
            FrequencyType.Monthly => new DateTime(localNow.Year, localNow.Month, 1, 0, 0, 0, DateTimeKind.Unspecified),
            _ => throw new ArgumentOutOfRangeException(nameof(frequency), frequency, "Unsupported frequency type.")
        };

        return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localStart, DateTimeKind.Unspecified), tz);
    }

    public static DateTime GetWindowEndUtc(FrequencyType frequency, TimeZoneInfo tz, DateTime utcNow)
    {
        var localNow = GetLocalNow(tz, utcNow);

        var localEnd = frequency switch
        {
            FrequencyType.Daily => localNow.Date.AddDays(1),
            FrequencyType.Weekly => StartOfWeek(localNow.Date).AddDays(7),
            FrequencyType.Monthly => new DateTime(localNow.Year, localNow.Month, 1, 0, 0, 0, DateTimeKind.Unspecified).AddMonths(1),
            _ => throw new ArgumentOutOfRangeException(nameof(frequency), frequency, "Unsupported frequency type.")
        };

        return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localEnd, DateTimeKind.Unspecified), tz);
    }

    public static string GetPeriodKey(FrequencyType frequency, TimeZoneInfo tz, DateTime utcNow)
    {
        var localNow = GetLocalNow(tz, utcNow);

        return frequency switch
        {
            FrequencyType.Daily => GetDayKey(localNow),
            FrequencyType.Weekly => $"{ISOWeek.GetYear(localNow)}-W{ISOWeek.GetWeekOfYear(localNow):D2}",
            FrequencyType.Monthly => localNow.ToString("yyyy-MM", CultureInfo.InvariantCulture),
            _ => throw new ArgumentOutOfRangeException(nameof(frequency), frequency, "Unsupported frequency type.")
        };
    }

    public static string GetDayKey(TimeZoneInfo tz, DateTime utcNow)
        => GetDayKey(GetLocalNow(tz, utcNow));

    private static DateTime StartOfWeek(DateTime date)
    {
        var daysSinceMonday = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-daysSinceMonday);
    }
}
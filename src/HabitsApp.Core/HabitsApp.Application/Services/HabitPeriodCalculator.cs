using System.Globalization;
using HabitsApp.Domain.Enums;

namespace HabitsApp.Application.Services;

public static class HabitPeriodCalculator
{
    public static DateTime GetLocalNow(TimeZoneInfo tz, DateTime utcNow)
        => TimeZoneInfo.ConvertTimeFromUtc(utcNow, tz);

    public static DateTime GetWindowStartUtc(FrequencyType frequency, DateTime utcNow)
        => GetWindowStartUtc(frequency, TimeZoneInfo.Utc, utcNow);

    public static DateTime GetWindowStartUtc(FrequencyType frequency, TimeZoneInfo tz, DateTime utcNow)
    {
        var localNow = GetLocalNow(tz, utcNow);

        DateTime localStart = frequency switch
        {
            FrequencyType.Daily => localNow.Date,
            FrequencyType.Weekly => StartOfWeek(localNow.Date),
            FrequencyType.Monthly => new DateTime(localNow.Year, localNow.Month, 1, 0, 0, 0),
            _ => throw new ArgumentOutOfRangeException(nameof(frequency), frequency, "Unsupported frequency type.")
        };

        return TimeZoneInfo.ConvertTimeToUtc(localStart, tz);
    }

    public static DateTime GetWindowEndUtc(FrequencyType frequency, DateTime utcNow)
        => GetWindowEndUtc(frequency, TimeZoneInfo.Utc, utcNow);

    public static DateTime GetWindowEndUtc(FrequencyType frequency, TimeZoneInfo tz, DateTime utcNow)
    {
        var localStart = TimeZoneInfo.ConvertTimeFromUtc(GetWindowStartUtc(frequency, tz, utcNow), tz);

        DateTime localEnd = frequency switch
        {
            FrequencyType.Daily => localStart.AddDays(1),
            FrequencyType.Weekly => localStart.AddDays(7),
            FrequencyType.Monthly => localStart.AddMonths(1),
            _ => throw new ArgumentOutOfRangeException(nameof(frequency), frequency, "Unsupported frequency type.")
        };

        return TimeZoneInfo.ConvertTimeToUtc(localEnd, tz);
    }

    public static string GetPeriodKey(FrequencyType frequency, DateTime utcNow)
        => GetPeriodKey(frequency, TimeZoneInfo.Utc, utcNow);

    public static string GetPeriodKey(FrequencyType frequency, TimeZoneInfo tz, DateTime utcNow)
    {
        var localNow = GetLocalNow(tz, utcNow);

        DateTime periodStart = frequency switch
        {
            FrequencyType.Daily => localNow.Date,
            FrequencyType.Weekly => StartOfWeek(localNow.Date),
            FrequencyType.Monthly => new DateTime(localNow.Year, localNow.Month, 1, 0, 0, 0),
            _ => throw new ArgumentOutOfRangeException(nameof(frequency), frequency, "Unsupported frequency type.")
        };

        return frequency switch
        {
            FrequencyType.Daily => periodStart.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            FrequencyType.Weekly => $"{ISOWeek.GetYear(periodStart)}-W{ISOWeek.GetWeekOfYear(periodStart):D2}",
            FrequencyType.Monthly => periodStart.ToString("yyyy-MM", CultureInfo.InvariantCulture),
            _ => throw new ArgumentOutOfRangeException(nameof(frequency), frequency, "Unsupported frequency type.")
        };
    }

    public static string GetDayKey(DateTime utcDateTime)
        => utcDateTime.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static string GetDayKey(TimeZoneInfo tz, DateTime utcNow)
        => GetLocalNow(tz, utcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static string GetHourKey(DateTime utcNow)
        => utcNow.ToString("yyyy-MM-dd'T'HH", CultureInfo.InvariantCulture);

    private static DateTime StartOfWeek(DateTime date)
    {
        var daysSinceMonday = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-daysSinceMonday);
    }
}
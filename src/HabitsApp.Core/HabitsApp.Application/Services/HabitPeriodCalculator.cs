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
            FrequencyType.Daily => windowStart.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            FrequencyType.Weekly => $"{ISOWeek.GetYear(windowStart)}-W{ISOWeek.GetWeekOfYear(windowStart):D2}",
            FrequencyType.Monthly => windowStart.ToString("yyyy-MM", CultureInfo.InvariantCulture),
            _ => throw new ArgumentOutOfRangeException(nameof(frequency), frequency, "Unsupported frequency type.")
        };
    }

    private static DateTime StartOfWeek(DateTime date)
    {
        var daysSinceMonday = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-daysSinceMonday);
    }
}
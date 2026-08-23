using System.Globalization;
using HabitsApp.Application.Services;
using HabitsApp.Domain.Enums;

namespace HabitsApp.Application.Tests;

public class HabitPeriodCalculatorTests
{
    private static readonly DateTime Wednesday = new(2026, 8, 19, 14, 30, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(FrequencyType.Daily, "2026-08-19")]
    [InlineData(FrequencyType.Weekly, "2026-W34")]
    [InlineData(FrequencyType.Monthly, "2026-08")]
    public void GetPeriodKey_ReturnsExpectedKey(FrequencyType frequency, string expected)
    {
        var key = HabitPeriodCalculator.GetPeriodKey(frequency, Wednesday);

        Assert.Equal(expected, key);
    }

    [Theory]
    [InlineData(FrequencyType.Daily, "2026-08-19T00:00:00Z", "2026-08-20T00:00:00Z")]
    [InlineData(FrequencyType.Weekly, "2026-08-17T00:00:00Z", "2026-08-24T00:00:00Z")]
    [InlineData(FrequencyType.Monthly, "2026-08-01T00:00:00Z", "2026-09-01T00:00:00Z")]
    public void WindowBoundaries_AreHalfOpen(FrequencyType frequency, string start, string end)
    {
        var windowStart = HabitPeriodCalculator.GetWindowStartUtc(frequency, Wednesday);
        var windowEnd = HabitPeriodCalculator.GetWindowEndUtc(frequency, Wednesday);

        Assert.Equal(DateTime.Parse(start, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind), windowStart);
        Assert.Equal(DateTime.Parse(end, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind), windowEnd);
    }

    [Fact]
    public void WeeklyWindow_OnMondayStartsNewWeek()
    {
        var monday = new DateTime(2026, 8, 24, 9, 0, 0, DateTimeKind.Utc);

        Assert.Equal(new DateTime(2026, 8, 24), HabitPeriodCalculator.GetWindowStartUtc(FrequencyType.Weekly, monday));
        Assert.Equal("2026-W35", HabitPeriodCalculator.GetPeriodKey(FrequencyType.Weekly, monday));
    }

    [Fact]
    public void SundayBelongsToWeekOfPrecedingMonday()
    {
        var sunday = new DateTime(2026, 8, 23, 23, 59, 0, DateTimeKind.Utc);

        Assert.Equal(new DateTime(2026, 8, 17), HabitPeriodCalculator.GetWindowStartUtc(FrequencyType.Weekly, sunday));
        Assert.Equal("2026-W34", HabitPeriodCalculator.GetPeriodKey(FrequencyType.Weekly, sunday));
    }

    [Fact]
    public void EarlyJanuaryMonday_BelongsToPreviousIsoYear()
    {
        var sunday = new DateTime(2027, 1, 3, 23, 59, 0, DateTimeKind.Utc);

        Assert.Equal(new DateTime(2026, 12, 28), HabitPeriodCalculator.GetWindowStartUtc(FrequencyType.Weekly, sunday));
        Assert.Equal("2026-W53", HabitPeriodCalculator.GetPeriodKey(FrequencyType.Weekly, sunday));
    }

    [Fact]
    public void LateDecemberMondayUsesTypicalIsoYear()
    {
        var monday = new DateTime(2025, 12, 29, 9, 0, 0, DateTimeKind.Utc);

        Assert.Equal(monday.Date, HabitPeriodCalculator.GetWindowStartUtc(FrequencyType.Weekly, monday));
        Assert.Equal("2026-W01", HabitPeriodCalculator.GetPeriodKey(FrequencyType.Weekly, monday));
    }

    [Fact]
    public void MonthlyWindow_CrossesMonthBoundary()
    {
        var lastDay = new DateTime(2026, 8, 31, 18, 0, 0, DateTimeKind.Utc);
        var firstDay = new DateTime(2026, 9, 1, 1, 0, 0, DateTimeKind.Utc);

        Assert.Equal(new DateTime(2026, 8, 1), HabitPeriodCalculator.GetWindowStartUtc(FrequencyType.Monthly, lastDay));
        Assert.Equal("2026-08", HabitPeriodCalculator.GetPeriodKey(FrequencyType.Monthly, lastDay));

        Assert.Equal(new DateTime(2026, 9, 1), HabitPeriodCalculator.GetWindowStartUtc(FrequencyType.Monthly, firstDay));
        Assert.Equal("2026-09", HabitPeriodCalculator.GetPeriodKey(FrequencyType.Monthly, firstDay));
    }
}
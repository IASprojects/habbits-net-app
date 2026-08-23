using HabitsApp.Domain.Enums;

namespace HabitsApp.Application.Contracts.Habits;

public sealed class HabitDashboardItemDto
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string ColorHex { get; set; } = "#4F46E5";

    public FrequencyType Frequency { get; set; }

    public int TargetCount { get; set; }

    public int CurrentPeriodCount { get; set; }

    public bool IsCompletedForPeriod { get; set; }
}
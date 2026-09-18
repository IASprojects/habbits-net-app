using HabitsApp.Domain.Enums;

namespace HabitsApp.Application.Contracts.Habits;

public sealed class DashboardResponseDto
{
    public DayPeriod CurrentPeriod { get; set; }

    public IReadOnlyList<HabitDashboardItemDto> Habits { get; set; } = [];
}
namespace HabitsApp.Application.Contracts.Habits;

public sealed class CalendarDayDto
{
    public DateOnly Date { get; set; }

    public IReadOnlyList<string> Colors { get; set; } = [];
}
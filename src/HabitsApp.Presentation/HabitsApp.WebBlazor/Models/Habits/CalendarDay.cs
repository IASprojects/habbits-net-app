namespace HabitsApp.WebBlazor.Models.Habits;

public sealed class CalendarDay
{
    public DateOnly Date { get; set; }

    public List<string> Colors { get; set; } = [];
}
namespace HabitsApp.WebBlazor.Models.Habits;

public sealed class HabitDashboardResponse
{
    public string CurrentPeriod { get; set; } = "Morning";

    public List<HabitDashboardItem> Habits { get; set; } = [];
}
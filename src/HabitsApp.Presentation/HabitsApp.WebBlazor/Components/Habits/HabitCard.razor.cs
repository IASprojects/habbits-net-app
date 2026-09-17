using HabitsApp.WebBlazor.Models.Habits;
using Microsoft.AspNetCore.Components;

namespace HabitsApp.WebBlazor.Components.Habits;

public partial class HabitCard
{
    private bool _isBusy;

    [Parameter]
    public HabitDashboardItem Habit { get; set; } = default!;

    [Parameter]
    public EventCallback<HabitDashboardItem> OnEdit { get; set; }

    [Parameter]
    public EventCallback<HabitDashboardItem> OnQuickLog { get; set; }

    private bool IsBusy => _isBusy;

    private string StreakLabel
        => Habit.Streak > 0 ? $"{Habit.Streak} day streak" : "No streak";

    private string ProgressLabel
        => Habit.Frequency switch
        {
            "Weekly" => $"{Habit.CurrentPeriodCount} / {Habit.TargetCount} this week",
            "Monthly" => $"{Habit.CurrentPeriodCount} / {Habit.TargetCount} this month",
            _ => $"{Habit.CurrentPeriodCount} / {Habit.TargetCount} today"
        };

    private async Task HandleQuickLogAsync()
    {
        if (_isBusy)
        {
            return;
        }

        _isBusy = true;
        try
        {
            await OnQuickLog.InvokeAsync(Habit);
        }
        finally
        {
            _isBusy = false;
        }
    }

    private static string GetChipBackground(string colorHex)
        => colorHex.Length == 7 ? $"{colorHex}22" : "rgba(78, 222, 163, 0.12)";

    private static string GetGlowColor(string colorHex)
    {
        var rgb = colorHex.Length == 7 && colorHex.StartsWith('#')
            ? string.Join(", ", Convert.ToByte(colorHex.Substring(1, 2), 16), Convert.ToByte(colorHex.Substring(3, 2), 16), Convert.ToByte(colorHex.Substring(5, 2), 16))
            : "78, 222, 163";

        return $"rgba({rgb}, 0.3)";
    }
}
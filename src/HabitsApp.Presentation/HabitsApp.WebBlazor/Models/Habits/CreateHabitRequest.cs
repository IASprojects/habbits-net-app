namespace HabitsApp.WebBlazor.Models.Habits;

public sealed class CreateHabitRequest
{
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string ColorHex { get; set; } = "#4F46E5";

    public string Frequency { get; set; } = "Daily";

    public int TargetCount { get; set; } = 1;
}
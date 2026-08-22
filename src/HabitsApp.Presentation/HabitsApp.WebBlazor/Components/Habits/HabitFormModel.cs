using System.ComponentModel.DataAnnotations;

namespace HabitsApp.WebBlazor.Components.Habits;

public sealed class HabitFormModel
{
    [Required(ErrorMessage = "Title is required.")]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string ColorHex { get; set; } = "#4F46E5";

    public string Frequency { get; set; } = "Daily";

    [Range(1, 999, ErrorMessage = "Target count must be at least 1.")]
    public int TargetCount { get; set; } = 1;
}
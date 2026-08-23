using System.ComponentModel.DataAnnotations;
using HabitsApp.Domain.Enums;

namespace HabitsApp.Application.Contracts.Habits;

public sealed class CreateHabitDto
{
    [Required(ErrorMessage = "Title is required.")]
    [StringLength(200, MinimumLength = 1, ErrorMessage = "Title must be between 1 and 200 characters.")]
    public string Title { get; set; } = string.Empty;

    [StringLength(1000, ErrorMessage = "Description must be at most 1000 characters.")]
    public string? Description { get; set; }

    public string ColorHex { get; set; } = "#4F46E5";

    public FrequencyType Frequency { get; set; } = FrequencyType.Daily;

    [Range(1, 999, ErrorMessage = "Target count must be at least 1.")]
    public int TargetCount { get; set; } = 1;
}
using HabitsApp.Domain.Enums;

namespace HabitsApp.Domain.Entities;

public sealed class Habit
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string ColorHex { get; set; } = "#4F46E5";

    public FrequencyType Frequency { get; set; } = FrequencyType.Daily;

    public int TargetCount { get; set; } = 1;

    public bool IsArchived { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    public ICollection<HabitLog> Logs { get; set; } = [];
}
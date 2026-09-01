namespace HabitsApp.Domain.Entities;

public sealed class HabitLog
{
    public Guid Id { get; set; }

    public Guid HabitId { get; set; }

    public Guid UserId { get; set; }

    public DateTime CompletedAtUtc { get; set; }

    public string PeriodKey { get; set; } = string.Empty;

    public string HourKey { get; set; } = string.Empty;
}
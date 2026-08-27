namespace HabitsApp.Domain.Entities;

public sealed class HabitLog
{
    public Guid Id { get; set; }

    public Guid HabitId { get; set; }

    public Guid UserId { get; set; }

    public DateTime CompletedAtUtc { get; set; }

    /// <summary>
    /// Calendar day key (<c>yyyy-MM-dd</c>, UTC) used to prevent duplicate logs on the same day.
    /// The per-period completion target is enforced separately via the habit's <c>TargetCount</c>
    /// and the frequency window bounds computed by the application layer.
    /// </summary>
    public string PeriodKey { get; set; } = string.Empty;
}
namespace HabitsApp.Application.Contracts.Habits;

public interface IHabitService
{
    Task<IReadOnlyList<HabitDashboardItemDto>> GetDashboardAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<HabitResult> CreateAsync(Guid userId, CreateHabitDto dto, CancellationToken cancellationToken = default);

    Task<HabitResult> UpdateAsync(Guid userId, Guid habitId, UpdateHabitDto dto, CancellationToken cancellationToken = default);

    Task<HabitResult> QuickLogAsync(Guid userId, Guid habitId, CancellationToken cancellationToken = default);

    Task<HabitResult> ArchiveAsync(Guid userId, Guid habitId, CancellationToken cancellationToken = default);
}
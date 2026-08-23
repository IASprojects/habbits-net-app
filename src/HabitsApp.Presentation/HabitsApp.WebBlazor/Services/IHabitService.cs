using HabitsApp.WebBlazor.Models.Habits;

namespace HabitsApp.WebBlazor.Services;

public interface IHabitService
{
    Task<IReadOnlyList<HabitDashboardItem>> GetDashboardAsync(CancellationToken cancellationToken = default);

    Task<HabitDashboardItem> CreateAsync(CreateHabitRequest request, CancellationToken cancellationToken = default);

    Task<HabitDashboardItem> UpdateAsync(Guid habitId, UpdateHabitRequest request, CancellationToken cancellationToken = default);

    Task<HabitDashboardItem> QuickLogAsync(Guid habitId, CancellationToken cancellationToken = default);

    Task ArchiveAsync(Guid habitId, CancellationToken cancellationToken = default);
}
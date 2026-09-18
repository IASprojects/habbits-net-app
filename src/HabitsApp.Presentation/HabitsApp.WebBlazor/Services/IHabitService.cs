using HabitsApp.WebBlazor.Models.Habits;

namespace HabitsApp.WebBlazor.Services;

public interface IHabitService
{
    Task<HabitDashboardResponse> GetDashboardAsync(bool activeOnly = true, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CalendarDay>> GetCalendarAsync(
        DateOnly start,
        DateOnly end,
        Guid? habitId,
        CancellationToken cancellationToken = default);

    Task<HabitDashboardItem> CreateAsync(CreateHabitRequest request, CancellationToken cancellationToken = default);

    Task<HabitDashboardItem> UpdateAsync(Guid habitId, UpdateHabitRequest request, CancellationToken cancellationToken = default);

    Task<HabitDashboardItem> QuickLogAsync(Guid habitId, CancellationToken cancellationToken = default);

    Task InactivateAsync(Guid habitId, CancellationToken cancellationToken = default);

    Task<HabitDashboardItem> ReactivateAsync(Guid habitId, CancellationToken cancellationToken = default);
}
using HabitsApp.Application.Contracts.Habits;
using HabitsApp.Application.Services;
using HabitsApp.Domain.Entities;
using HabitsApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace HabitsApp.Api.Services;

public sealed class HabitService : IHabitService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<HabitService> _logger;

    public HabitService(ApplicationDbContext dbContext, ILogger<HabitService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<IReadOnlyList<HabitDashboardItemDto>> GetDashboardAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var habits = await _dbContext.Habits
            .Where(h => !h.IsArchived)
            .OrderBy(h => h.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        if (habits.Count == 0)
        {
            return [];
        }

        var habitIds = habits.Select(h => h.Id).ToArray();
        var logs = await _dbContext.HabitLogs
            .Where(l => habitIds.Contains(l.HabitId))
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var items = new List<HabitDashboardItemDto>(habits.Count);

        foreach (var habit in habits)
        {
            var windowStart = HabitPeriodCalculator.GetWindowStartUtc(habit.Frequency, now);
            var windowEnd = HabitPeriodCalculator.GetWindowEndUtc(habit.Frequency, now);

            var currentPeriodCount = logs.Count(l =>
                l.HabitId == habit.Id &&
                l.CompletedAtUtc >= windowStart &&
                l.CompletedAtUtc < windowEnd);

            items.Add(ToDto(habit, currentPeriodCount));
        }

        return items;
    }

    public async Task<HabitResult> CreateAsync(Guid userId, CreateHabitDto dto, CancellationToken cancellationToken = default)
    {
        var habit = new Habit
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = dto.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim(),
            ColorHex = string.IsNullOrWhiteSpace(dto.ColorHex) ? "#4F46E5" : dto.ColorHex,
            Frequency = dto.Frequency,
            TargetCount = dto.TargetCount,
            IsArchived = false,
            CreatedAtUtc = DateTime.UtcNow
        };

        _dbContext.Habits.Add(habit);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User {UserId} created habit {HabitId}.", userId, habit.Id);

        return HabitResult.Success(ToDto(habit, 0));
    }

    public async Task<HabitResult> UpdateAsync(Guid userId, Guid habitId, UpdateHabitDto dto, CancellationToken cancellationToken = default)
    {
        var habit = await _dbContext.Habits.FirstOrDefaultAsync(h => h.Id == habitId, cancellationToken);
        if (habit is null)
        {
            return HabitResult.Failure(
                StatusCodes.Status404NotFound,
                "Habit not found",
                "The habit was not found or is not accessible.");
        }

        habit.Title = dto.Title.Trim();
        habit.Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();
        habit.ColorHex = string.IsNullOrWhiteSpace(dto.ColorHex) ? "#4F46E5" : dto.ColorHex;
        habit.Frequency = dto.Frequency;
        habit.TargetCount = dto.TargetCount;
        habit.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return HabitResult.Success(await BuildDashboardItemAsync(habit, cancellationToken));
    }

    public async Task<HabitResult> QuickLogAsync(Guid userId, Guid habitId, CancellationToken cancellationToken = default)
    {
        var habit = await _dbContext.Habits.FirstOrDefaultAsync(h => h.Id == habitId, cancellationToken);
        if (habit is null)
        {
            return HabitResult.Failure(
                StatusCodes.Status404NotFound,
                "Habit not found",
                "The habit was not found or is not accessible.");
        }

        var now = DateTime.UtcNow;
        var periodKey = HabitPeriodCalculator.GetPeriodKey(habit.Frequency, now);

        var existing = await _dbContext.HabitLogs
            .FirstOrDefaultAsync(l => l.HabitId == habitId && l.PeriodKey == periodKey, cancellationToken);
        if (existing is not null)
        {
            return HabitResult.Success(await BuildDashboardItemAsync(habit, cancellationToken));
        }

        _dbContext.HabitLogs.Add(new HabitLog
        {
            Id = Guid.NewGuid(),
            HabitId = habitId,
            UserId = userId,
            CompletedAtUtc = now,
            PeriodKey = periodKey
        });

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            _logger.LogWarning(ex, "Quick log race detected for habit {HabitId} in period {PeriodKey}; treating as idempotent.", habitId, periodKey);
        }

        return HabitResult.Success(await BuildDashboardItemAsync(habit, cancellationToken));
    }

    public async Task<HabitResult> ArchiveAsync(Guid userId, Guid habitId, CancellationToken cancellationToken = default)
    {
        var habit = await _dbContext.Habits.FirstOrDefaultAsync(h => h.Id == habitId, cancellationToken);
        if (habit is null)
        {
            return HabitResult.Failure(
                StatusCodes.Status404NotFound,
                "Habit not found",
                "The habit was not found or is not accessible.");
        }

        habit.IsArchived = true;
        habit.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User {UserId} archived habit {HabitId}.", userId, habitId);

        return HabitResult.Success(ToDto(habit, 0));
    }

    public async Task<IReadOnlyList<CalendarDayDto>> GetCalendarAsync(
        Guid userId,
        DateOnly start,
        DateOnly end,
        Guid? habitId,
        CancellationToken cancellationToken = default)
    {
        var startUtc = start.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var endUtcExclusive = end.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        var logsQuery = _dbContext.HabitLogs
            .AsNoTracking()
            .Where(l => l.CompletedAtUtc >= startUtc && l.CompletedAtUtc < endUtcExclusive);

        if (habitId.HasValue)
        {
            logsQuery = logsQuery.Where(l => l.HabitId == habitId.Value);
        }

        var logRows = await logsQuery
            .Select(l => new { l.HabitId, Date = l.CompletedAtUtc.Date })
            .ToListAsync(cancellationToken);

        if (logRows.Count == 0)
        {
            return [];
        }

        var habitIds = logRows.Select(r => r.HabitId).Distinct().ToArray();
        var colorMap = await _dbContext.Habits
            .AsNoTracking()
            .Where(h => habitIds.Contains(h.Id))
            .ToDictionaryAsync(h => h.Id, h => h.ColorHex, cancellationToken);

        return logRows
            .GroupBy(r => DateOnly.FromDateTime(r.Date))
            .OrderBy(g => g.Key)
            .Select(g => new CalendarDayDto
            {
                Date = g.Key,
                Colors = g
                    .Select(r => colorMap.TryGetValue(r.HabitId, out var color) ? color : "#4F46E5")
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray()
            })
            .ToList();
    }

    private async Task<HabitDashboardItemDto> BuildDashboardItemAsync(Habit habit, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var windowStart = HabitPeriodCalculator.GetWindowStartUtc(habit.Frequency, now);
        var windowEnd = HabitPeriodCalculator.GetWindowEndUtc(habit.Frequency, now);

        var currentPeriodCount = await _dbContext.HabitLogs
            .CountAsync(l =>
                l.HabitId == habit.Id &&
                l.CompletedAtUtc >= windowStart &&
                l.CompletedAtUtc < windowEnd,
                cancellationToken);

        return ToDto(habit, currentPeriodCount);
    }

    private static HabitDashboardItemDto ToDto(Habit habit, int currentPeriodCount)
        => new()
        {
            Id = habit.Id,
            Title = habit.Title,
            Description = habit.Description,
            ColorHex = habit.ColorHex,
            Frequency = habit.Frequency,
            TargetCount = habit.TargetCount,
            CurrentPeriodCount = currentPeriodCount,
            IsCompletedForPeriod = currentPeriodCount >= habit.TargetCount
        };
}
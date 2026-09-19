using HabitsApp.Application.Contracts.Habits;
using HabitsApp.Application.Services;
using HabitsApp.Domain.Entities;
using HabitsApp.Domain.Enums;
using HabitsApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace HabitsApp.Api.Services;

public sealed class HabitService : IHabitService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly TimeProvider _time;
    private readonly ILogger<HabitService> _logger;

    public HabitService(ApplicationDbContext dbContext, TimeProvider time, ILogger<HabitService> logger)
    {
        _dbContext = dbContext;
        _time = time;
        _logger = logger;
    }

    public async Task<DashboardResponseDto> GetDashboardAsync(Guid userId, bool activeOnly = true, CancellationToken cancellationToken = default)
    {
        var now = _time.GetUtcNow().UtcDateTime;
        var tz = await ResolveTimeZoneAsync(userId, cancellationToken);
        var currentPeriod = HabitPeriodCalculator.GetDayPeriod(tz, now);

        var habits = await _dbContext.Habits
            .Where(h => h.UserId == userId && h.IsActive == activeOnly)
            .ToListAsync(cancellationToken);

        var orderedHabits = habits
            .OrderBy(h => HabitPeriodCalculator.GetDisplayRank(h.Period, currentPeriod))
            .ThenBy(h => h.CreatedAtUtc)
            .ToList();

        var habitIds = orderedHabits.Select(h => h.Id).ToArray();
        var logs = await _dbContext.HabitLogs
            .Where(l => habitIds.Contains(l.HabitId))
            .ToListAsync(cancellationToken);

        var logsByHabit = logs.ToLookup(l => l.HabitId);
        var items = new List<HabitDashboardItemDto>(orderedHabits.Count);

        foreach (var habit in orderedHabits)
        {
            var habitLogs = logsByHabit[habit.Id].Select(l => l.CompletedAtUtc).ToList();
            var windowStart = HabitPeriodCalculator.GetWindowStartUtc(habit.Frequency, tz, now);
            var windowEnd = HabitPeriodCalculator.GetWindowEndUtc(habit.Frequency, tz, now);

            var currentPeriodCount = habitLogs.Count(x => x >= windowStart && x < windowEnd);
            var streak = ComputeStreak(habitLogs, tz, now);

            items.Add(ToDto(habit, currentPeriodCount, streak));
        }

        return new DashboardResponseDto
        {
            CurrentPeriod = currentPeriod,
            Habits = items
        };
    }

    public async Task<HabitResult> CreateAsync(Guid userId, CreateHabitDto dto, CancellationToken cancellationToken = default)
    {
        if (dto.Period is not null && !Enum.IsDefined(typeof(DayPeriod), dto.Period.Value))
        {
            return HabitResult.Failure(
                StatusCodes.Status400BadRequest,
                "Invalid period",
                "The habit period is not a valid value.");
        }

        var habit = new Habit
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = dto.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim(),
            ColorHex = string.IsNullOrWhiteSpace(dto.ColorHex) ? "#4F46E5" : dto.ColorHex,
            Frequency = dto.Frequency,
            Period = dto.Period,
            TargetCount = dto.TargetCount,
            IsArchived = false,
            CreatedAtUtc = _time.GetUtcNow().UtcDateTime
        };

        _dbContext.Habits.Add(habit);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User {UserId} created habit {HabitId}.", userId, habit.Id);

        return HabitResult.Success(ToDto(habit, 0, 0));
    }

    public async Task<HabitResult> UpdateAsync(Guid userId, Guid habitId, UpdateHabitDto dto, CancellationToken cancellationToken = default)
    {
        var habit = await _dbContext.Habits.FirstOrDefaultAsync(h => h.Id == habitId && h.UserId == userId, cancellationToken);
        if (habit is null)
        {
            return HabitResult.Failure(
                StatusCodes.Status404NotFound,
                "Habit not found",
                "The habit was not found or is not accessible.");
        }

        if (!habit.IsActive)
        {
            return HabitResult.Failure(
                StatusCodes.Status409Conflict,
                "Habit is inactive",
                "This habit is inactive. Reactivate it before making changes.");
        }

        if (dto.Period is not null && !Enum.IsDefined(typeof(DayPeriod), dto.Period.Value))
        {
            return HabitResult.Failure(
                StatusCodes.Status400BadRequest,
                "Invalid period",
                "The habit period is not a valid value.");
        }

        habit.Title = dto.Title.Trim();
        habit.Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();
        habit.ColorHex = string.IsNullOrWhiteSpace(dto.ColorHex) ? "#4F46E5" : dto.ColorHex;
        habit.Frequency = dto.Frequency;
        habit.Period = dto.Period;
        habit.TargetCount = dto.TargetCount;
        habit.UpdatedAtUtc = _time.GetUtcNow().UtcDateTime;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return HabitResult.Success(await BuildDashboardItemAsync(userId, habit, cancellationToken));
    }

    public async Task<HabitResult> QuickLogAsync(Guid userId, Guid habitId, CancellationToken cancellationToken = default)
    {
        var habit = await _dbContext.Habits.FirstOrDefaultAsync(h => h.Id == habitId && h.UserId == userId, cancellationToken);
        if (habit is null)
        {
            return HabitResult.Failure(
                StatusCodes.Status404NotFound,
                "Habit not found",
                "The habit was not found or is not accessible.");
        }

        if (!habit.IsActive)
        {
            return HabitResult.Failure(
                StatusCodes.Status409Conflict,
                "Habit is inactive",
                "This habit is inactive. Reactivate it before making changes.");
        }

        var now = _time.GetUtcNow().UtcDateTime;
        var tz = await ResolveTimeZoneAsync(userId, cancellationToken);
        var windowStart = HabitPeriodCalculator.GetWindowStartUtc(habit.Frequency, tz, now);
        var windowEnd = HabitPeriodCalculator.GetWindowEndUtc(habit.Frequency, tz, now);
        var hourKey = HabitPeriodCalculator.GetHourKey(now);

        var currentPeriodCount = await _dbContext.HabitLogs
            .CountAsync(l =>
                l.HabitId == habitId &&
                l.CompletedAtUtc >= windowStart &&
                l.CompletedAtUtc < windowEnd &&
                l.UserId == userId,
                cancellationToken);

        if (currentPeriodCount >= habit.TargetCount)
        {
            return HabitResult.Success(await BuildDashboardItemAsync(userId, habit, cancellationToken));
        }

        var existing = await _dbContext.HabitLogs
            .FirstOrDefaultAsync(l => l.HabitId == habitId && l.HourKey == hourKey && l.UserId == userId, cancellationToken);
        if (existing is not null)
        {
            return HabitResult.Success(await BuildDashboardItemAsync(userId, habit, cancellationToken));
        }

        var periodKey = HabitPeriodCalculator.GetPeriodKey(habit.Frequency, tz, now);

        _dbContext.HabitLogs.Add(new HabitLog
        {
            Id = Guid.NewGuid(),
            HabitId = habitId,
            UserId = userId,
            CompletedAtUtc = now,
            PeriodKey = periodKey,
            HourKey = hourKey
        });

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            _logger.LogWarning(ex, "Quick log race detected for habit {HabitId} in hour {HourKey}; treating as idempotent.", habitId, hourKey);
        }

        return HabitResult.Success(await BuildDashboardItemAsync(userId, habit, cancellationToken));
    }

    public async Task<HabitResult> InactivateAsync(Guid userId, Guid habitId, CancellationToken cancellationToken = default)
    {
        var habit = await _dbContext.Habits.FirstOrDefaultAsync(h => h.Id == habitId && h.UserId == userId, cancellationToken);
        if (habit is null)
        {
            return HabitResult.Failure(
                StatusCodes.Status404NotFound,
                "Habit not found",
                "The habit was not found or is not accessible.");
        }

        if (!habit.IsActive)
        {
            return HabitResult.Success(ToDto(habit, 0, 0));
        }

        habit.IsActive = false;
        habit.UpdatedAtUtc = _time.GetUtcNow().UtcDateTime;

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User {UserId} inactivated habit {HabitId}.", userId, habitId);

        return HabitResult.Success(ToDto(habit, 0, 0));
    }

    public async Task<HabitResult> ReactivateAsync(Guid userId, Guid habitId, CancellationToken cancellationToken = default)
    {
        var habit = await _dbContext.Habits.FirstOrDefaultAsync(h => h.Id == habitId && h.UserId == userId, cancellationToken);
        if (habit is null)
        {
            return HabitResult.Failure(
                StatusCodes.Status404NotFound,
                "Habit not found",
                "The habit was not found or is not accessible.");
        }

        habit.IsActive = true;
        habit.UpdatedAtUtc = _time.GetUtcNow().UtcDateTime;

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User {UserId} reactivated habit {HabitId}.", userId, habitId);

        return HabitResult.Success(await BuildDashboardItemAsync(userId, habit, cancellationToken));
    }

    public async Task<DateOnly> GetLocalTodayAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var tz = await ResolveTimeZoneAsync(userId, cancellationToken);
        return DateOnly.FromDateTime(HabitPeriodCalculator.GetLocalNow(tz, _time.GetUtcNow().UtcDateTime));
    }

    public async Task<HabitResult> ArchiveAsync(Guid userId, Guid habitId, CancellationToken cancellationToken = default)
    {
        var habit = await _dbContext.Habits.FirstOrDefaultAsync(h => h.Id == habitId && h.UserId == userId, cancellationToken);
        if (habit is null)
        {
            return HabitResult.Failure(
                StatusCodes.Status404NotFound,
                "Habit not found",
                "The habit was not found or is not accessible.");
        }

        habit.IsArchived = true;
        habit.UpdatedAtUtc = _time.GetUtcNow().UtcDateTime;

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User {UserId} archived habit {HabitId}.", userId, habitId);

        return HabitResult.Success(ToDto(habit, 0, 0));
    }

    public async Task<IReadOnlyList<CalendarDayDto>> GetCalendarAsync(
        Guid userId,
        DateOnly start,
        DateOnly end,
        Guid? habitId,
        CancellationToken cancellationToken = default)
    {
        var tz = await ResolveTimeZoneAsync(userId, cancellationToken);
        var startUtc = TimeZoneInfo.ConvertTimeToUtc(start.ToDateTime(TimeOnly.MinValue), tz);
        var endUtcExclusive = TimeZoneInfo.ConvertTimeToUtc(end.AddDays(1).ToDateTime(TimeOnly.MinValue), tz);

        var logsQuery = _dbContext.HabitLogs
            .AsNoTracking()
            .Where(l => l.CompletedAtUtc >= startUtc && l.CompletedAtUtc < endUtcExclusive && l.UserId == userId);

        if (habitId.HasValue)
        {
            logsQuery = logsQuery.Where(l => l.HabitId == habitId.Value);
        }

        var logRows = await logsQuery
            .Select(l => new { l.HabitId, l.CompletedAtUtc })
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
            .GroupBy(r => DateOnly.FromDateTime(HabitPeriodCalculator.GetLocalNow(tz, r.CompletedAtUtc)))
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

    private async Task<HabitDashboardItemDto> BuildDashboardItemAsync(Guid userId, Habit habit, CancellationToken cancellationToken)
    {
        var now = _time.GetUtcNow().UtcDateTime;
        var tz = await ResolveTimeZoneAsync(habit.UserId, cancellationToken);
        var windowStart = HabitPeriodCalculator.GetWindowStartUtc(habit.Frequency, tz, now);
        var windowEnd = HabitPeriodCalculator.GetWindowEndUtc(habit.Frequency, tz, now);

        var logs = await _dbContext.HabitLogs
            .AsNoTracking()
            .Where(l => l.HabitId == habit.Id && l.UserId == userId)
            .Select(l => l.CompletedAtUtc)
            .ToListAsync(cancellationToken);

        var currentPeriodCount = logs.Count(x => x >= windowStart && x < windowEnd);
        var streak = ComputeStreak(logs, tz, now);

        return ToDto(habit, currentPeriodCount, streak);
    }

    private async Task<TimeZoneInfo> ResolveTimeZoneAsync(Guid userId, CancellationToken cancellationToken)
    {
        var timeZoneId = await _dbContext.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.TimeZoneId)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return TimeZoneInfo.Utc;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Utc;
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }

    private static int ComputeStreak(IEnumerable<DateTime> completedAtUtc, TimeZoneInfo tz, DateTime utcNow)
    {
        var completedLocalDates = completedAtUtc
            .Select(x => DateOnly.FromDateTime(HabitPeriodCalculator.GetLocalNow(tz, x)))
            .Distinct()
            .ToHashSet();

        if (completedLocalDates.Count == 0)
        {
            return 0;
        }

        var localToday = DateOnly.FromDateTime(HabitPeriodCalculator.GetLocalNow(tz, utcNow));
        var cursor = completedLocalDates.Contains(localToday) ? localToday : localToday.AddDays(-1);

        var streak = 0;
        while (completedLocalDates.Contains(cursor))
        {
            streak++;
            cursor = cursor.AddDays(-1);
        }

        return streak;
    }

    private static HabitDashboardItemDto ToDto(Habit habit, int currentPeriodCount, int streak)
        => new()
        {
            Id = habit.Id,
            Title = habit.Title,
            Description = habit.Description,
            ColorHex = habit.ColorHex,
            Frequency = habit.Frequency,
            Period = habit.Period,
            TargetCount = habit.TargetCount,
            IsActive = habit.IsActive,
            CurrentPeriodCount = currentPeriodCount,
            IsCompletedForPeriod = currentPeriodCount >= habit.TargetCount,
            Streak = streak
        };
}
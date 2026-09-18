using HabitsApp.Api.Services;
using HabitsApp.Application.Services;
using HabitsApp.Application.Contracts.Habits;
using HabitsApp.Domain.Entities;
using HabitsApp.Domain.Enums;
using HabitsApp.Infrastructure.Abstractions;
using HabitsApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace HabitsApp.Application.Tests;

public class HabitServiceTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    private sealed class TestCurrentUserService : ICurrentUserService
    {
        private readonly Guid _userId;

        public TestCurrentUserService(Guid userId) => _userId = userId;

        public Guid? UserId => _userId;
    }

    private static ApplicationDbContext CreateContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        var context = new ApplicationDbContext(options, new TestCurrentUserService(UserId));
        context.Database.EnsureCreated();
        return context;
    }

    private static HabitService CreateHabitService(ApplicationDbContext context)
        => new(context, TimeProvider.System, NullLogger<HabitService>.Instance);

    private static HabitService CreateHabitService(ApplicationDbContext context, TimeProvider time)
        => new(context, time, NullLogger<HabitService>.Instance);

    private static ApplicationDbContext CreateScopedContext(string dbName, Guid scopedUserId)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        var context = new ApplicationDbContext(options, new TestCurrentUserService(scopedUserId));
        context.Database.EnsureCreated();
        return context;
    }

    [Fact]
    public async Task CreateAsync_AddsHabitWithOwnershipAndCreatedAt()
    {
        using var context = CreateContext(Guid.NewGuid().ToString());
        var service = CreateHabitService(context);

        var result = await service.CreateAsync(UserId, new CreateHabitDto
        {
            Title = "Meditate",
            Frequency = FrequencyType.Daily,
            TargetCount = 2
        });

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Data);
        Assert.Equal("Meditate", result.Data.Title);
        Assert.Equal(FrequencyType.Daily, result.Data.Frequency);

        var saved = await context.Habits.SingleAsync();
        Assert.Equal(UserId, saved.UserId);
        Assert.False(saved.IsArchived);
        Assert.NotEqual(default, saved.CreatedAtUtc);
    }

    [Fact]
    public async Task GetDashboardAsync_CountsOnlyLogsInCurrentPeriod()
    {
        using var context = CreateContext(Guid.NewGuid().ToString());
        var now = DateTime.UtcNow;

        var habit = new Habit
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            Title = "Run",
            Frequency = FrequencyType.Daily,
            TargetCount = 2,
            CreatedAtUtc = now
        };

        context.Habits.Add(habit);
        context.HabitLogs.AddRange(
            new HabitLog
            {
                Id = Guid.NewGuid(),
                HabitId = habit.Id,
                UserId = UserId,
                CompletedAtUtc = now.AddMinutes(-10),
                PeriodKey = "current"
            },
            new HabitLog
            {
                Id = Guid.NewGuid(),
                HabitId = habit.Id,
                UserId = UserId,
                CompletedAtUtc = now.AddDays(-1),
                PeriodKey = "previous"
            });

        await context.SaveChangesAsync();

        var service = CreateHabitService(context);
        var dashboard = await service.GetDashboardAsync(UserId);
        var items = dashboard.Habits;

        var item = Assert.Single(items);
        Assert.Equal(1, item.CurrentPeriodCount);
        Assert.False(item.IsCompletedForPeriod);
    }

    [Fact]
    public async Task GetDashboardAsync_FlagsCompletedWhenTargetReached()
    {
        using var context = CreateContext(Guid.NewGuid().ToString());
        var now = DateTime.UtcNow;

        var habit = new Habit
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            Title = "Drink Water",
            Frequency = FrequencyType.Daily,
            TargetCount = 1,
            CreatedAtUtc = now
        };

        context.Habits.Add(habit);
        context.HabitLogs.Add(new HabitLog
        {
            Id = Guid.NewGuid(),
            HabitId = habit.Id,
            UserId = UserId,
            CompletedAtUtc = now,
            PeriodKey = "current"
        });

        await context.SaveChangesAsync();

        var service = CreateHabitService(context);
        var dashboard = await service.GetDashboardAsync(UserId);
        var items = dashboard.Habits;

        var item = Assert.Single(items);
        Assert.Equal(1, item.CurrentPeriodCount);
        Assert.True(item.IsCompletedForPeriod);
    }

    [Fact]
    public async Task QuickLogAsync_IsIdempotentForPeriod()
    {
        using var context = CreateContext(Guid.NewGuid().ToString());

        var habit = new Habit
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            Title = "Read",
            Frequency = FrequencyType.Daily,
            TargetCount = 1,
            CreatedAtUtc = DateTime.UtcNow
        };

        context.Habits.Add(habit);
        await context.SaveChangesAsync();

        var service = CreateHabitService(context);

        var first = await service.QuickLogAsync(UserId, habit.Id);
        var second = await service.QuickLogAsync(UserId, habit.Id);

        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        Assert.Equal(1, first.Data!.CurrentPeriodCount);
        Assert.Equal(1, second.Data!.CurrentPeriodCount);
        Assert.Equal(1, await context.HabitLogs.CountAsync());
    }

    [Fact]
    public async Task QuickLogAsync_AllowsMultipleDaysWithinSameWeek_UpToTargetCount()
    {
        using var context = CreateContext(Guid.NewGuid().ToString());
        var now = DateTime.UtcNow;
        var weekStart = HabitPeriodCalculator.GetWindowStartUtc(FrequencyType.Weekly, now);

        var habit = new Habit
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            Title = "Exercise",
            Frequency = FrequencyType.Weekly,
            TargetCount = 3,
            CreatedAtUtc = now
        };
        context.Habits.Add(habit);

        var seedDays = Enumerable.Range(0, 7)
            .Select(offset => weekStart.AddDays(offset).Date)
            .Where(day => day != now.Date)
            .Take(2)
            .ToArray();

        context.HabitLogs.AddRange(seedDays.Select(day => new HabitLog
        {
            Id = Guid.NewGuid(),
            HabitId = habit.Id,
            UserId = UserId,
            CompletedAtUtc = day.AddHours(12),
            PeriodKey = HabitPeriodCalculator.GetDayKey(day)
        }));
        await context.SaveChangesAsync();

        var service = CreateHabitService(context);
        var result = await service.QuickLogAsync(UserId, habit.Id);

        Assert.True(result.Succeeded);
        Assert.Equal(3, result.Data!.CurrentPeriodCount);
        Assert.True(result.Data.IsCompletedForPeriod);
        Assert.Equal(3, await context.HabitLogs.CountAsync(l => l.HabitId == habit.Id));
    }

    [Fact]
    public async Task QuickLogAsync_SameDayDoubleClick_IsIdempotent_ForWeeklyHabit()
    {
        using var context = CreateContext(Guid.NewGuid().ToString());
        var now = DateTime.UtcNow;

        var habit = new Habit
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            Title = "Exercise",
            Frequency = FrequencyType.Weekly,
            TargetCount = 3,
            CreatedAtUtc = now
        };
        context.Habits.Add(habit);
        await context.SaveChangesAsync();

        var service = CreateHabitService(context);
        var first = await service.QuickLogAsync(UserId, habit.Id);
        var second = await service.QuickLogAsync(UserId, habit.Id);

        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        Assert.Equal(1, first.Data!.CurrentPeriodCount);
        Assert.Equal(1, second.Data!.CurrentPeriodCount);
        Assert.Equal(1, await context.HabitLogs.CountAsync(l => l.HabitId == habit.Id));
    }

    [Fact]
    public async Task QuickLogAsync_NoOpsOnceTargetReached_EvenOnNewDayWithinSamePeriod()
    {
        using var context = CreateContext(Guid.NewGuid().ToString());
        var now = DateTime.UtcNow;
        var weekStart = HabitPeriodCalculator.GetWindowStartUtc(FrequencyType.Weekly, now);

        var habit = new Habit
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            Title = "Exercise",
            Frequency = FrequencyType.Weekly,
            TargetCount = 2,
            CreatedAtUtc = now
        };
        context.Habits.Add(habit);

        var seedDays = Enumerable.Range(0, 7)
            .Select(offset => weekStart.AddDays(offset).Date)
            .Where(day => day != now.Date)
            .Take(2)
            .ToArray();

        context.HabitLogs.AddRange(seedDays.Select(day => new HabitLog
        {
            Id = Guid.NewGuid(),
            HabitId = habit.Id,
            UserId = UserId,
            CompletedAtUtc = day.AddHours(12),
            PeriodKey = HabitPeriodCalculator.GetDayKey(day)
        }));
        await context.SaveChangesAsync();

        var service = CreateHabitService(context);
        var result = await service.QuickLogAsync(UserId, habit.Id);

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.Data!.CurrentPeriodCount);
        Assert.True(result.Data.IsCompletedForPeriod);
        Assert.Equal(2, await context.HabitLogs.CountAsync(l => l.HabitId == habit.Id));
    }

    [Fact]
    public async Task QuickLogAsync_AllowsMultipleDaysWithinSameMonth_UpToTargetCount()
    {
        using var context = CreateContext(Guid.NewGuid().ToString());
        var now = DateTime.UtcNow;
        var monthStart = HabitPeriodCalculator.GetWindowStartUtc(FrequencyType.Monthly, now);

        var habit = new Habit
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            Title = "Gym",
            Frequency = FrequencyType.Monthly,
            TargetCount = 2,
            CreatedAtUtc = now
        };
        context.Habits.Add(habit);
        await context.SaveChangesAsync();

        var daysInMonth = DateTime.DaysInMonth(monthStart.Year, monthStart.Month);
        var seedDay = Enumerable.Range(0, daysInMonth)
            .Select(offset => monthStart.AddDays(offset).Date)
            .First(day => day != now.Date);

        context.HabitLogs.Add(new HabitLog
        {
            Id = Guid.NewGuid(),
            HabitId = habit.Id,
            UserId = UserId,
            CompletedAtUtc = seedDay.AddHours(12),
            PeriodKey = HabitPeriodCalculator.GetDayKey(seedDay)
        });
        await context.SaveChangesAsync();

        var service = CreateHabitService(context);
        var result = await service.QuickLogAsync(UserId, habit.Id);

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.Data!.CurrentPeriodCount);
        Assert.True(result.Data.IsCompletedForPeriod);
        Assert.Equal(2, await context.HabitLogs.CountAsync(l => l.HabitId == habit.Id));
    }

    [Fact]
    public async Task QuickLogAsync_ReturnsNotFound_ForOtherUsersHabit()
    {
        using var context = CreateContext(Guid.NewGuid().ToString());

        var otherUser = Guid.NewGuid();
        var habit = new Habit
        {
            Id = Guid.NewGuid(),
            UserId = otherUser,
            Title = "Secret",
            Frequency = FrequencyType.Daily,
            TargetCount = 1,
            CreatedAtUtc = DateTime.UtcNow
        };

        context.Habits.Add(habit);
        await context.SaveChangesAsync();

        var service = CreateHabitService(context);
        var result = await service.QuickLogAsync(UserId, habit.Id);

        Assert.False(result.Succeeded);
        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public async Task QuickLogAsync_TargetCountGreaterThanOne_IncrementsAcrossHours()
    {
        using var context = CreateContext(Guid.NewGuid().ToString());
        var now = DateTime.UtcNow;

        var habit = new Habit
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            Title = "Brush Teeth",
            Frequency = FrequencyType.Daily,
            TargetCount = 3,
            CreatedAtUtc = now
        };

        var seedTime = now.Hour == 0 ? now.Date.AddHours(8) : now.AddHours(-1);
        context.Habits.Add(habit);
        context.HabitLogs.Add(new HabitLog
        {
            Id = Guid.NewGuid(),
            HabitId = habit.Id,
            UserId = UserId,
            CompletedAtUtc = seedTime,
            PeriodKey = HabitPeriodCalculator.GetPeriodKey(habit.Frequency, seedTime),
            HourKey = HabitPeriodCalculator.GetHourKey(seedTime)
        });
        await context.SaveChangesAsync();

        var service = CreateHabitService(context);
        var result = await service.QuickLogAsync(UserId, habit.Id);

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.Data!.CurrentPeriodCount);
        Assert.True(result.Data.CurrentPeriodCount < result.Data.TargetCount);
        Assert.Equal(2, await context.HabitLogs.CountAsync());
    }

    [Fact]
    public async Task QuickLogAsync_DeduplicatesSameHour()
    {
        using var context = CreateContext(Guid.NewGuid().ToString());

        var habit = new Habit
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            Title = "Brush Teeth",
            Frequency = FrequencyType.Daily,
            TargetCount = 3,
            CreatedAtUtc = DateTime.UtcNow
        };

        context.Habits.Add(habit);
        await context.SaveChangesAsync();

        var service = CreateHabitService(context);

        var first = await service.QuickLogAsync(UserId, habit.Id);
        var second = await service.QuickLogAsync(UserId, habit.Id);

        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        Assert.Equal(1, first.Data!.CurrentPeriodCount);
        Assert.Equal(1, second.Data!.CurrentPeriodCount);
        Assert.Equal(1, await context.HabitLogs.CountAsync());
    }

    [Fact]
    public async Task QuickLogAsync_StopsAtTargetCount()
    {
        using var context = CreateContext(Guid.NewGuid().ToString());
        var now = DateTime.UtcNow;

        var habit = new Habit
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            Title = "Brush Teeth",
            Frequency = FrequencyType.Daily,
            TargetCount = 2,
            CreatedAtUtc = now
        };

        var seedTime = now.Hour == 0 ? now.Date.AddHours(8) : now.AddHours(-1);
        var earlierSeed = seedTime.AddHours(-1);
        context.Habits.Add(habit);
        context.HabitLogs.AddRange(
            new HabitLog
            {
                Id = Guid.NewGuid(),
                HabitId = habit.Id,
                UserId = UserId,
                CompletedAtUtc = seedTime,
                PeriodKey = HabitPeriodCalculator.GetPeriodKey(habit.Frequency, seedTime),
                HourKey = HabitPeriodCalculator.GetHourKey(seedTime)
            },
            new HabitLog
            {
                Id = Guid.NewGuid(),
                HabitId = habit.Id,
                UserId = UserId,
                CompletedAtUtc = earlierSeed,
                PeriodKey = HabitPeriodCalculator.GetPeriodKey(habit.Frequency, earlierSeed),
                HourKey = HabitPeriodCalculator.GetHourKey(earlierSeed)
            });
        await context.SaveChangesAsync();

        var service = CreateHabitService(context);
        var result = await service.QuickLogAsync(UserId, habit.Id);

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.Data!.CurrentPeriodCount);
        Assert.True(result.Data.IsCompletedForPeriod);
        Assert.Equal(2, await context.HabitLogs.CountAsync());
    }

    [Fact]
    public async Task UpdateAsync_ReturnsNotFound_ForOtherUsersHabit()
    {
        using var context = CreateContext(Guid.NewGuid().ToString());

        var otherUser = Guid.NewGuid();
        var habit = new Habit
        {
            Id = Guid.NewGuid(),
            UserId = otherUser,
            Title = "Secret",
            Frequency = FrequencyType.Daily,
            TargetCount = 1,
            CreatedAtUtc = DateTime.UtcNow
        };

        context.Habits.Add(habit);
        await context.SaveChangesAsync();

        var service = CreateHabitService(context);
        var result = await service.UpdateAsync(UserId, habit.Id, new UpdateHabitDto
        {
            Title = "Tampered",
            Frequency = FrequencyType.Daily,
            TargetCount = 1
        });

        Assert.False(result.Succeeded);
        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public async Task ArchiveAsync_ReturnsNotFound_ForOtherUsersHabit()
    {
        using var context = CreateContext(Guid.NewGuid().ToString());

        var otherUser = Guid.NewGuid();
        var habit = new Habit
        {
            Id = Guid.NewGuid(),
            UserId = otherUser,
            Title = "Secret",
            Frequency = FrequencyType.Daily,
            TargetCount = 1,
            CreatedAtUtc = DateTime.UtcNow
        };

        context.Habits.Add(habit);
        await context.SaveChangesAsync();

        var service = CreateHabitService(context);
        var result = await service.ArchiveAsync(UserId, habit.Id);

        Assert.False(result.Succeeded);
        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesFieldsAndSetsUpdatedAt()
    {
        using var context = CreateContext(Guid.NewGuid().ToString());

        var habit = new Habit
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            Title = "Old Title",
            Frequency = FrequencyType.Daily,
            TargetCount = 1,
            CreatedAtUtc = DateTime.UtcNow
        };

        context.Habits.Add(habit);
        await context.SaveChangesAsync();

        var service = CreateHabitService(context);
        var result = await service.UpdateAsync(UserId, habit.Id, new UpdateHabitDto
        {
            Title = "New Title",
            Frequency = FrequencyType.Weekly,
            TargetCount = 3
        });

        Assert.True(result.Succeeded);
        Assert.Equal("New Title", result.Data!.Title);
        Assert.Equal(FrequencyType.Weekly, result.Data.Frequency);
        Assert.Equal(3, result.Data.TargetCount);

        var saved = await context.Habits.SingleAsync();
        Assert.NotNull(saved.UpdatedAtUtc);
    }

    [Fact]
    public async Task ArchiveAsync_ArchivesHabit_WithoutAffectingIsActiveDashboardFilter()
    {
        using var context = CreateContext(Guid.NewGuid().ToString());

        var habit = new Habit
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            Title = "Gym",
            Frequency = FrequencyType.Daily,
            TargetCount = 1,
            CreatedAtUtc = DateTime.UtcNow
        };

        context.Habits.Add(habit);
        await context.SaveChangesAsync();

        var service = CreateHabitService(context);
        var result = await service.ArchiveAsync(UserId, habit.Id);

        Assert.True(result.Succeeded);

        var archived = await context.Habits.SingleAsync();
        Assert.True(archived.IsArchived);
        Assert.True(archived.IsActive);

        var dashboard = await service.GetDashboardAsync(UserId);
        Assert.Single(dashboard.Habits);
        Assert.True(Assert.Single(dashboard.Habits).IsActive);
    }

    [Fact]
    public async Task GetCalendarAsync_GroupsColorsByDayAndDedupes()
    {
        using var context = CreateContext(Guid.NewGuid().ToString());
        var now = new DateTime(2026, 2, 10, 12, 0, 0, DateTimeKind.Utc);

        var red = new Habit
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            Title = "Read",
            ColorHex = "#EF4444",
            Frequency = FrequencyType.Daily,
            TargetCount = 1,
            CreatedAtUtc = now
        };
        var blue = new Habit
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            Title = "Run",
            ColorHex = "#3B82F6",
            Frequency = FrequencyType.Daily,
            TargetCount = 1,
            CreatedAtUtc = now
        };
        var redClone = new Habit
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            Title = "Write",
            ColorHex = "#EF4444",
            Frequency = FrequencyType.Daily,
            TargetCount = 1,
            CreatedAtUtc = now
        };

        context.Habits.AddRange(red, blue, redClone);
        context.HabitLogs.AddRange(
            new HabitLog { Id = Guid.NewGuid(), HabitId = red.Id, UserId = UserId, CompletedAtUtc = now, PeriodKey = "2026-02-10" },
            new HabitLog { Id = Guid.NewGuid(), HabitId = blue.Id, UserId = UserId, CompletedAtUtc = now, PeriodKey = "2026-02-10" },
            new HabitLog { Id = Guid.NewGuid(), HabitId = redClone.Id, UserId = UserId, CompletedAtUtc = now, PeriodKey = "2026-02-10" },
            new HabitLog { Id = Guid.NewGuid(), HabitId = red.Id, UserId = UserId, CompletedAtUtc = now.AddDays(1), PeriodKey = "2026-02-11" });

        await context.SaveChangesAsync();

        var service = CreateHabitService(context);
        var days = await service.GetCalendarAsync(UserId, new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 28), null);

        Assert.Equal(2, days.Count);

        var first = days[0];
        Assert.Equal(new DateOnly(2026, 2, 10), first.Date);
        Assert.Equal(2, first.Colors.Count);
        Assert.Contains("#EF4444", first.Colors);
        Assert.Contains("#3B82F6", first.Colors);

        var second = days[1];
        Assert.Equal(new DateOnly(2026, 2, 11), second.Date);
        Assert.Single(second.Colors);
    }

    [Fact]
    public async Task GetCalendarAsync_FiltersByHabitId()
    {
        using var context = CreateContext(Guid.NewGuid().ToString());
        var now = new DateTime(2026, 2, 10, 12, 0, 0, DateTimeKind.Utc);

        var red = new Habit
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            Title = "Read",
            ColorHex = "#EF4444",
            Frequency = FrequencyType.Daily,
            TargetCount = 1,
            CreatedAtUtc = now
        };
        var blue = new Habit
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            Title = "Run",
            ColorHex = "#3B82F6",
            Frequency = FrequencyType.Daily,
            TargetCount = 1,
            CreatedAtUtc = now
        };

        context.Habits.AddRange(red, blue);
        context.HabitLogs.AddRange(
            new HabitLog { Id = Guid.NewGuid(), HabitId = red.Id, UserId = UserId, CompletedAtUtc = now, PeriodKey = "2026-02-10" },
            new HabitLog { Id = Guid.NewGuid(), HabitId = blue.Id, UserId = UserId, CompletedAtUtc = now, PeriodKey = "2026-02-10" });

        await context.SaveChangesAsync();

        var service = CreateHabitService(context);
        var days = await service.GetCalendarAsync(UserId, new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 28), red.Id);

        var day = Assert.Single(days);
        Assert.Equal(new DateOnly(2026, 2, 10), day.Date);
        Assert.Single(day.Colors);
        Assert.Equal("#EF4444", Assert.Single(day.Colors));
    }

    [Fact]
    public async Task GetCalendarAsync_RespectsRangeBoundaries()
    {
        using var context = CreateContext(Guid.NewGuid().ToString());
        var now = new DateTime(2026, 2, 10, 12, 0, 0, DateTimeKind.Utc);

        var habit = new Habit
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            Title = "Read",
            ColorHex = "#EF4444",
            Frequency = FrequencyType.Daily,
            TargetCount = 1,
            CreatedAtUtc = now
        };

        context.Habits.Add(habit);
        context.HabitLogs.AddRange(
            new HabitLog { Id = Guid.NewGuid(), HabitId = habit.Id, UserId = UserId, CompletedAtUtc = now, PeriodKey = "2026-02-10" },
            new HabitLog { Id = Guid.NewGuid(), HabitId = habit.Id, UserId = UserId, CompletedAtUtc = now.AddMonths(1), PeriodKey = "2026-03-10" });

        await context.SaveChangesAsync();

        var service = CreateHabitService(context);
        var days = await service.GetCalendarAsync(UserId, new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 28), null);

        var day = Assert.Single(days);
        Assert.Equal(new DateOnly(2026, 2, 10), day.Date);
    }

    [Fact]
    public async Task GetDashboardAsync_ComputesStreakForConsecutiveDays()
    {
        using var context = CreateContext(Guid.NewGuid().ToString());
        var now = DateTime.UtcNow;

        var habit = new Habit
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            Title = "Meditate",
            Frequency = FrequencyType.Daily,
            TargetCount = 1,
            CreatedAtUtc = now
        };

        context.Habits.Add(habit);
        context.HabitLogs.AddRange(
            new HabitLog { Id = Guid.NewGuid(), HabitId = habit.Id, UserId = UserId, CompletedAtUtc = now, PeriodKey = "today" },
            new HabitLog { Id = Guid.NewGuid(), HabitId = habit.Id, UserId = UserId, CompletedAtUtc = now.AddDays(-1), PeriodKey = "yesterday" },
            new HabitLog { Id = Guid.NewGuid(), HabitId = habit.Id, UserId = UserId, CompletedAtUtc = now.AddDays(-2), PeriodKey = "day-before" },
            new HabitLog { Id = Guid.NewGuid(), HabitId = habit.Id, UserId = UserId, CompletedAtUtc = now.AddDays(-4), PeriodKey = "gap-beyond" });

        await context.SaveChangesAsync();

        var service = CreateHabitService(context);
        var dashboard = await service.GetDashboardAsync(UserId);
        var items = dashboard.Habits;

        var item = Assert.Single(items);
        Assert.Equal(3, item.Streak);
    }

    [Fact]
    public async Task GetDashboardAsync_StreakBreaks_WhenALocalDayIsSkipped()
    {
        using var context = CreateContext(Guid.NewGuid().ToString());
        var now = DateTime.UtcNow;

        var habit = new Habit
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            Title = "Run",
            Frequency = FrequencyType.Daily,
            TargetCount = 1,
            CreatedAtUtc = now
        };

        context.Habits.Add(habit);
        context.HabitLogs.AddRange(
            new HabitLog { Id = Guid.NewGuid(), HabitId = habit.Id, UserId = UserId, CompletedAtUtc = now, PeriodKey = "today" },
            new HabitLog { Id = Guid.NewGuid(), HabitId = habit.Id, UserId = UserId, CompletedAtUtc = now.AddDays(-2), PeriodKey = "gap" });

        await context.SaveChangesAsync();

        var service = CreateHabitService(context);
        var dashboard = await service.GetDashboardAsync(UserId);
        var items = dashboard.Habits;

        var item = Assert.Single(items);
        Assert.Equal(1, item.Streak);
    }

    [Fact]
    public async Task GetDashboardAsync_StreakStartsFromYesterday_WhenTodayNotCompleted()
    {
        using var context = CreateContext(Guid.NewGuid().ToString());
        var now = DateTime.UtcNow;

        var habit = new Habit
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            Title = "Write",
            Frequency = FrequencyType.Daily,
            TargetCount = 1,
            CreatedAtUtc = now
        };

        context.Habits.Add(habit);
        context.HabitLogs.AddRange(
            new HabitLog { Id = Guid.NewGuid(), HabitId = habit.Id, UserId = UserId, CompletedAtUtc = now.AddDays(-1), PeriodKey = "yesterday" },
            new HabitLog { Id = Guid.NewGuid(), HabitId = habit.Id, UserId = UserId, CompletedAtUtc = now.AddDays(-2), PeriodKey = "day-before" },
            new HabitLog { Id = Guid.NewGuid(), HabitId = habit.Id, UserId = UserId, CompletedAtUtc = now.AddDays(-3), PeriodKey = "three-days" });

        await context.SaveChangesAsync();

        var service = CreateHabitService(context);
        var dashboard = await service.GetDashboardAsync(UserId);
        var items = dashboard.Habits;

        var item = Assert.Single(items);
        Assert.Equal(3, item.Streak);
    }

    [Fact]
    public async Task GetDashboardAsync_StreakIsZero_WhenNoLogsExist()
    {
        using var context = CreateContext(Guid.NewGuid().ToString());
        var now = DateTime.UtcNow;

        var habit = new Habit
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            Title = "Read",
            Frequency = FrequencyType.Daily,
            TargetCount = 1,
            CreatedAtUtc = now
        };

        context.Habits.Add(habit);
        await context.SaveChangesAsync();

        var service = CreateHabitService(context);
        var dashboard = await service.GetDashboardAsync(UserId);
        var items = dashboard.Habits;

        var item = Assert.Single(items);
        Assert.Equal(0, item.Streak);
    }

    [Fact]
    public async Task GetDashboardAsync_ReturnsActiveHabits_ByDefault()
    {
        using var context = CreateContext(Guid.NewGuid().ToString());
        var now = DateTime.UtcNow;

        context.Habits.AddRange(
            new Habit
            {
                Id = Guid.NewGuid(),
                UserId = UserId,
                Title = "Active habit",
                Frequency = FrequencyType.Daily,
                TargetCount = 1,
                CreatedAtUtc = now
            },
            new Habit
            {
                Id = Guid.NewGuid(),
                UserId = UserId,
                Title = "Inactive habit",
                Frequency = FrequencyType.Daily,
                TargetCount = 1,
                IsActive = false,
                CreatedAtUtc = now
            });
        await context.SaveChangesAsync();

        var service = CreateHabitService(context);
        var dashboard = await service.GetDashboardAsync(UserId);
        var items = dashboard.Habits;

        Assert.Single(items);
        Assert.True(Assert.Single(items).IsActive);
    }

    [Fact]
    public async Task GetDashboardAsync_InactiveFilter_ReturnsOnlyInactiveHabits()
    {
        using var context = CreateContext(Guid.NewGuid().ToString());
        var now = DateTime.UtcNow;

        context.Habits.AddRange(
            new Habit
            {
                Id = Guid.NewGuid(),
                UserId = UserId,
                Title = "Active habit",
                Frequency = FrequencyType.Daily,
                TargetCount = 1,
                CreatedAtUtc = now
            },
            new Habit
            {
                Id = Guid.NewGuid(),
                UserId = UserId,
                Title = "Inactive habit",
                Frequency = FrequencyType.Daily,
                TargetCount = 1,
                IsActive = false,
                CreatedAtUtc = now
            });
        await context.SaveChangesAsync();

        var service = CreateHabitService(context);
        var dashboard = await service.GetDashboardAsync(UserId, false);
        var items = dashboard.Habits;

        Assert.Single(items);
        var item = Assert.Single(items);
        Assert.False(item.IsActive);
        Assert.Equal("Inactive habit", item.Title);
    }

    [Fact]
    public async Task GetDashboardAsync_NeverReturnsAnotherUsersHabits()
    {
        using var context = CreateContext(Guid.NewGuid().ToString());

        var otherUser = Guid.NewGuid();
        context.Habits.Add(new Habit
        {
            Id = Guid.NewGuid(),
            UserId = otherUser,
            Title = "Secret",
            Frequency = FrequencyType.Daily,
            TargetCount = 1,
            CreatedAtUtc = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var service = CreateHabitService(context);
        var dashboard = await service.GetDashboardAsync(UserId);
        var items = dashboard.Habits;

        Assert.Empty(items);
    }

    [Fact]
    public async Task IsActive_ColumnIsNonNullableWithDefaultTrue_AndNewHabitsAreActive()
    {
        using var context = CreateContext(Guid.NewGuid().ToString());

        var entity = context.Model.FindEntityType("HabitsApp.Domain.Entities.Habit");
        Assert.NotNull(entity);
        Assert.False(entity.GetProperty("IsActive").IsNullable);

        var service = CreateHabitService(context);
        var result = await service.CreateAsync(UserId, new CreateHabitDto
        {
            Title = "Meditate",
            Frequency = FrequencyType.Daily,
            TargetCount = 1
        });

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Data);
        Assert.True(result.Data.IsActive);
        Assert.True((await context.Habits.SingleAsync()).IsActive);
    }

    [Fact]
    public async Task InactivateAsync_SetsInactive_KeepsLogs_AndDoesNotChangeIsArchived()
    {
        using var context = CreateContext(Guid.NewGuid().ToString());
        var now = DateTime.UtcNow;

        var habit = new Habit
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            Title = "Gym",
            Frequency = FrequencyType.Daily,
            TargetCount = 1,
            IsArchived = false,
            CreatedAtUtc = now
        };
        context.Habits.Add(habit);
        context.HabitLogs.Add(new HabitLog
        {
            Id = Guid.NewGuid(),
            HabitId = habit.Id,
            UserId = UserId,
            CompletedAtUtc = now.AddDays(-1),
            PeriodKey = "yesterday"
        });
        await context.SaveChangesAsync();

        var service = CreateHabitService(context);
        var result = await service.InactivateAsync(UserId, habit.Id);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Data);
        Assert.False(result.Data.IsActive);

        var saved = await context.Habits.SingleAsync();
        Assert.False(saved.IsActive);
        Assert.False(saved.IsArchived);
        Assert.NotNull(saved.UpdatedAtUtc);
        Assert.Equal(1, await context.HabitLogs.CountAsync());
    }

    [Fact]
    public async Task InactivateAsync_RepeatedCall_IsIdempotentNoOp_WithoutChangingTimestampOrLogs()
    {
        using var context = CreateContext(Guid.NewGuid().ToString());
        var now = DateTime.UtcNow;

        var habit = new Habit
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            Title = "Gym",
            Frequency = FrequencyType.Daily,
            TargetCount = 1,
            IsArchived = false,
            CreatedAtUtc = now
        };
        context.Habits.Add(habit);
        context.HabitLogs.Add(new HabitLog
        {
            Id = Guid.NewGuid(),
            HabitId = habit.Id,
            UserId = UserId,
            CompletedAtUtc = now.AddDays(-1),
            PeriodKey = "yesterday"
        });
        await context.SaveChangesAsync();

        var service = CreateHabitService(context);
        var first = await service.InactivateAsync(UserId, habit.Id);
        var updatedAtAfterFirst = (await context.Habits.SingleAsync()).UpdatedAtUtc;

        var second = await service.InactivateAsync(UserId, habit.Id);

        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);

        var saved = await context.Habits.SingleAsync();
        Assert.False(saved.IsActive);
        Assert.Equal(updatedAtAfterFirst, saved.UpdatedAtUtc);
        Assert.False(saved.IsArchived);
        Assert.Equal(1, await context.HabitLogs.CountAsync());
    }

    [Fact]
    public async Task InactivateAsync_ReturnsNotFound_ForOtherUsersHabit_WithoutModifyingIt()
    {
        var otherUser = Guid.NewGuid();
        using var context = CreateScopedContext(Guid.NewGuid().ToString(), otherUser);

        var habit = new Habit
        {
            Id = Guid.NewGuid(),
            UserId = otherUser,
            Title = "Secret",
            Frequency = FrequencyType.Daily,
            TargetCount = 1,
            IsArchived = false,
            CreatedAtUtc = DateTime.UtcNow
        };
        context.Habits.Add(habit);
        await context.SaveChangesAsync();

        var service = CreateHabitService(context);
        var result = await service.InactivateAsync(UserId, habit.Id);

        Assert.False(result.Succeeded);
        Assert.Equal(404, result.StatusCode);

        var other = await context.Habits.SingleAsync();
        Assert.True(other.IsActive);
        Assert.False(other.IsArchived);
        Assert.Null(other.UpdatedAtUtc);
        Assert.Equal(0, await context.HabitLogs.CountAsync());
    }

    [Fact]
    public async Task ReactivateAsync_SetsActive_KeepsLogs_DoesNotChangeIsArchived_AndReturnsProgress()
    {
        using var context = CreateContext(Guid.NewGuid().ToString());
        var now = DateTime.UtcNow;

        var habit = new Habit
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            Title = "Gym",
            Frequency = FrequencyType.Daily,
            TargetCount = 2,
            IsActive = false,
            IsArchived = false,
            CreatedAtUtc = now
        };
        context.Habits.Add(habit);
        context.HabitLogs.Add(new HabitLog
        {
            Id = Guid.NewGuid(),
            HabitId = habit.Id,
            UserId = UserId,
            CompletedAtUtc = now,
            PeriodKey = "current"
        });
        await context.SaveChangesAsync();

        var service = CreateHabitService(context);
        var result = await service.ReactivateAsync(UserId, habit.Id);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Data);
        Assert.True(result.Data.IsActive);
        Assert.Equal(1, result.Data.CurrentPeriodCount);

        var saved = await context.Habits.SingleAsync();
        Assert.True(saved.IsActive);
        Assert.False(saved.IsArchived);
        Assert.NotNull(saved.UpdatedAtUtc);
        Assert.Equal(1, await context.HabitLogs.CountAsync());
    }

    [Fact]
    public async Task ReactivateAsync_ReturnsNotFound_ForOtherUsersHabit_WithoutChangingTheirState()
    {
        var otherUser = Guid.NewGuid();
        using var context = CreateScopedContext(Guid.NewGuid().ToString(), otherUser);

        var updatedAt = DateTime.UtcNow.AddDays(-2);
        var habit = new Habit
        {
            Id = Guid.NewGuid(),
            UserId = otherUser,
            Title = "Secret",
            Frequency = FrequencyType.Daily,
            TargetCount = 1,
            IsActive = false,
            IsArchived = true,
            UpdatedAtUtc = updatedAt,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-10)
        };
        context.Habits.Add(habit);
        context.HabitLogs.Add(new HabitLog
        {
            Id = Guid.NewGuid(),
            HabitId = habit.Id,
            UserId = otherUser,
            CompletedAtUtc = updatedAt,
            PeriodKey = "old"
        });
        await context.SaveChangesAsync();

        var service = CreateHabitService(context);
        var result = await service.ReactivateAsync(UserId, habit.Id);

        Assert.False(result.Succeeded);
        Assert.Equal(404, result.StatusCode);

        var other = await context.Habits.SingleAsync();
        Assert.False(other.IsActive);
        Assert.True(other.IsArchived);
        Assert.Equal(updatedAt, other.UpdatedAtUtc);
        Assert.Equal(1, await context.HabitLogs.CountAsync());
    }

    [Fact]
    public async Task UpdateAsync_ReturnsConflict_ForOwnedInactiveHabit_AndDoesNotApplyChanges()
    {
        using var context = CreateContext(Guid.NewGuid().ToString());

        var habit = new Habit
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            Title = "Old Title",
            Frequency = FrequencyType.Daily,
            TargetCount = 1,
            IsActive = false,
            CreatedAtUtc = DateTime.UtcNow
        };
        context.Habits.Add(habit);
        await context.SaveChangesAsync();

        var service = CreateHabitService(context);
        var result = await service.UpdateAsync(UserId, habit.Id, new UpdateHabitDto
        {
            Title = "Tampered",
            Frequency = FrequencyType.Daily,
            TargetCount = 1
        });

        Assert.False(result.Succeeded);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal("Old Title", (await context.Habits.SingleAsync()).Title);
    }

    [Fact]
    public async Task QuickLogAsync_ReturnsNotFound_ForInactiveHabitOfAnotherUser_WithoutRevealingState()
    {
        using var context = CreateContext(Guid.NewGuid().ToString());

        var otherUser = Guid.NewGuid();
        var habit = new Habit
        {
            Id = Guid.NewGuid(),
            UserId = otherUser,
            Title = "Secret",
            Frequency = FrequencyType.Daily,
            TargetCount = 1,
            IsActive = false,
            CreatedAtUtc = DateTime.UtcNow
        };
        context.Habits.Add(habit);
        await context.SaveChangesAsync();

        var service = CreateHabitService(context);
        var result = await service.QuickLogAsync(UserId, habit.Id);

        Assert.False(result.Succeeded);
        Assert.Equal(404, result.StatusCode);
        Assert.Equal(0, await context.HabitLogs.CountAsync());
    }

    [Fact]
    public async Task QuickLogAsync_ReturnsConflict_ForOwnedInactiveHabit_AndPerformsNoWork()
    {
        using var context = CreateContext(Guid.NewGuid().ToString());

        var habit = new Habit
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            Title = "Gym",
            Frequency = FrequencyType.Daily,
            TargetCount = 1,
            IsActive = false,
            CreatedAtUtc = DateTime.UtcNow
        };
        context.Habits.Add(habit);
        await context.SaveChangesAsync();

        var service = CreateHabitService(context);
        var result = await service.QuickLogAsync(UserId, habit.Id);

        Assert.False(result.Succeeded);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal(0, await context.HabitLogs.CountAsync());
    }

    [Fact]
    public async Task CreateAsync_PersistsPeriod()
    {
        using var context = CreateContext(Guid.NewGuid().ToString());
        var service = CreateHabitService(context);

        var result = await service.CreateAsync(UserId, new CreateHabitDto
        {
            Title = "Meditate",
            Frequency = FrequencyType.Daily,
            Period = DayPeriod.Morning,
            TargetCount = 1
        });

        Assert.True(result.Succeeded);
        Assert.Equal(DayPeriod.Morning, result.Data!.Period);
        Assert.Equal(DayPeriod.Morning, (await context.Habits.SingleAsync()).Period);
    }

    [Fact]
    public async Task CreateAsync_LeavesPeriodNull_WhenAny()
    {
        using var context = CreateContext(Guid.NewGuid().ToString());
        var service = CreateHabitService(context);

        var result = await service.CreateAsync(UserId, new CreateHabitDto
        {
            Title = "Meditate",
            Frequency = FrequencyType.Daily,
            TargetCount = 1
        });

        Assert.True(result.Succeeded);
        Assert.Null(result.Data!.Period);
        Assert.Null((await context.Habits.SingleAsync()).Period);
    }

    [Fact]
    public async Task CreateAsync_UndefinedPeriod_ReturnsBadRequest()
    {
        using var context = CreateContext(Guid.NewGuid().ToString());
        var service = CreateHabitService(context);

        var result = await service.CreateAsync(UserId, new CreateHabitDto
        {
            Title = "Meditate",
            Frequency = FrequencyType.Daily,
            Period = (DayPeriod)999,
            TargetCount = 1
        });

        Assert.False(result.Succeeded);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal(0, await context.Habits.CountAsync());
    }

    [Fact]
    public async Task UpdateAsync_PersistsPeriod()
    {
        using var context = CreateContext(Guid.NewGuid().ToString());

        var habit = new Habit
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            Title = "Old Title",
            Frequency = FrequencyType.Daily,
            TargetCount = 1,
            CreatedAtUtc = DateTime.UtcNow
        };
        context.Habits.Add(habit);
        await context.SaveChangesAsync();

        var service = CreateHabitService(context);
        var result = await service.UpdateAsync(UserId, habit.Id, new UpdateHabitDto
        {
            Title = "New Title",
            Frequency = FrequencyType.Daily,
            Period = DayPeriod.Night,
            TargetCount = 1
        });

        Assert.True(result.Succeeded);
        Assert.Equal(DayPeriod.Night, result.Data!.Period);
        Assert.Equal(DayPeriod.Night, (await context.Habits.SingleAsync()).Period);
    }

    [Fact]
    public async Task UpdateAsync_UndefinedPeriod_ReturnsBadRequest_WithoutApplyingChanges()
    {
        using var context = CreateContext(Guid.NewGuid().ToString());

        var habit = new Habit
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            Title = "Old Title",
            Frequency = FrequencyType.Daily,
            TargetCount = 1,
            CreatedAtUtc = DateTime.UtcNow
        };
        context.Habits.Add(habit);
        await context.SaveChangesAsync();

        var service = CreateHabitService(context);
        var result = await service.UpdateAsync(UserId, habit.Id, new UpdateHabitDto
        {
            Title = "Tampered",
            Frequency = FrequencyType.Daily,
            Period = (DayPeriod)999,
            TargetCount = 1
        });

        Assert.False(result.Succeeded);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal("Old Title", (await context.Habits.SingleAsync()).Title);
    }

    [Fact]
    public async Task GetDashboardAsync_ReturnsCurrentPeriodInRootAndPeriodInEachItem()
    {
        using var context = CreateContext(Guid.NewGuid().ToString());
        var now = new DateTime(2026, 8, 19, 9, 0, 0, DateTimeKind.Utc);

        context.Habits.Add(new Habit
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            Title = "Morning jog",
            Frequency = FrequencyType.Daily,
            Period = DayPeriod.Morning,
            TargetCount = 1,
            CreatedAtUtc = now
        });
        await context.SaveChangesAsync();

        var service = CreateHabitService(context, new FakeTimeProvider(new DateTimeOffset(2026, 8, 19, 9, 0, 0, TimeSpan.Zero)));
        var dashboard = await service.GetDashboardAsync(UserId);

        Assert.Equal(DayPeriod.Morning, dashboard.CurrentPeriod);
        var item = Assert.Single(dashboard.Habits);
        Assert.Equal(DayPeriod.Morning, item.Period);
    }

    [Fact]
    public async Task GetDashboardAsync_EmptyDashboard_ReturnsRootResponseWithCurrentPeriod()
    {
        using var context = CreateContext(Guid.NewGuid().ToString());

        var service = CreateHabitService(context, new FakeTimeProvider(new DateTimeOffset(2026, 8, 19, 20, 0, 0, TimeSpan.Zero)));
        var dashboard = await service.GetDashboardAsync(UserId);

        Assert.Empty(dashboard.Habits);
        Assert.Equal(DayPeriod.Night, dashboard.CurrentPeriod);
    }

    [Fact]
    public async Task GetDashboardAsync_OrdersHabits_ByPeriodRankThenCreatedAtAscending()
    {
        using var context = CreateContext(Guid.NewGuid().ToString());
        var now = new DateTime(2026, 8, 19, 14, 0, 0, DateTimeKind.Utc);

        context.Habits.AddRange(
            new Habit
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            Title = "Night worker",
            Frequency = FrequencyType.Daily,
            Period = DayPeriod.Night,
            TargetCount = 1,
            CreatedAtUtc = now.AddHours(-10)
        },
        new Habit
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            Title = "Afternoon run",
            Frequency = FrequencyType.Daily,
            Period = DayPeriod.Afternoon,
            TargetCount = 1,
            CreatedAtUtc = now.AddHours(-3)
        },
        new Habit
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            Title = "Morning stretch",
            Frequency = FrequencyType.Daily,
            Period = DayPeriod.Morning,
            TargetCount = 1,
            CreatedAtUtc = now.AddHours(-1)
        },
        new Habit
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            Title = "Read",
            Frequency = FrequencyType.Daily,
            TargetCount = 1,
            CreatedAtUtc = now.AddHours(-2)
        });
        await context.SaveChangesAsync();

        var service = CreateHabitService(context, new FakeTimeProvider(new DateTimeOffset(2026, 8, 19, 14, 0, 0, TimeSpan.Zero)));
        var dashboard = await service.GetDashboardAsync(UserId);
        var titles = dashboard.Habits.Select(h => h.Title).ToArray();

        Assert.Equal(DayPeriod.Afternoon, dashboard.CurrentPeriod);
        Assert.Equal(
            new[] { "Afternoon run", "Read", "Morning stretch", "Night worker" },
            titles);
    }

    [Fact]
    public async Task GetDashboardAsync_NullPeriod_SortsAsAny_WithSameTieBreak()
    {
        using var context = CreateContext(Guid.NewGuid().ToString());
        var now = new DateTime(2026, 8, 19, 14, 0, 0, DateTimeKind.Utc);

        context.Habits.AddRange(
            new Habit
            {
                Id = Guid.NewGuid(),
                UserId = UserId,
                Title = "Any explicit",
                Frequency = FrequencyType.Daily,
                Period = DayPeriod.Any,
                TargetCount = 1,
                CreatedAtUtc = now.AddHours(-1)
            },
            new Habit
            {
                Id = Guid.NewGuid(),
                UserId = UserId,
                Title = "Null period",
                Frequency = FrequencyType.Daily,
                TargetCount = 1,
                CreatedAtUtc = now
            });
        await context.SaveChangesAsync();

        var service = CreateHabitService(context, new FakeTimeProvider(new DateTimeOffset(2026, 8, 19, 14, 0, 0, TimeSpan.Zero)));
        var dashboard = await service.GetDashboardAsync(UserId);
        var titles = dashboard.Habits.Select(h => h.Title).ToArray();

        Assert.Equal(new[] { "Any explicit", "Null period" }, titles);
    }

    [Fact]
    public async Task GetDashboardAsync_TieBreak_WithinCategory_IsCreatedAtUtcAscending()
    {
        using var context = CreateContext(Guid.NewGuid().ToString());
        var now = new DateTime(2026, 8, 19, 14, 0, 0, DateTimeKind.Utc);

        context.Habits.AddRange(
            new Habit
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            Title = "Older",
            Frequency = FrequencyType.Daily,
            Period = DayPeriod.Night,
            TargetCount = 1,
            CreatedAtUtc = now.AddDays(-2)
        },
        new Habit
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            Title = "Newer",
            Frequency = FrequencyType.Daily,
            Period = DayPeriod.Night,
            TargetCount = 1,
            CreatedAtUtc = now.AddDays(-1)
        });
        await context.SaveChangesAsync();

        var service = CreateHabitService(context, new FakeTimeProvider(new DateTimeOffset(2026, 8, 19, 14, 0, 0, TimeSpan.Zero)));
        var dashboard = await service.GetDashboardAsync(UserId);

        Assert.Equal(new[] { "Older", "Newer" }, dashboard.Habits.Select(h => h.Title));
    }

    [Fact]
    public async Task GetDashboardAsync_IsTimeZoneAware_UsesUsersLocalPeriod()
    {
        using var context = CreateContext(Guid.NewGuid().ToString());

        context.Users.Add(new ApplicationUser
        {
            Id = UserId,
            UserName = "a@b.com",
            Email = "a@b.com",
            TimeZoneId = "America/New_York"
        });
        await context.SaveChangesAsync();

        var service = CreateHabitService(context, new FakeTimeProvider(new DateTimeOffset(2026, 8, 20, 1, 30, 0, TimeSpan.Zero)));
        var dashboard = await service.GetDashboardAsync(UserId);

        Assert.Equal(DayPeriod.Night, dashboard.CurrentPeriod);
    }
}
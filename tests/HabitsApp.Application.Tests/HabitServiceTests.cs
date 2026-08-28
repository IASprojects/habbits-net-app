using HabitsApp.Api.Services;
using HabitsApp.Application.Services;
using HabitsApp.Application.Contracts.Habits;
using HabitsApp.Domain.Entities;
using HabitsApp.Domain.Enums;
using HabitsApp.Infrastructure.Abstractions;
using HabitsApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

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

    [Fact]
    public async Task CreateAsync_AddsHabitWithOwnershipAndCreatedAt()
    {
        using var context = CreateContext(Guid.NewGuid().ToString());
        var service = new HabitService(context, NullLogger<HabitService>.Instance);

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

        var service = new HabitService(context, NullLogger<HabitService>.Instance);
        var items = await service.GetDashboardAsync(UserId);

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

        var service = new HabitService(context, NullLogger<HabitService>.Instance);
        var items = await service.GetDashboardAsync(UserId);

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

        var service = new HabitService(context, NullLogger<HabitService>.Instance);

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

        var service = new HabitService(context, NullLogger<HabitService>.Instance);
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

        var service = new HabitService(context, NullLogger<HabitService>.Instance);
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

        var service = new HabitService(context, NullLogger<HabitService>.Instance);
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

        var service = new HabitService(context, NullLogger<HabitService>.Instance);
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

        var service = new HabitService(context, NullLogger<HabitService>.Instance);
        var result = await service.QuickLogAsync(UserId, habit.Id);

        Assert.False(result.Succeeded);
        Assert.Equal(404, result.StatusCode);
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

        var service = new HabitService(context, NullLogger<HabitService>.Instance);
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

        var service = new HabitService(context, NullLogger<HabitService>.Instance);
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

        var service = new HabitService(context, NullLogger<HabitService>.Instance);
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
    public async Task ArchiveAsync_ArchivesHabitAndHidesFromDashboard()
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

        var service = new HabitService(context, NullLogger<HabitService>.Instance);
        var result = await service.ArchiveAsync(UserId, habit.Id);

        Assert.True(result.Succeeded);

        var archived = await context.Habits.SingleAsync();
        Assert.True(archived.IsArchived);

        var dashboard = await service.GetDashboardAsync(UserId);
        Assert.Empty(dashboard);
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

        var service = new HabitService(context, NullLogger<HabitService>.Instance);
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

        var service = new HabitService(context, NullLogger<HabitService>.Instance);
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

        var service = new HabitService(context, NullLogger<HabitService>.Instance);
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

        var service = new HabitService(context, NullLogger<HabitService>.Instance);
        var items = await service.GetDashboardAsync(UserId);

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

        var service = new HabitService(context, NullLogger<HabitService>.Instance);
        var items = await service.GetDashboardAsync(UserId);

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

        var service = new HabitService(context, NullLogger<HabitService>.Instance);
        var items = await service.GetDashboardAsync(UserId);

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

        var service = new HabitService(context, NullLogger<HabitService>.Instance);
        var items = await service.GetDashboardAsync(UserId);

        var item = Assert.Single(items);
        Assert.Equal(0, item.Streak);
    }
}
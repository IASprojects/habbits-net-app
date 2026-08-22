using HabitsApp.Api.Services;
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
}
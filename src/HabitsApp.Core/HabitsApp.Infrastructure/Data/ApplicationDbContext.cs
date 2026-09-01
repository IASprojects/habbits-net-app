using HabitsApp.Domain.Entities;
using HabitsApp.Infrastructure.Abstractions;
using HabitsApp.Infrastructure.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace HabitsApp.Infrastructure.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    private readonly ICurrentUserService _currentUserService;

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : this(options, new AnonymousCurrentUserService())
    {
    }

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, ICurrentUserService currentUserService)
        : base(options)
    {
        _currentUserService = currentUserService;
    }

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<Habit> Habits => Set<Habit>();

    public DbSet<HabitLog> HabitLogs => Set<HabitLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(rt => rt.Id);
            entity.Property(rt => rt.TokenHash).HasMaxLength(64).IsRequired();
            entity.HasIndex(rt => rt.TokenHash).IsUnique();
            entity.Property(rt => rt.UserId).IsRequired();
            entity.Property(rt => rt.ExpiresAtUtc).IsRequired();
            entity.Property(rt => rt.CreatedAtUtc).IsRequired();
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(rt => rt.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Habit>(entity =>
        {
            entity.HasKey(h => h.Id);
            entity.Property(h => h.UserId).IsRequired();
            entity.Property(h => h.Title).HasMaxLength(200).IsRequired();
            entity.Property(h => h.Description).HasMaxLength(1000);
            entity.Property(h => h.ColorHex).HasMaxLength(9).IsRequired();
            entity.Property(h => h.TargetCount).IsRequired();
            entity.Property(h => h.CreatedAtUtc).IsRequired();
            entity.HasIndex(h => new { h.UserId, h.IsArchived });
            entity.HasQueryFilter(h => h.UserId == _currentUserService.UserId);
        });

        modelBuilder.Entity<HabitLog>(entity =>
        {
            entity.HasKey(l => l.Id);
            entity.Property(l => l.HabitId).IsRequired();
            entity.Property(l => l.UserId).IsRequired();
            entity.Property(l => l.CompletedAtUtc).IsRequired();
            entity.Property(l => l.PeriodKey).HasMaxLength(16).IsRequired();
            entity.Property(l => l.HourKey).HasMaxLength(13).IsRequired();
            entity.HasIndex(l => new { l.HabitId, l.HourKey }).IsUnique();
            entity.HasIndex(l => new { l.HabitId, l.CompletedAtUtc });
            entity.HasOne<Habit>()
                .WithMany(h => h.Logs)
                .HasForeignKey(l => l.HabitId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasQueryFilter(l => l.UserId == _currentUserService.UserId);
        });
    }

    private sealed class AnonymousCurrentUserService : ICurrentUserService
    {
        public Guid? UserId => null;
    }
}
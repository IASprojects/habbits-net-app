using Microsoft.AspNetCore.Identity;

namespace HabitsApp.Domain.Entities;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public string? TimeZoneId { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

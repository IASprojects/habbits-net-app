namespace HabitsApp.Application.Contracts.Auth;

public sealed class UserProfileDto
{
    public Guid Id { get; set; }

    public string Email { get; set; } = string.Empty;

    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public string? TimeZoneId { get; set; }
}
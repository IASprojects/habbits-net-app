using System.ComponentModel.DataAnnotations;

namespace HabitsApp.Application.Contracts.Auth;

public sealed class UpdateProfileCommand
{
    [StringLength(100, ErrorMessage = "First name must not exceed 100 characters.")]
    public string? FirstName { get; set; }

    [StringLength(100, ErrorMessage = "Last name must not exceed 100 characters.")]
    public string? LastName { get; set; }

    [StringLength(64, ErrorMessage = "Time zone must not exceed 64 characters.")]
    public string? TimeZoneId { get; set; }
}

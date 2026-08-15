using System.ComponentModel.DataAnnotations;

namespace HabitsApp.Application.Contracts.Auth;

public sealed class RegisterUserCommand
{
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "A valid email address is required.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters long.")]
    [RegularExpression(@"^(?=.*\d)(?=.*[A-Z]).+$", ErrorMessage = "Password must contain at least one digit and one uppercase letter.")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password confirmation is required.")]
    [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [StringLength(100, ErrorMessage = "First name must not exceed 100 characters.")]
    public string? FirstName { get; set; }

    [StringLength(100, ErrorMessage = "Last name must not exceed 100 characters.")]
    public string? LastName { get; set; }
}
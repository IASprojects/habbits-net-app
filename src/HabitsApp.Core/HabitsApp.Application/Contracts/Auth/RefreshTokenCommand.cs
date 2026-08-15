using System.ComponentModel.DataAnnotations;

namespace HabitsApp.Application.Contracts.Auth;

public sealed class RefreshTokenCommand
{
    [Required(ErrorMessage = "Refresh token is required.")]
    public string RefreshToken { get; set; } = string.Empty;
}
namespace HabitsApp.WebBlazor.Models.Auth;

public sealed class RegisterRequest
{
    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string ConfirmPassword { get; set; } = string.Empty;

    public string? FirstName { get; set; }

    public string? LastName { get; set; }
}
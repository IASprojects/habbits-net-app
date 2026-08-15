namespace HabitsApp.WebBlazor.Models.Auth;

public sealed class AuthResponse
{
    public string AccessToken { get; set; } = string.Empty;

    public int ExpiresIn { get; set; }

    public string RefreshToken { get; set; } = string.Empty;

    public UserProfileDto User { get; set; } = new();
}
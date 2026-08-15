using HabitsApp.Application.Contracts.Auth;

namespace HabitsApp.Application.Services;

public sealed class JwtSettings : IJwtSettings
{
    public string Issuer { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;

    public string SecretKey { get; set; } = string.Empty;

    public int ExpiryMinutes { get; set; }

    public int RefreshTokenExpiryDays { get; set; }
}
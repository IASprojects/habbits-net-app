namespace HabitsApp.Application.Contracts.Auth;

public interface IJwtSettings
{
    string Issuer { get; }

    string Audience { get; }

    string SecretKey { get; }

    int ExpiryMinutes { get; }

    int RefreshTokenExpiryDays { get; }
}
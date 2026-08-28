using HabitsApp.WebBlazor.Models.Auth;

namespace HabitsApp.WebBlazor.Services;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);

    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);

    Task<AuthResponse> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default);

    Task LogoutAsync(string refreshToken, CancellationToken cancellationToken = default);

    Task<UserProfileDto?> GetMeAsync(CancellationToken cancellationToken = default);

    Task<UserProfileDto?> UpdateMeAsync(UpdateProfileRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TimeZoneDto>> GetTimezonesAsync(CancellationToken cancellationToken = default);
}
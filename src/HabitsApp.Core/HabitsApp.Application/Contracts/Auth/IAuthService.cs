using System.Security.Claims;

namespace HabitsApp.Application.Contracts.Auth;

public interface IAuthService
{
    Task<AuthResult> RegisterAsync(RegisterUserCommand command, CancellationToken cancellationToken = default);

    Task<AuthResult> LoginAsync(LoginCommand command, CancellationToken cancellationToken = default);

    Task<AuthResult> RefreshAsync(RefreshTokenCommand command, CancellationToken cancellationToken = default);

    Task<AuthResult> LogoutAsync(ClaimsPrincipal principal, LogoutCommand command, CancellationToken cancellationToken = default);

    Task<UserProfileDto?> GetCurrentUserAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default);
}
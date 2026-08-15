using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using HabitsApp.Application.Contracts.Auth;
using HabitsApp.Domain.Entities;
using HabitsApp.Infrastructure.Data;
using HabitsApp.Infrastructure.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace HabitsApp.Api.Services;

public sealed class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _dbContext;
    private readonly IJwtSettings _jwtSettings;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext dbContext,
        IJwtSettings jwtSettings,
        ILogger<AuthService> logger)
    {
        _userManager = userManager;
        _dbContext = dbContext;
        _jwtSettings = jwtSettings;
        _logger = logger;
    }

    public async Task<AuthResult> RegisterAsync(RegisterUserCommand command, CancellationToken cancellationToken = default)
    {
        var existingUser = await _userManager.FindByEmailAsync(command.Email);
        if (existingUser is not null)
        {
            return AuthResult.Failure(
                StatusCodes.Status409Conflict,
                "Email already registered",
                "An account with this email already exists.");
        }

        var user = new ApplicationUser
        {
            UserName = command.Email,
            Email = command.Email,
            FirstName = command.FirstName,
            LastName = command.LastName
        };

        var createResult = await _userManager.CreateAsync(user, command.Password);
        if (!createResult.Succeeded)
        {
            var errors = createResult.Errors.ToDictionary(
                e => e.Code,
                e => new[] { e.Description });

            return AuthResult.Failure(
                StatusCodes.Status400BadRequest,
                "Registration failed",
                "One or more validation errors occurred.",
                errors);
        }

        _logger.LogInformation("User {UserId} registered successfully.", user.Id);

        var response = await IssueTokenPairAsync(user, cancellationToken);
        return AuthResult.Success(response);
    }

    public async Task<AuthResult> LoginAsync(LoginCommand command, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByEmailAsync(command.Email);
        if (user is null)
        {
            return AuthResult.Failure(
                StatusCodes.Status401Unauthorized,
                "Invalid credentials",
                "Invalid email or password.");
        }

        if (await _userManager.IsLockedOutAsync(user))
        {
            return AuthResult.Failure(
                StatusCodes.Status423Locked,
                "Account locked",
                "Too many failed login attempts. Try again later.");
        }

        if (!await _userManager.CheckPasswordAsync(user, command.Password))
        {
            await _userManager.AccessFailedAsync(user);

            if (await _userManager.IsLockedOutAsync(user))
            {
                _logger.LogWarning("User {UserId} locked out due to failed login attempts.", user.Id);
                return AuthResult.Failure(
                    StatusCodes.Status423Locked,
                    "Account locked",
                    "Too many failed login attempts. Try again later.");
            }

            return AuthResult.Failure(
                StatusCodes.Status401Unauthorized,
                "Invalid credentials",
                "Invalid email or password.");
        }

        await _userManager.ResetAccessFailedCountAsync(user);
        _logger.LogInformation("User {UserId} logged in successfully.", user.Id);

        var response = await IssueTokenPairAsync(user, cancellationToken);
        return AuthResult.Success(response);
    }

    public async Task<AuthResult> RefreshAsync(RefreshTokenCommand command, CancellationToken cancellationToken = default)
    {
        var tokenHash = HashToken(command.RefreshToken);
        var refreshToken = await _dbContext.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash, cancellationToken);

        if (refreshToken is null)
        {
            return AuthResult.Failure(
                StatusCodes.Status401Unauthorized,
                "Invalid refresh token",
                "The refresh token is invalid.");
        }

        if (refreshToken.RevokedAtUtc is not null || refreshToken.ExpiresAtUtc < DateTime.UtcNow)
        {
            return AuthResult.Failure(
                StatusCodes.Status401Unauthorized,
                "Invalid refresh token",
                "The refresh token is no longer valid.");
        }

        var user = await _userManager.FindByIdAsync(refreshToken.UserId.ToString());
        if (user is null)
        {
            return AuthResult.Failure(
                StatusCodes.Status401Unauthorized,
                "Invalid refresh token",
                "The refresh token is invalid.");
        }

        refreshToken.RevokedAtUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        var response = await IssueTokenPairAsync(user, cancellationToken);
        return AuthResult.Success(response);
    }

    public async Task<AuthResult> LogoutAsync(LogoutCommand command, CancellationToken cancellationToken = default)
    {
        var tokenHash = HashToken(command.RefreshToken);
        var refreshToken = await _dbContext.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash, cancellationToken);

        if (refreshToken is not null && refreshToken.RevokedAtUtc is null)
        {
            refreshToken.RevokedAtUtc = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return AuthResult.Success(new AuthResponse());
    }

    public async Task<UserProfileDto?> GetCurrentUserAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default)
    {
        var userId = principal.FindFirstValue("sub");
        if (userId is null || !Guid.TryParse(userId, out _))
        {
            return null;
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return null;
        }

        return new UserProfileDto
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            FirstName = user.FirstName,
            LastName = user.LastName
        };
    }

    private async Task<AuthResponse> IssueTokenPairAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        var accessToken = CreateAccessToken(user);
        var refreshToken = GenerateRefreshToken();

        _dbContext.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = HashToken(refreshToken),
            ExpiresAtUtc = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpiryDays),
            CreatedAtUtc = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new AuthResponse
        {
            AccessToken = accessToken,
            ExpiresIn = _jwtSettings.ExpiryMinutes * 60,
            RefreshToken = refreshToken,
            User = new UserProfileDto
            {
                Id = user.Id,
                Email = user.Email ?? string.Empty,
                FirstName = user.FirstName,
                LastName = user.LastName
            }
        };
    }

    private string CreateAccessToken(ApplicationUser user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.SecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new("sub", user.Id.ToString()),
            new("email", user.Email ?? string.Empty),
            new("jti", Guid.NewGuid().ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email ?? string.Empty)
        };

        if (!string.IsNullOrWhiteSpace(user.FirstName))
        {
            claims.Add(new Claim("given_name", user.FirstName));
        }

        if (!string.IsNullOrWhiteSpace(user.LastName))
        {
            claims.Add(new Claim("family_name", user.LastName));
        }

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = _jwtSettings.Issuer,
            Audience = _jwtSettings.Audience,
            Expires = DateTime.UtcNow.AddMinutes(_jwtSettings.ExpiryMinutes),
            SigningCredentials = credentials
        };

        var handler = new JsonWebTokenHandler();
        return handler.CreateToken(tokenDescriptor);
    }

    private static string GenerateRefreshToken()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    private static string HashToken(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using HabitsApp.Api.Services;
using HabitsApp.Application.Contracts.Auth;
using HabitsApp.Domain.Entities;
using HabitsApp.Infrastructure.Abstractions;
using HabitsApp.Infrastructure.Data;
using HabitsApp.Infrastructure.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HabitsApp.Application.Tests;

public class AuthServiceTests
{
    private sealed class TestCurrentUserService : ICurrentUserService
    {
        public Guid? UserId => null;
    }

    private sealed class FakeJwtSettings : IJwtSettings
    {
        public string Issuer { get; set; } = "HabitsApp";

        public string Audience { get; set; } = "HabitsApp";

        public string SecretKey { get; set; } = "test-secret-key-that-is-at-least-32-bytes!!";

        public int ExpiryMinutes { get; set; } = 60;

        public int RefreshTokenExpiryDays { get; set; } = 7;
    }

    private static ApplicationDbContext CreateContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        var context = new ApplicationDbContext(options, new TestCurrentUserService());
        context.Database.EnsureCreated();
        return context;
    }

    private static UserManager<ApplicationUser> CreateUserManager(ApplicationDbContext context)
    {
        var store = new UserStore<ApplicationUser, IdentityRole<Guid>, ApplicationDbContext, Guid>(context);
        IList<IUserValidator<ApplicationUser>> userValidators = new List<IUserValidator<ApplicationUser>>
        {
            new UserValidator<ApplicationUser>()
        };
        IList<IPasswordValidator<ApplicationUser>> passwordValidators = new List<IPasswordValidator<ApplicationUser>>
        {
            new PasswordValidator<ApplicationUser>()
        };

        var services = new ServiceCollection();
        services.AddLogging();

        return new UserManager<ApplicationUser>(
            store,
            Options.Create(new IdentityOptions()),
            new PasswordHasher<ApplicationUser>(),
            userValidators,
            passwordValidators,
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            services.BuildServiceProvider(),
            new NullLogger<UserManager<ApplicationUser>>());
    }

    private static AuthService CreateAuthService(UserManager<ApplicationUser> userManager, ApplicationDbContext context)
        => new(userManager, context, new FakeJwtSettings(), NullLogger<AuthService>.Instance);

    private static ClaimsPrincipal CreatePrincipal(Guid userId)
        => new(new ClaimsIdentity(new[] { new Claim("sub", userId.ToString()) }, "Test"));

    private static string HashToken(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    [Fact]
    public async Task LogoutAsync_RevokesOwnRefreshToken_WhenPrincipalMatches()
    {
        using var context = CreateContext(Guid.NewGuid().ToString());
        var userManager = CreateUserManager(context);

        var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = "a@b.com", Email = "a@b.com" };
        context.Users.Add(user);

        const string refreshToken = "own-token";
        context.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = HashToken(refreshToken),
            ExpiresAtUtc = DateTime.UtcNow.AddDays(7)
        });

        await context.SaveChangesAsync();

        var authService = CreateAuthService(userManager, context);
        var result = await authService.LogoutAsync(CreatePrincipal(user.Id), new LogoutCommand { RefreshToken = refreshToken });

        Assert.True(result.Succeeded);

        var stored = await context.RefreshTokens.SingleAsync();
        Assert.NotNull(stored.RevokedAtUtc);
    }

    [Fact]
    public async Task LogoutAsync_DoesNotRevoke_WhenRefreshTokenBelongsToAnotherUser()
    {
        using var context = CreateContext(Guid.NewGuid().ToString());
        var userManager = CreateUserManager(context);

        var owner = new ApplicationUser { Id = Guid.NewGuid(), UserName = "a@b.com", Email = "a@b.com" };
        var attacker = new ApplicationUser { Id = Guid.NewGuid(), UserName = "b@c.com", Email = "b@c.com" };
        context.Users.AddRange(owner, attacker);

        const string refreshToken = "owner-token";
        context.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = owner.Id,
            TokenHash = HashToken(refreshToken),
            ExpiresAtUtc = DateTime.UtcNow.AddDays(7)
        });

        await context.SaveChangesAsync();

        var authService = CreateAuthService(userManager, context);
        var result = await authService.LogoutAsync(CreatePrincipal(attacker.Id), new LogoutCommand { RefreshToken = refreshToken });

        Assert.True(result.Succeeded);

        var stored = await context.RefreshTokens.SingleAsync();
        Assert.Null(stored.RevokedAtUtc);
    }

    [Fact]
    public async Task LogoutAsync_Succeeds_WhenTokenAlreadyRevoked()
    {
        using var context = CreateContext(Guid.NewGuid().ToString());
        var userManager = CreateUserManager(context);

        var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = "a@b.com", Email = "a@b.com" };
        context.Users.Add(user);

        const string refreshToken = "already-revoked-token";
        context.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = HashToken(refreshToken),
            ExpiresAtUtc = DateTime.UtcNow.AddDays(7),
            RevokedAtUtc = DateTime.UtcNow.AddHours(-1)
        });

        await context.SaveChangesAsync();

        var authService = CreateAuthService(userManager, context);
        var result = await authService.LogoutAsync(CreatePrincipal(user.Id), new LogoutCommand { RefreshToken = refreshToken });

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task LogoutAsync_ReturnsUnauthorized_WhenSubClaimMissing()
    {
        using var context = CreateContext(Guid.NewGuid().ToString());
        var userManager = CreateUserManager(context);
        var authService = CreateAuthService(userManager, context);

        var result = await authService.LogoutAsync(
            new ClaimsPrincipal(new ClaimsIdentity()),
            new LogoutCommand { RefreshToken = "anything" });

        Assert.False(result.Succeeded);
        Assert.Equal(401, result.StatusCode);
    }

    [Fact]
    public async Task UpdateMeAsync_PersistsTimeZoneAndRoundTrips()
    {
        using var context = CreateContext(Guid.NewGuid().ToString());
        var userManager = CreateUserManager(context);

        var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = "a@b.com", Email = "a@b.com", SecurityStamp = Guid.NewGuid().ToString() };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var authService = CreateAuthService(userManager, context);
        var result = await authService.UpdateMeAsync(
            CreatePrincipal(user.Id),
            new UpdateProfileCommand { TimeZoneId = "America/New_York" });

        Assert.NotNull(result);
        Assert.Equal("America/New_York", result!.TimeZoneId);

        var profile = await authService.GetCurrentUserAsync(CreatePrincipal(user.Id));
        Assert.Equal("America/New_York", profile!.TimeZoneId);
    }

    [Fact]
    public async Task UpdateMeAsync_ClearsTimeZone_WhenValueIsEmpty()
    {
        using var context = CreateContext(Guid.NewGuid().ToString());
        var userManager = CreateUserManager(context);

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "a@b.com",
            Email = "a@b.com",
            SecurityStamp = Guid.NewGuid().ToString(),
            TimeZoneId = "America/New_York"
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var authService = CreateAuthService(userManager, context);
        var result = await authService.UpdateMeAsync(
            CreatePrincipal(user.Id),
            new UpdateProfileCommand { TimeZoneId = "" });

        Assert.NotNull(result);
        Assert.Null(result!.TimeZoneId);
    }

    [Fact]
    public async Task UpdateMeAsync_Throws_ForInvalidTimeZone()
    {
        using var context = CreateContext(Guid.NewGuid().ToString());
        var userManager = CreateUserManager(context);

        var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = "a@b.com", Email = "a@b.com", SecurityStamp = Guid.NewGuid().ToString() };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var authService = CreateAuthService(userManager, context);

        await Assert.ThrowsAsync<ArgumentException>(() => authService.UpdateMeAsync(
            CreatePrincipal(user.Id),
            new UpdateProfileCommand { TimeZoneId = "Not/AZone" }));
    }

    [Fact]
    public void GetTimezones_ReturnsNonEmptyServerList()
    {
        using var context = CreateContext(Guid.NewGuid().ToString());
        var authService = CreateAuthService(CreateUserManager(context), context);

        var timezones = authService.GetTimezones();

        Assert.NotEmpty(timezones);
        Assert.All(timezones, tz =>
        {
            Assert.False(string.IsNullOrWhiteSpace(tz.Id));
            Assert.False(string.IsNullOrWhiteSpace(tz.DisplayName));
        });
    }
}
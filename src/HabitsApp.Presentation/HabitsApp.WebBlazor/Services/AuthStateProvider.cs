using System.Security.Claims;
using System.Text;
using System.Text.Json;
using HabitsApp.WebBlazor.Models.Auth;
using Microsoft.AspNetCore.Components.Authorization;

namespace HabitsApp.WebBlazor.Services;

public sealed class AuthStateProvider : AuthenticationStateProvider
{
    private readonly TokenStorage _tokenStorage;
    private readonly IAuthService _authService;
    private ClaimsPrincipal _currentUser = new(new ClaimsIdentity());

    public AuthStateProvider(TokenStorage tokenStorage, IAuthService authService)
    {
        _tokenStorage = tokenStorage;
        _authService = authService;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var accessToken = await _tokenStorage.GetAccessTokenAsync();

        if (string.IsNullOrEmpty(accessToken))
        {
            _currentUser = new ClaimsPrincipal(new ClaimsIdentity());
            return new AuthenticationState(_currentUser);
        }

        var expiresAt = await _tokenStorage.GetExpiresAtAsync();
        if (expiresAt.HasValue && expiresAt.Value <= DateTimeOffset.UtcNow)
        {
            await TryRefreshAsync();
            accessToken = await _tokenStorage.GetAccessTokenAsync();

            if (string.IsNullOrEmpty(accessToken))
            {
                _currentUser = new ClaimsPrincipal(new ClaimsIdentity());
                return new AuthenticationState(_currentUser);
            }
        }

        _currentUser = CreateClaimsPrincipal(accessToken);
        return new AuthenticationState(_currentUser);
    }

    public async Task LoginAsync(AuthResponse response)
    {
        var expiresAt = DateTimeOffset.UtcNow.AddSeconds(response.ExpiresIn);
        await _tokenStorage.SetTokensAsync(response.AccessToken, response.RefreshToken, expiresAt);

        _currentUser = CreateClaimsPrincipal(response.AccessToken);
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_currentUser)));
    }

    public async Task LogoutAsync()
    {
        var refreshToken = await _tokenStorage.GetRefreshTokenAsync();

        if (!string.IsNullOrEmpty(refreshToken))
        {
            try
            {
                await _authService.LogoutAsync(refreshToken);
            }
            catch
            {
                // Best-effort revocation; local tokens are cleared regardless.
            }
        }

        await _tokenStorage.ClearAsync();
        _currentUser = new ClaimsPrincipal(new ClaimsIdentity());
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_currentUser)));
    }

    private async Task TryRefreshAsync()
    {
        var refreshToken = await _tokenStorage.GetRefreshTokenAsync();
        if (string.IsNullOrEmpty(refreshToken))
        {
            await _tokenStorage.ClearAsync();
            return;
        }

        try
        {
            var response = await _authService.RefreshAsync(refreshToken);
            var expiresAt = DateTimeOffset.UtcNow.AddSeconds(response.ExpiresIn);
            await _tokenStorage.SetTokensAsync(response.AccessToken, response.RefreshToken, expiresAt);
        }
        catch
        {
            await _tokenStorage.ClearAsync();
        }
    }

    private static ClaimsPrincipal CreateClaimsPrincipal(string accessToken)
    {
        var claims = ParseTokenClaims(accessToken);
        var identity = new ClaimsIdentity(claims, "jwt");
        return new ClaimsPrincipal(identity);
    }

    private static List<Claim> ParseTokenClaims(string accessToken)
    {
        var claims = new List<Claim>();

        var payload = DecodePayload(accessToken);
        if (payload is null)
        {
            return claims;
        }

        if (payload.TryGetValue("sub", out var sub) && sub is string subValue && !string.IsNullOrEmpty(subValue))
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, subValue));
            claims.Add(new Claim("sub", subValue));
        }

        if (payload.TryGetValue("email", out var email) && email is string emailValue && !string.IsNullOrEmpty(emailValue))
        {
            claims.Add(new Claim(ClaimTypes.Email, emailValue));
        }

        if (payload.TryGetValue("given_name", out var givenName) && givenName is string givenNameValue)
        {
            claims.Add(new Claim(ClaimTypes.GivenName, givenNameValue));
        }

        if (payload.TryGetValue("family_name", out var familyName) && familyName is string familyNameValue)
        {
            claims.Add(new Claim(ClaimTypes.Surname, familyNameValue));
        }

        return claims;
    }

    private static Dictionary<string, object?>? DecodePayload(string token)
    {
        var parts = token.Split('.');
        if (parts.Length != 3)
        {
            return null;
        }

        try
        {
            var payloadJson = Base64UrlDecode(parts[1]);
            return JsonSerializer.Deserialize<Dictionary<string, object?>>(payloadJson);
        }
        catch
        {
            return null;
        }
    }

    private static string Base64UrlDecode(string input)
    {
        var padded = input.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2:
                padded += "==";
                break;
            case 3:
                padded += "=";
                break;
        }

        var bytes = Convert.FromBase64String(padded);
        return Encoding.UTF8.GetString(bytes);
    }
}
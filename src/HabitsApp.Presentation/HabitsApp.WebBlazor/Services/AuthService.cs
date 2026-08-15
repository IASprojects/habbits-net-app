using System.Net.Http.Json;
using HabitsApp.WebBlazor.Models.Auth;

namespace HabitsApp.WebBlazor.Services;

public sealed class AuthService : IAuthService
{
    private readonly HttpClient _httpClient;

    public AuthService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/auth/register", request, cancellationToken);
        return await HandleResponseAsync<AuthResponse>(response, cancellationToken);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/auth/login", request, cancellationToken);
        return await HandleResponseAsync<AuthResponse>(response, cancellationToken);
    }

    public async Task<AuthResponse> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/auth/refresh", new { refreshToken }, cancellationToken);
        return await HandleResponseAsync<AuthResponse>(response, cancellationToken);
    }

    public async Task LogoutAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/auth/logout", new { refreshToken }, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            await ReadAndThrowAsync(response, cancellationToken);
        }
    }

    public async Task<UserProfileDto?> GetMeAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync("/api/auth/me", cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            return null;
        }

        return await HandleResponseAsync<UserProfileDto>(response, cancellationToken);
    }

    private static async Task<T> HandleResponseAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken);
            return result ?? throw new InvalidOperationException("The API returned an empty response.");
        }

        await ReadAndThrowAsync(response, cancellationToken);
        throw new InvalidOperationException("Unreachable code.");
    }

    private static async Task ReadAndThrowAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(cancellationToken: cancellationToken);
        throw new ApiException((int)response.StatusCode, problem);
    }
}
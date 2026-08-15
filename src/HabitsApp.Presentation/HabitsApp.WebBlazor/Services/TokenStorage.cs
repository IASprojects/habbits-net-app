using Microsoft.JSInterop;

namespace HabitsApp.WebBlazor.Services;

public sealed class TokenStorage
{
    private const string AccessTokenKey = "habits_access_token";
    private const string RefreshTokenKey = "habits_refresh_token";
    private const string ExpiresAtKey = "habits_expires_at";

    private readonly IJSRuntime _jsRuntime;

    public TokenStorage(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task SetTokensAsync(string accessToken, string refreshToken, DateTimeOffset expiresAt)
    {
        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", AccessTokenKey, accessToken);
        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", RefreshTokenKey, refreshToken);
        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", ExpiresAtKey, expiresAt.ToUnixTimeSeconds().ToString());
    }

    public async Task<string?> GetAccessTokenAsync()
        => await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", AccessTokenKey);

    public async Task<string?> GetRefreshTokenAsync()
        => await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", RefreshTokenKey);

    public async Task<DateTimeOffset?> GetExpiresAtAsync()
    {
        var value = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", ExpiresAtKey);
        if (string.IsNullOrEmpty(value) || !long.TryParse(value, out var unixSeconds))
        {
            return null;
        }

        return DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
    }

    public async Task ClearAsync()
    {
        await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", AccessTokenKey);
        await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", RefreshTokenKey);
        await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", ExpiresAtKey);
    }
}
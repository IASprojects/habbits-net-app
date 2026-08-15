using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;

namespace HabitsApp.WebBlazor.Services;

public class HealthService : IHealthService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public HealthService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    public async Task<bool> CheckDatabaseHealthAsync()
    {
        try
        {
            var baseUrl = _configuration["ApiBaseUrl"] ?? _httpClient.BaseAddress?.ToString();
            var response = await _httpClient.GetFromJsonAsync<HealthResponse>($"{baseUrl}/api/health");
            return response?.IsConnected ?? false;
        }
        catch
        {
            return false;
        }
    }

    private record HealthResponse(bool IsConnected, string DatabaseName, string TimestampUtc, double LatencyMs);
}
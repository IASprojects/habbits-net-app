using System.Net.Http.Json;
using HabitsApp.WebBlazor.Models.Auth;
using HabitsApp.WebBlazor.Models.Habits;

namespace HabitsApp.WebBlazor.Services;

public sealed class HabitService : IHabitService
{
    private readonly HttpClient _httpClient;

    public HabitService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyList<HabitDashboardItem>> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync("/api/habits", cancellationToken);
        return await HandleResponseAsync<IReadOnlyList<HabitDashboardItem>>(response, cancellationToken);
    }

    public async Task<IReadOnlyList<CalendarDay>> GetCalendarAsync(
        DateOnly start,
        DateOnly end,
        Guid? habitId,
        CancellationToken cancellationToken = default)
    {
        var query = $"/api/habits/calendar?start={start:yyyy-MM-dd}&end={end:yyyy-MM-dd}";
        if (habitId.HasValue)
        {
            query += $"&habitId={habitId.Value}";
        }

        var response = await _httpClient.GetAsync(query, cancellationToken);
        return await HandleResponseAsync<IReadOnlyList<CalendarDay>>(response, cancellationToken);
    }

    public async Task<HabitDashboardItem> CreateAsync(CreateHabitRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/habits", request, cancellationToken);
        return await HandleResponseAsync<HabitDashboardItem>(response, cancellationToken);
    }

    public async Task<HabitDashboardItem> UpdateAsync(Guid habitId, UpdateHabitRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"/api/habits/{habitId}", request, cancellationToken);
        return await HandleResponseAsync<HabitDashboardItem>(response, cancellationToken);
    }

    public async Task<HabitDashboardItem> QuickLogAsync(Guid habitId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"/api/habits/{habitId}/quick-log", new { }, cancellationToken);
        return await HandleResponseAsync<HabitDashboardItem>(response, cancellationToken);
    }

    public async Task ArchiveAsync(Guid habitId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"/api/habits/{habitId}", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            await ReadAndThrowAsync(response, cancellationToken);
        }
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
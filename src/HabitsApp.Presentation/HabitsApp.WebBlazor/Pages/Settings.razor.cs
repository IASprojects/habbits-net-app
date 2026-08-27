using HabitsApp.WebBlazor.Services;
using Microsoft.AspNetCore.Components;

namespace HabitsApp.WebBlazor.Pages;

public partial class Settings
{
    private bool isConnected = false;
    private Guid sessionId = Guid.NewGuid();
    private Timer? timer;

    [Inject] private IHealthService HealthService { get; set; } = default!;

    [Inject] private AuthStateProvider AuthStateProvider { get; set; } = default!;

    [Inject] private NavigationManager Navigation { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        await CheckHealth();

        timer = new Timer(async _ => await CheckHealth(), null,
            TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(30));
    }

    private async Task CheckHealth()
    {
        try
        {
            var start = DateTime.Now;
            var result = await HealthService.CheckDatabaseHealthAsync();
            var latency = (DateTime.Now - start).TotalMilliseconds;

            Console.WriteLine($"[{sessionId}] Health check: {(result ? "Success" : "Failed")} (Latency: {latency}ms)");
            isConnected = result;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[{sessionId}] Health check error: {ex.Message}");
            isConnected = false;
        }
        finally
        {
            StateHasChanged();
        }
    }

    private async Task HandleLogout()
    {
        await AuthStateProvider.LogoutAsync();
        Navigation.NavigateTo("");
    }

    public void Dispose()
    {
        timer?.Dispose();
    }
}
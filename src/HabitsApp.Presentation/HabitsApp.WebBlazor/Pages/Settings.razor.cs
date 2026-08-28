using HabitsApp.WebBlazor.Models.Auth;
using HabitsApp.WebBlazor.Services;
using Microsoft.AspNetCore.Components;

namespace HabitsApp.WebBlazor.Pages;

public partial class Settings
{
    private bool isConnected = false;
    private Guid sessionId = Guid.NewGuid();
    private Timer? timer;
    private UserProfileDto? profile;
    private IReadOnlyList<TimeZoneDto> timezones = [];
    private string selectedTimeZoneId = "";
    private bool isLoadingProfile = true;
    private bool isSavingTimeZone;
    private string? timeZoneMessage;
    private bool isTimeZoneError;

    [Inject] private IHealthService HealthService { get; set; } = default!;

    [Inject] private AuthStateProvider AuthStateProvider { get; set; } = default!;

    [Inject] private IAuthService AuthService { get; set; } = default!;

    [Inject] private TimeZoneJsInterop TimeZoneJsInterop { get; set; } = default!;

    [Inject] private NavigationManager Navigation { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        await CheckHealth();
        await LoadProfileAsync();

        timer = new Timer(async _ => await CheckHealth(), null,
            TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(30));
    }

    private bool IsTimeZoneDirty => !string.Equals(selectedTimeZoneId, profile?.TimeZoneId, System.StringComparison.Ordinal);

    private bool IsSavingTimeZone => isSavingTimeZone;

    private async Task LoadProfileAsync()
    {
        isLoadingProfile = true;
        StateHasChanged();

        try
        {
            profile = await AuthService.GetMeAsync();
            timezones = await AuthService.GetTimezonesAsync();
            selectedTimeZoneId = profile?.TimeZoneId ?? "";

            if (profile is not null && string.IsNullOrWhiteSpace(profile.TimeZoneId))
            {
                var detected = await TimeZoneJsInterop.GetBrowserTimeZoneIdAsync();
                if (string.IsNullOrWhiteSpace(detected))
                {
                    detected = "UTC";
                }

                await PersistTimeZoneAsync(detected, showMessage: false);
            }
        }
        finally
        {
            isLoadingProfile = false;
            StateHasChanged();
        }
    }

    private async Task SaveTimeZoneAsync()
    {
        await PersistTimeZoneAsync(selectedTimeZoneId, showMessage: true);
    }

    private async Task PersistTimeZoneAsync(string timeZoneId, bool showMessage)
    {
        isSavingTimeZone = true;
        timeZoneMessage = null;
        isTimeZoneError = false;
        StateHasChanged();

        try
        {
            var updated = await AuthService.UpdateMeAsync(new UpdateProfileRequest { TimeZoneId = timeZoneId });
            profile = updated ?? profile;
            selectedTimeZoneId = profile?.TimeZoneId ?? timeZoneId;

            if (showMessage)
            {
                timeZoneMessage = "Time zone saved.";
            }
        }
        catch (ApiException ex)
        {
            isTimeZoneError = true;
            timeZoneMessage = ex.GetErrorMessage() ?? "Failed to save the time zone.";
        }
        finally
        {
            isSavingTimeZone = false;
            StateHasChanged();
        }
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
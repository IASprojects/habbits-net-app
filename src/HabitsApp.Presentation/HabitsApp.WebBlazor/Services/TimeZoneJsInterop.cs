using System.Globalization;
using Microsoft.JSInterop;

namespace HabitsApp.WebBlazor.Services;

public sealed class TimeZoneJsInterop
{
    private readonly IJSRuntime _jsRuntime;

    public TimeZoneJsInterop(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task<string?> GetBrowserTimeZoneIdAsync()
        => await _jsRuntime.InvokeAsync<string?>("HabitsApp.getBrowserTimeZoneId");

    public async Task<DateOnly?> GetLocalTodayAsync()
    {
        var iso = await _jsRuntime.InvokeAsync<string?>("HabitsApp.getLocalToday");
        if (iso is not null && DateOnly.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.None, out var today))
        {
            return today;
        }

        return null;
    }
}
using HabitsApp.WebBlazor.Models.Habits;
using HabitsApp.WebBlazor.Services;
using Microsoft.AspNetCore.Components;

namespace HabitsApp.WebBlazor.Pages;

public partial class Calendar
{
    private enum ViewMode
    {
        Month,
        Week
    }

    private static readonly DateOnly MinimumDate = new(2025, 1, 1);

    private static readonly string[] WeekDayLabels = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];

    private ViewMode mode = ViewMode.Month;

    private DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);

    private DateOnly anchorDate = DateOnly.FromDateTime(DateTime.UtcNow);

    private List<HabitDashboardItem> habits = [];

    private Guid? selectedHabitId;

    private List<CalendarDay> days = [];

    private bool isLoading = true;

    private string? errorMessage;

    [Inject] private IHabitService HabitService { get; set; } = default!;

    [Inject] private TimeZoneJsInterop TimeZoneJsInterop { get; set; } = default!;

    private bool IsMonthMode => mode == ViewMode.Month;

    private bool IsLoading => isLoading;

    private DateOnly RangeStart => IsMonthMode
        ? new DateOnly(anchorDate.Year, anchorDate.Month, 1)
        : ClampToMinimum(StartOfWeek(anchorDate));

    private DateOnly RangeEnd => IsMonthMode
        ? new DateOnly(anchorDate.Year, anchorDate.Month, DateTime.DaysInMonth(anchorDate.Year, anchorDate.Month))
        : RangeStart.AddDays(6);

    private bool CanGoPrevious => RangeStart > MinimumDate;

    private string HeaderTitle => IsMonthMode
        ? anchorDate.ToString("MMMM yyyy")
        : $"{RangeStart:dd} – {RangeEnd:dd} {RangeStart:MMM yyyy}";

    private List<List<DateOnly?>> Weeks => BuildWeeks();

    protected override async Task OnInitializedAsync()
    {
        var localToday = await TimeZoneJsInterop.GetLocalTodayAsync();
        today = localToday ?? DateOnly.FromDateTime(DateTime.UtcNow);
        anchorDate = today;

        await LoadHabitsAsync();
        await LoadCalendarDaysAsync();
    }

    private async Task LoadHabitsAsync()
    {
        try
        {
            habits = (await HabitService.GetDashboardAsync()).ToList();
        }
        catch
        {
            errorMessage = "Unable to load your habits. Please try again.";
        }
    }

    private async Task LoadCalendarDaysAsync()
    {
        isLoading = true;
        errorMessage = null;
        StateHasChanged();

        try
        {
            days = (await HabitService.GetCalendarAsync(RangeStart, RangeEnd, selectedHabitId)).ToList();
        }
        catch
        {
            errorMessage = "Unable to load the calendar. Please try again.";
        }
        finally
        {
            isLoading = false;
            StateHasChanged();
        }
    }

    private async Task GoToday()
    {
        var localToday = await TimeZoneJsInterop.GetLocalTodayAsync();
        anchorDate = localToday ?? today;
        await LoadCalendarDaysAsync();
    }

    private async Task GoPrevious()
    {
        if (!CanGoPrevious)
        {
            return;
        }

        anchorDate = IsMonthMode ? anchorDate.AddMonths(-1) : anchorDate.AddDays(-7);
        await LoadCalendarDaysAsync();
    }

    private async Task GoNext()
    {
        anchorDate = IsMonthMode ? anchorDate.AddMonths(1) : anchorDate.AddDays(7);
        await LoadCalendarDaysAsync();
    }

    private async Task SetMode(ViewMode viewMode)
    {
        if (mode == viewMode)
        {
            return;
        }

        mode = viewMode;
        await LoadCalendarDaysAsync();
    }

    private async Task OnFilterChangedAsync(ChangeEventArgs e)
    {
        var raw = e.Value?.ToString();
        selectedHabitId = string.IsNullOrWhiteSpace(raw) ? null : Guid.Parse(raw);
        await LoadCalendarDaysAsync();
    }

    private List<List<DateOnly?>> BuildWeeks()
    {
        if (!IsMonthMode)
        {
            var week = new List<DateOnly?>();
            for (var day = RangeStart; day <= RangeEnd; day = day.AddDays(1))
            {
                week.Add(day);
            }
            return [week];
        }

        var cursor = StartOfWeek(new DateOnly(anchorDate.Year, anchorDate.Month, 1));
        var lastVisibleDay = StartOfWeek(new DateOnly(
            anchorDate.Year,
            anchorDate.Month,
            DateTime.DaysInMonth(anchorDate.Year, anchorDate.Month))).AddDays(6);

        var weeks = new List<List<DateOnly?>>();
        while (cursor <= lastVisibleDay)
        {
            var week = new List<DateOnly?>();
            for (var i = 0; i < 7; i++)
            {
                week.Add(cursor < MinimumDate ? null : cursor);
                cursor = cursor.AddDays(1);
            }
            weeks.Add(week);
        }

        return weeks;
    }

    private static DateOnly StartOfWeek(DateOnly date)
    {
        var daysSinceMonday = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-daysSinceMonday);
    }

    private static DateOnly ClampToMinimum(DateOnly date)
        => date < MinimumDate ? MinimumDate : date;

    private bool IsInRange(DateOnly day)
    {
        if (!IsMonthMode)
        {
            return true;
        }

        return day.Year == anchorDate.Year && day.Month == anchorDate.Month;
    }

    private bool IsToday(DateOnly day)
        => day == today;

    private IReadOnlyList<string> GetColors(DateOnly day)
    {
        var match = days.FirstOrDefault(d => d.Date == day);
        return match?.Colors ?? [];
    }
}
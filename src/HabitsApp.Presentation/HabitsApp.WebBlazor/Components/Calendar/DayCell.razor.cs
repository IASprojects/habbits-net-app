using Microsoft.AspNetCore.Components;

namespace HabitsApp.WebBlazor.Components.Calendar;

public partial class DayCell
{
    private const int MaxVisibleDots = 4;

    [Parameter]
    public DateOnly Date { get; set; }

    [Parameter]
    public IReadOnlyList<string> Colors { get; set; } = [];

    [Parameter]
    public bool IsInRange { get; set; } = true;

    [Parameter]
    public bool IsToday { get; set; }

    private IEnumerable<string> VisibleColors => Colors.Take(MaxVisibleDots);

    private string AriaLabel
        => $"{Date.Day} {Date.ToString("MMMM")}: {Colors.Count} habito(s) registrado(s)";
}
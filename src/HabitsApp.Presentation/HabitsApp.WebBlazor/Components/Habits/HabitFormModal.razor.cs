using HabitsApp.WebBlazor.Models.Habits;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace HabitsApp.WebBlazor.Components.Habits;

public partial class HabitFormModal
{
    private static readonly string[] Colors =
    [
        "#4F46E5",
        "#10B981",
        "#F59E0B",
        "#EF4444",
        "#8B5CF6",
        "#06B6D4",
        "#EC4899"
    ];

    private static readonly string[] Frequencies = ["Daily", "Weekly", "Monthly"];

    private HabitFormModel Form { get; set; } = new();

    private bool _wasOpen;

    [Parameter]
    public bool IsOpen { get; set; }

    [Parameter]
    public HabitDashboardItem? Habit { get; set; }

    [Parameter]
    public bool IsSaving { get; set; }

    [Parameter]
    public string? ErrorMessage { get; set; }

    [Parameter]
    public EventCallback<HabitFormModel> OnSave { get; set; }

    [Parameter]
    public EventCallback OnClose { get; set; }

    protected override void OnParametersSet()
    {
        if (IsOpen && !_wasOpen)
        {
            InitializeForm();
        }

        _wasOpen = IsOpen;
    }

    private void InitializeForm()
    {
        Form = Habit is null
            ? new HabitFormModel()
            : new HabitFormModel
            {
                Title = Habit.Title,
                Description = Habit.Description ?? string.Empty,
                ColorHex = Habit.ColorHex,
                Frequency = Habit.Frequency,
                TargetCount = Habit.TargetCount
            };
    }

    private void SelectColor(string color)
        => Form.ColorHex = color;

    private void SelectFrequency(string frequency)
        => Form.Frequency = frequency;

    private async Task HandleValidSubmit(EditContext editContext)
        => await OnSave.InvokeAsync(Form);

    private async Task Close()
        => await OnClose.InvokeAsync();
}
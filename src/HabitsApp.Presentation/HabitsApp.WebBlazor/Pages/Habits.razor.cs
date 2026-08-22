using HabitsApp.WebBlazor.Components.Habits;
using HabitsApp.WebBlazor.Models.Habits;
using HabitsApp.WebBlazor.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace HabitsApp.WebBlazor.Pages;

public partial class Habits
{
    private List<HabitDashboardItem> HabitItems { get; set; } = [];

    private bool IsLoading { get; set; } = true;

    private bool ShowModal { get; set; }

    private bool IsSaving { get; set; }

    private HabitDashboardItem? EditingHabit { get; set; }

    private string FirstName { get; set; } = "there";

    private string? ErrorMessage { get; set; }

    private string? ModalErrorMessage { get; set; }

    [Inject] private IHabitService HabitService { get; set; } = default!;

    [Inject] private AuthenticationStateProvider AuthStateProvider { get; set; } = default!;

    private int TotalCount => HabitItems.Count;

    private int CompletedCount => HabitItems.Count(h => h.IsCompletedForPeriod);

    private int MomentumPercent => TotalCount == 0 ? 0 : (int)Math.Round(CompletedCount * 100.0 / TotalCount);

    protected override async Task OnInitializedAsync()
    {
        await LoadHabitsAsync();
    }

    private async Task LoadHabitsAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        StateHasChanged();

        try
        {
            var authState = await AuthStateProvider.GetAuthenticationStateAsync();
            FirstName = authState.User.FindFirst("given_name")?.Value ?? "there";

            var items = await HabitService.GetDashboardAsync();
            HabitItems = items.ToList();
        }
        catch
        {
            ErrorMessage = "Unable to load your habits. Please try again.";
        }
        finally
        {
            IsLoading = false;
            StateHasChanged();
        }
    }

    private void OpenCreate()
    {
        EditingHabit = null;
        ModalErrorMessage = null;
        ShowModal = true;
    }

    private void OpenEdit(HabitDashboardItem habit)
    {
        EditingHabit = habit;
        ModalErrorMessage = null;
        ShowModal = true;
    }

    private void CloseModal()
    {
        if (!IsSaving)
        {
            ShowModal = false;
        }
    }

    private async Task HandleModalSave(HabitFormModel model)
    {
        IsSaving = true;
        ModalErrorMessage = null;
        StateHasChanged();

        try
        {
            if (EditingHabit is null)
            {
                await HabitService.CreateAsync(new CreateHabitRequest
                {
                    Title = model.Title,
                    Description = model.Description,
                    ColorHex = model.ColorHex,
                    Frequency = model.Frequency,
                    TargetCount = model.TargetCount
                });
            }
            else
            {
                await HabitService.UpdateAsync(EditingHabit.Id, new UpdateHabitRequest
                {
                    Title = model.Title,
                    Description = model.Description,
                    ColorHex = model.ColorHex,
                    Frequency = model.Frequency,
                    TargetCount = model.TargetCount
                });
            }

            ShowModal = false;
            await LoadHabitsAsync();
        }
        catch (ApiException ex)
        {
            ModalErrorMessage = ex.GetErrorMessage() ?? "Unable to save the habit.";
        }
        catch
        {
            ModalErrorMessage = "Unable to reach the server. Please try again.";
        }
        finally
        {
            IsSaving = false;
            StateHasChanged();
        }
    }

    private async Task HandleQuickLog(HabitDashboardItem habit)
    {
        try
        {
            var updated = await HabitService.QuickLogAsync(habit.Id);
            var index = HabitItems.FindIndex(h => h.Id == habit.Id);
            if (index >= 0)
            {
                HabitItems[index] = updated;
            }
        }
        catch
        {
            ErrorMessage = "Unable to log this habit. Please try again.";
        }

        StateHasChanged();
    }
}
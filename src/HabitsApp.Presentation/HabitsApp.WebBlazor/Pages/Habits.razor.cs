using HabitsApp.WebBlazor.Components;
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

    private bool ActiveOnly { get; set; } = true;

    private bool ShowModal { get; set; }

    private bool IsSaving { get; set; }

    private HabitDashboardItem? EditingHabit { get; set; }

    private string FirstName { get; set; } = "there";

    private string? ErrorMessage { get; set; }

    private string? ModalErrorMessage { get; set; }

    private bool ShowInactivateConfirm { get; set; }

    private bool ShowRestoreConfirm { get; set; }

    private bool IsConfirmBusy { get; set; }

    private HabitDashboardItem? PendingActionHabit { get; set; }

    private string? ConfirmErrorMessage { get; set; }

    [Inject] private IHabitService HabitService { get; set; } = default!;

    [Inject] private AuthenticationStateProvider AuthStateProvider { get; set; } = default!;

    private int TotalCount => HabitItems.Count(h => h.IsActive);

    private int CompletedCount => HabitItems.Count(h => h.IsActive && h.IsCompletedForPeriod);

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

            var items = await HabitService.GetDashboardAsync(ActiveOnly);
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

    private async Task SetActiveView(bool activeOnly)
    {
        if (ActiveOnly == activeOnly)
        {
            return;
        }

        ActiveOnly = activeOnly;
        await LoadHabitsAsync();
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

    private void RequestInactivate(HabitDashboardItem habit)
    {
        PendingActionHabit = habit;
        ConfirmErrorMessage = null;
        ShowInactivateConfirm = true;
        StateHasChanged();
    }

    private void RequestReactivate(HabitDashboardItem habit)
    {
        PendingActionHabit = habit;
        ConfirmErrorMessage = null;
        ShowModal = false;
        ShowRestoreConfirm = true;
        StateHasChanged();
    }

    private void CancelConfirmation()
    {
        if (IsConfirmBusy)
        {
            return;
        }

        ShowInactivateConfirm = false;
        ShowRestoreConfirm = false;
        PendingActionHabit = null;
        ConfirmErrorMessage = null;
        StateHasChanged();
    }

    private async Task HandleInactivateConfirmed()
    {
        var habit = PendingActionHabit;
        if (habit is null)
        {
            return;
        }

        IsConfirmBusy = true;
        ConfirmErrorMessage = null;
        StateHasChanged();

        try
        {
            await HabitService.InactivateAsync(habit.Id);
            ShowInactivateConfirm = false;
            PendingActionHabit = null;
            EditingHabit = null;
            ShowModal = false;
            await LoadHabitsAsync();
        }
        catch (ApiException ex)
        {
            ConfirmErrorMessage = ex.GetErrorMessage() ?? "Unable to inactivate the habit.";
        }
        catch
        {
            ConfirmErrorMessage = "Unable to reach the server. Please try again.";
        }
        finally
        {
            IsConfirmBusy = false;
            StateHasChanged();
        }
    }

    private async Task HandleRestoreConfirmed()
    {
        var habit = PendingActionHabit;
        if (habit is null)
        {
            return;
        }

        IsConfirmBusy = true;
        ConfirmErrorMessage = null;
        StateHasChanged();

        try
        {
            await HabitService.ReactivateAsync(habit.Id);
            ShowRestoreConfirm = false;
            PendingActionHabit = null;
            await LoadHabitsAsync();
        }
        catch (ApiException ex)
        {
            ConfirmErrorMessage = ex.GetErrorMessage() ?? "Unable to reactivate the habit.";
        }
        catch
        {
            ConfirmErrorMessage = "Unable to reach the server. Please try again.";
        }
        finally
        {
            IsConfirmBusy = false;
            StateHasChanged();
        }
    }
}
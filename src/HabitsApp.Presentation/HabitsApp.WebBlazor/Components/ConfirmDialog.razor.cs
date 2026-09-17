using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace HabitsApp.WebBlazor.Components;

public partial class ConfirmDialog
{
    [Parameter]
    public bool IsOpen { get; set; }

    [Parameter]
    public string Title { get; set; } = "Confirm";

    [Parameter]
    public string Message { get; set; } = string.Empty;

    [Parameter]
    public string? ErrorMessage { get; set; }

    [Parameter]
    public string ConfirmLabel { get; set; } = "Confirm";

    [Parameter]
    public bool IsWarning { get; set; }

    [Parameter]
    public bool IsBusy { get; set; }

    [Parameter]
    public EventCallback OnConfirm { get; set; }

    [Parameter]
    public EventCallback OnCancel { get; set; }

    private async Task Confirm()
        => await OnConfirm.InvokeAsync();

    private async Task Cancel()
        => await OnCancel.InvokeAsync();

    private async Task HandleKeyDown(KeyboardEventArgs keyEvent)
    {
        if (keyEvent.Key == "Escape" || keyEvent.Key == "Esc")
        {
            await Cancel();
        }
    }
}
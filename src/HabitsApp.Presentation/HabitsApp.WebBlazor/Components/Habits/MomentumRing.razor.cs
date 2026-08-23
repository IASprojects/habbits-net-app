using HabitsApp.WebBlazor.Models.Habits;
using Microsoft.AspNetCore.Components;

namespace HabitsApp.WebBlazor.Components.Habits;

public partial class MomentumRing
{
    [Parameter]
    public int Percent { get; set; }

    private int ClampedPercent => Math.Clamp(Percent, 0, 100);
}
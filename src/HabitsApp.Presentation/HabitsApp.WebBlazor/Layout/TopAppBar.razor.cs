using System.Security.Claims;
using Microsoft.AspNetCore.Components;

namespace HabitsApp.WebBlazor.Layout;

public partial class TopAppBar
{
    [Parameter]
    public ClaimsPrincipal User { get; set; } = default!;

    [Inject] private NavigationManager Navigation { get; set; } = default!;

    private static string GetDisplayName(ClaimsPrincipal user)
    {
        var givenName = user.FindFirst("given_name")?.Value;
        var familyName = user.FindFirst("family_name")?.Value;
        var email = user.FindFirst(ClaimTypes.Email)?.Value;
        var name = $"{givenName} {familyName}".Trim();
        return string.IsNullOrEmpty(name) ? email ?? "User" : name;
    }

    private static string GetInitials(ClaimsPrincipal user)
    {
        var givenName = user.FindFirst("given_name")?.Value;
        var familyName = user.FindFirst("family_name")?.Value;
        var email = user.FindFirst(ClaimTypes.Email)?.Value;
        var initials = $"{givenName?.FirstOrDefault()}{familyName?.FirstOrDefault()}";
        return string.IsNullOrWhiteSpace(initials)
            ? (email?.Length > 0 ? email.Substring(0, 1).ToUpperInvariant() : "U")
            : initials.ToUpperInvariant();
    }

    private void GoToSettings() => Navigation.NavigateTo("settings");
}
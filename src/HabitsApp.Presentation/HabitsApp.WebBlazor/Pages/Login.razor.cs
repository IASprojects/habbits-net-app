using System.ComponentModel.DataAnnotations;
using HabitsApp.WebBlazor.Models.Auth;
using HabitsApp.WebBlazor.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace HabitsApp.WebBlazor.Pages;

public partial class Login
{
    private LoginForm Model { get; set; } = new();

    private bool IsSubmitting { get; set; }

    private string? ErrorMessage { get; set; }

    [Inject] private AuthStateProvider AuthStateProvider { get; set; } = default!;

    [Inject] private IAuthService AuthService { get; set; } = default!;

    [Inject] private NavigationManager Navigation { get; set; } = default!;

    [SupplyParameterFromQuery(Name = "returnUrl")]
    private string? ReturnUrl { get; set; }

    private async Task HandleValidSubmit(EditContext editContext)
    {
        IsSubmitting = true;
        ErrorMessage = null;
        StateHasChanged();

        try
        {
            var response = await AuthService.LoginAsync(new LoginRequest
            {
                Email = Model.Email,
                Password = Model.Password
            });

            await AuthStateProvider.LoginAsync(response);

            var target = string.IsNullOrWhiteSpace(ReturnUrl) || ReturnUrl == "/" || ReturnUrl.StartsWith("/login")
                ? "/habits"
                : ReturnUrl;
            Navigation.NavigateTo(target);
        }
        catch (ApiException ex)
        {
            ErrorMessage = ex.GetErrorMessage() ?? "Login failed. Please try again.";
        }
        catch
        {
            ErrorMessage = "Unable to reach the server. Please try again.";
        }
        finally
        {
            IsSubmitting = false;
            StateHasChanged();
        }
    }

    private sealed class LoginForm
    {
        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Enter a valid email address.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required.")]
        public string Password { get; set; } = string.Empty;
    }
}
using System.ComponentModel.DataAnnotations;
using HabitsApp.WebBlazor.Models.Auth;
using HabitsApp.WebBlazor.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace HabitsApp.WebBlazor.Pages;

public partial class Register
{
    private RegisterForm Model { get; set; } = new();

    private bool IsSubmitting { get; set; }

    private string? ErrorMessage { get; set; }

    [Inject] private AuthStateProvider AuthStateProvider { get; set; } = default!;

    [Inject] private IAuthService AuthService { get; set; } = default!;

    [Inject] private NavigationManager Navigation { get; set; } = default!;

    private async Task HandleValidSubmit(EditContext editContext)
    {
        IsSubmitting = true;
        ErrorMessage = null;
        StateHasChanged();

        try
        {
            var response = await AuthService.RegisterAsync(new RegisterRequest
            {
                FirstName = Model.FirstName,
                LastName = Model.LastName,
                Email = Model.Email,
                Password = Model.Password,
                ConfirmPassword = Model.ConfirmPassword
            });

            await AuthStateProvider.LoginAsync(response);
            Navigation.NavigateTo("habits");
        }
        catch (ApiException ex)
        {
            ErrorMessage = ex.GetErrorMessage() ?? "Registration failed. Please try again.";
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

    private sealed class RegisterForm
    {
        [StringLength(100, ErrorMessage = "First name must not exceed 100 characters.")]
        public string? FirstName { get; set; }

        [StringLength(100, ErrorMessage = "Last name must not exceed 100 characters.")]
        public string? LastName { get; set; }

        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Enter a valid email address.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required.")]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters long.")]
        [RegularExpression(@"^(?=.*\d)(?=.*[A-Z]).+$", ErrorMessage = "Password must contain at least one digit and one uppercase letter.")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password confirmation is required.")]
        [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
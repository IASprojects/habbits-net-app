namespace HabitsApp.WebBlazor.Models.Auth;

public sealed class UpdateProfileRequest
{
    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public string? TimeZoneId { get; set; }
}
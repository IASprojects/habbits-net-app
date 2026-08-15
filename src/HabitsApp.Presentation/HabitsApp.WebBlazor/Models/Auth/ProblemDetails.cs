using System.Text.Json.Serialization;

namespace HabitsApp.WebBlazor.Models.Auth;

public sealed class ProblemDetails
{
    public string? Type { get; set; }

    public string? Title { get; set; }

    public int? Status { get; set; }

    public string? Detail { get; set; }

    public string? Instance { get; set; }

    [JsonPropertyName("errors")]
    public Dictionary<string, string[]>? Errors { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object>? Extensions { get; set; }
}
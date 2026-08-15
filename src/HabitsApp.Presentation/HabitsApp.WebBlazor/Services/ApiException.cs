using HabitsApp.WebBlazor.Models.Auth;

namespace HabitsApp.WebBlazor.Services;

public sealed class ApiException : Exception
{
    public int StatusCode { get; }

    public ProblemDetails? Problem { get; }

    public ApiException(int statusCode, ProblemDetails? problem)
        : base(problem?.Detail ?? problem?.Title ?? $"Request failed with status code {statusCode}.")
    {
        StatusCode = statusCode;
        Problem = problem;
    }

    public string? GetErrorMessage()
        => Problem?.Detail ?? Problem?.Title;
}
namespace HabitsApp.Application.Contracts.Auth;

public sealed class AuthResult
{
    public bool Succeeded { get; init; }

    public AuthResponse? Response { get; init; }

    public string? ErrorType { get; init; }

    public string? ErrorDetail { get; init; }

    public int? StatusCode { get; init; }

    public IReadOnlyDictionary<string, string[]>? ValidationErrors { get; init; }

    public static AuthResult Success(AuthResponse response) => new() { Succeeded = true, Response = response };

    public static AuthResult Failure(int statusCode, string errorType, string errorDetail, IReadOnlyDictionary<string, string[]>? validationErrors = null)
        => new()
        {
            Succeeded = false,
            StatusCode = statusCode,
            ErrorType = errorType,
            ErrorDetail = errorDetail,
            ValidationErrors = validationErrors
        };
}
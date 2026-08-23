namespace HabitsApp.Application.Contracts.Habits;

public sealed class HabitResult
{
    public bool Succeeded { get; init; }

    public HabitDashboardItemDto? Data { get; init; }

    public string? ErrorType { get; init; }

    public string? ErrorDetail { get; init; }

    public int? StatusCode { get; init; }

    public static HabitResult Success(HabitDashboardItemDto data) => new() { Succeeded = true, Data = data };

    public static HabitResult Failure(int statusCode, string errorType, string errorDetail)
        => new()
        {
            Succeeded = false,
            StatusCode = statusCode,
            ErrorType = errorType,
            ErrorDetail = errorDetail
        };
}
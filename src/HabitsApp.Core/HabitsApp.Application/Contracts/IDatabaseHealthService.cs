namespace HabitsApp.Application.Contracts;

public interface IDatabaseHealthService
{
    Task<bool> CheckDatabaseHealthAsync(CancellationToken cancellationToken = default);
}
namespace HabitsApp.WebBlazor.Services;

public interface IHealthService
{
    Task<bool> CheckDatabaseHealthAsync();
}
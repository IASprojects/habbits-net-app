using HabitsApp.Application.Contracts;
using HabitsApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HabitsApp.Application.Services;

public class DatabaseHealthService : IDatabaseHealthService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<DatabaseHealthService> _logger;

    public DatabaseHealthService(ApplicationDbContext dbContext, ILogger<DatabaseHealthService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<bool> CheckDatabaseHealthAsync(CancellationToken cancellationToken = default)
    {
        var start = DateTime.UtcNow;
        try
        {
            await _dbContext.Database.ExecuteSqlRawAsync("SELECT 1", cancellationToken);
            var latency = (DateTime.UtcNow - start).TotalMilliseconds;
            _logger.LogInformation("PostgreSQL ping succeeded (Latency: {Latency}ms)", latency);
            return true;
        }
        catch (Exception ex)
        {
            var latency = (DateTime.UtcNow - start).TotalMilliseconds;
            _logger.LogError(ex, "PostgreSQL ping failed (Latency: {Latency}ms)", latency);
            return false;
        }
    }
}
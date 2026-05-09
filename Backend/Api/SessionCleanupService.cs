using Bradford.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

// Runs on startup and then every 24 hours.
// Deletes conversation turns older than 30 days to keep the SQLite database from growing unboundedly.
public sealed class SessionCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SessionCleanupService> _logger;
    private static readonly TimeSpan RetentionPeriod = TimeSpan.FromDays(30);
    private static readonly TimeSpan Interval        = TimeSpan.FromHours(24);

    public SessionCleanupService(IServiceScopeFactory scopeFactory, ILogger<SessionCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await CleanupAsync(ct);
            await Task.Delay(Interval, ct);
        }
    }

    private async Task CleanupAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AgentDbContext>();

            var cutoff  = DateTime.UtcNow - RetentionPeriod;
            var deleted = await db.ConversationTurns
                .Where(t => t.Timestamp < cutoff)
                .ExecuteDeleteAsync(ct);

            if (deleted > 0)
                _logger.LogInformation("Session cleanup: deleted {Count} turns older than {Days} days.", deleted, RetentionPeriod.Days);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Session cleanup failed (non-fatal): {Msg}", ex.Message);
        }
    }
}

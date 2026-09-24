/*
 * Purpose: Periodically retries durable account-closure cleanup jobs.
 * Change reason: Add E-001 Phase 4 automatic reconciliation after partial media failures.
 * Created: 2026-09-11T00:32:56-06:00
 * Last updated: 2026-09-11T00:32:56-06:00
 */

using Appetee.Application.Abstractions.Users;

namespace Appetee.Api.HostedServices;

/// <summary>Runs bounded cleanup batches in fresh scopes until application shutdown.</summary>
public sealed class AccountClosureCleanupWorker : BackgroundService
{
    private static readonly TimeSpan ReconciliationInterval = TimeSpan.FromMinutes(1);
    private const int BatchSize = 25;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AccountClosureCleanupWorker> _logger;

    public AccountClosureCleanupWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<AccountClosureCleanupWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    // Cleanup failures never terminate the host; the durable job remains eligible for a later pass.
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(ReconciliationInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var processor = scope.ServiceProvider
                    .GetRequiredService<IAccountClosureProcessor>();
                await processor.ProcessDueAsync(BatchSize, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Account-closure reconciliation pass failed; it will be retried.");
            }

            if (!await timer.WaitForNextTickAsync(stoppingToken))
            {
                break;
            }
        }
    }
}

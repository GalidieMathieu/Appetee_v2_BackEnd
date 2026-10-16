/*
 * Purpose: Reconciles durable account-closure jobs with managed Blob Storage.
 * Change reason: Implement E-001 Phase 4 idempotent cleanup and retry behavior.
 * Created: 2026-09-11T00:32:56-06:00
 * Last updated: 2026-09-11T00:32:56-06:00
 */

using Appetee.Application.Abstractions.Users;
using Appetee.Infrastructure.Data;
using Microsoft.Extensions.Logging;

namespace Appetee.Infrastructure.Users;

/// <summary>Deletes only URLs owned by the configured container and scrubs them from completed jobs.</summary>
public sealed class AccountClosureProcessor : IAccountClosureProcessor
{
    private const int MaximumBatchSize = 100;
    private readonly IAccountClosureRepository _repository;
    private readonly IBlobStorageService _blobStorage;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AccountClosureProcessor> _logger;

    public AccountClosureProcessor(
        IAccountClosureRepository repository,
        IBlobStorageService blobStorage,
        TimeProvider timeProvider,
        ILogger<AccountClosureProcessor> logger)
    {
        _repository = repository;
        _blobStorage = blobStorage;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    // A completed or missing job is already reconciled; Blob deletion is DeleteIfExists and replay-safe.
    public async Task<bool> TryProcessAsync(long cleanupId, CancellationToken ct)
    {
        try
        {
            var cleanup = await _repository.GetPendingCleanupAsync(cleanupId, ct);

            if (cleanup is null)
            {
                return true;
            }

            if (cleanup.ProfileImageUrl is not null
                && Uri.TryCreate(cleanup.ProfileImageUrl, UriKind.Absolute, out var imageUri)
                && _blobStorage.TryGetBlobName(imageUri, out var blobName)
                && blobName.StartsWith(
                    $"users/{cleanup.FormerUserId}/",
                    StringComparison.Ordinal))
            {
                await _blobStorage.DeleteAsync(blobName, ct);
            }

            await _repository.MarkCleanupCompletedAsync(cleanup.Id, ct);
            return true;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception exception)
        {
            await RecordFailureBestEffortAsync(cleanupId, exception);
            return false;
        }
    }

    // Bounded batches keep reconciliation from monopolizing database or Blob resources.
    public async Task ProcessDueAsync(int maximumCount, CancellationToken ct)
    {
        if (maximumCount is < 1 or > MaximumBatchSize)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumCount));
        }

        var cleanupIds = await _repository.ListDueCleanupIdsAsync(maximumCount, ct);

        foreach (var cleanupId in cleanupIds)
        {
            if (ct.IsCancellationRequested)
            {
                break;
            }

            await TryProcessAsync(cleanupId, ct);
        }
    }

    // Failure records contain only a bounded exception type, never the deleted account or media URL.
    private async Task RecordFailureBestEffortAsync(
        long cleanupId,
        Exception exception)
    {
        var errorCode = exception.GetType().Name;
        var nextAttemptUtc = _timeProvider.GetUtcNow().AddMinutes(1);

        try
        {
            await _repository.MarkCleanupFailedAsync(
                cleanupId,
                errorCode.Length <= 100 ? errorCode : errorCode[..100],
                nextAttemptUtc,
                CancellationToken.None);
        }
        catch (Exception persistenceException)
        {
            _logger.LogError(
                persistenceException,
                "Could not record failure for account cleanup job {CleanupId}.",
                cleanupId);
        }

        _logger.LogWarning(
            "Account cleanup job {CleanupId} will be retried after {ErrorCode}.",
            cleanupId,
            errorCode);
    }
}

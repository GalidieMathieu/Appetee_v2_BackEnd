using Appetee.Application.Models.MealPlans;
using Appetee.Application.utils;
using Appetee.Infrastructure.Data;
using Dapper;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using System.Data;
using System.Diagnostics;

namespace Appetee.Infrastructure.MealPlans;

/// <summary>Owns the common user-lock, revision, mutation, readback, and commit protocol.</summary>
internal sealed class MealPlanTransaction
{
    private const int DeadlockErrorNumber = 1213;
    private readonly IDbConnectionFactory _db;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<MealPlanTransaction> _logger;

    public MealPlanTransaction(
        IDbConnectionFactory db,
        TimeProvider timeProvider,
        ILogger<MealPlanTransaction> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    internal async Task<MealPlanWriteResult<TPayload>> ExecuteAsync<TPayload>(
        string operationName,
        int currentUserId,
        long expectedRevision,
        Func<MealPlanWriteContext, Task<MealPlanMutation<TPayload>>> operation,
        CancellationToken ct,
        Func<MealPlanWriteContext, Task>? validateOwnership = null,
        bool isCopy = false,
        Func<MealPlanWriteContext, long, Task<TPayload>>? readback = null)
    {
        if (string.IsNullOrWhiteSpace(operationName))
            throw new ArgumentException("Operation name is required.", nameof(operationName));
        if (currentUserId <= 0)
            throw new UnauthorizedException();
        if (expectedRevision < 0)
            throw new ValidationException(
                "Revision must be nonnegative.",
                "revision_invalid",
                new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    ["revision"] = ["Revision must be nonnegative."],
                });

        ArgumentNullException.ThrowIfNull(operation);
        using var copyTimeout = isCopy ? CancellationTokenSource.CreateLinkedTokenSource(ct) : null;
        if (copyTimeout is not null)
        {
            copyTimeout.CancelAfter(TimeSpan.FromSeconds(MealPlanRules.CopyOperationTimeoutSeconds));
            ct = copyTimeout.Token;
        }
        var stopwatch = Stopwatch.StartNew();

        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var rawConnection = await _db.CreateOpenConnectionAsync(ct);
            if (rawConnection is not MySqlConnection connection)
            {
                throw new InvalidOperationException(
                    "Meal Plan writes require the configured MySQL connection.");
            }

            await using var transaction = await connection.BeginTransactionAsync(
                IsolationLevel.ReadCommitted,
                ct);
            var commitStarted = false;

            try
            {
                var lockedUserId = await connection.QuerySingleOrDefaultAsync<int?>(
                    Command(
                        MealPlanSql.LockUser,
                        new { CurrentUserId = currentUserId },
                        transaction,
                        ct));
                if (!lockedUserId.HasValue)
                    throw new UnauthorizedException();

                var persistedRevision = await connection.QuerySingleOrDefaultAsync<long?>(
                    Command(
                        MealPlanSql.GetSettingsRevision,
                        new { CurrentUserId = currentUserId },
                        transaction,
                        ct));
                var currentRevision = persistedRevision ?? 0;
                var timestampUtc = _timeProvider.GetUtcNow().UtcDateTime;
                var context = new MealPlanWriteContext(
                    connection,
                    transaction,
                    timestampUtc,
                    currentRevision,
                    ct);
                if (validateOwnership is not null)
                    await validateOwnership(context);
                if (currentRevision != expectedRevision)
                {
                    throw new ConflictException(
                        "The Meal Plan revision is stale. Refresh and review the latest state.",
                        code: "meal_plan_revision_conflict");
                }

                if (!persistedRevision.HasValue)
                {
                    await connection.ExecuteAsync(Command(
                        MealPlanSql.InsertSettingsAtRevisionZero,
                        new
                        {
                            CurrentUserId = currentUserId,
                            TimestampUtc = timestampUtc,
                        },
                        transaction,
                        ct));
                }

                var mutation = await operation(context);

                var resultRevision = currentRevision;
                if (!mutation.Changed)
                {
                    var unchangedPayload = readback is null
                        ? mutation.Payload
                        : await readback(context, resultRevision);
                    await transaction.RollbackAsync(CancellationToken.None);
                    stopwatch.Stop();
                    _logger.LogInformation(
                        "Meal Plan write completed as a no-op. Operation {Operation}; " +
                        "Revision {Revision}; RetryCount {RetryCount}; ElapsedMs {ElapsedMs}; SqlCommands {SqlCommands}; ReportedRows {ReportedRows}",
                        operationName,
                        resultRevision,
                        attempt,
                        stopwatch.ElapsedMilliseconds,
                        context.CommandCount+(persistedRevision.HasValue ? 2 : 3),context.ReportedRows);
                    return new MealPlanWriteResult<TPayload>(
                        resultRevision,
                        false,
                        unchangedPayload);
                }

                if (currentRevision == long.MaxValue)
                {
                    throw new InternalServerException(
                        "The Meal Plan revision cannot be incremented.");
                }

                var updated = await connection.ExecuteAsync(Command(
                    MealPlanSql.IncrementSettingsRevision,
                    new
                    {
                        CurrentUserId = currentUserId,
                        CurrentRevision = currentRevision,
                        TimestampUtc = timestampUtc,
                    },
                    transaction,
                    ct));
                if (updated != 1)
                {
                    throw new InternalServerException(
                        "The Meal Plan revision could not be updated.");
                }

                resultRevision = checked(currentRevision + 1);
                var payload = readback is null
                    ? mutation.Payload
                    : await readback(context, resultRevision);

                commitStarted = true;
                await transaction.CommitAsync(ct);
                stopwatch.Stop();
                _logger.LogInformation(
                    "Meal Plan write completed. Operation {Operation}; Changed {Changed}; " +
                    "Revision {Revision}; RetryCount {RetryCount}; ElapsedMs {ElapsedMs}; SqlCommands {SqlCommands}; ReportedRows {ReportedRows}",
                    operationName,
                    mutation.Changed,
                    resultRevision,
                    attempt,
                    stopwatch.ElapsedMilliseconds,
                    context.CommandCount+(persistedRevision.HasValue ? 3 : 4),context.ReportedRows);
                return new MealPlanWriteResult<TPayload>(
                    resultRevision,
                    mutation.Changed,
                    payload);
            }
            catch (MySqlException exception)
                when (exception.Number == DeadlockErrorNumber
                    && !commitStarted
                    && attempt == 0)
            {
                if (!await TryRollbackAsync(transaction))
                    throw;
                _logger.LogWarning(
                    "Meal Plan write deadlocked and will retry once. Operation {Operation}; RetryCount 1",
                    operationName);
                await Task.Delay(Random.Shared.Next(10, 31), ct);
            }
            catch
            {
                await TryRollbackAsync(transaction);
                stopwatch.Stop();
                _logger.LogWarning(
                    "Meal Plan write failed. Operation {Operation}; RetryCount {RetryCount}; ElapsedMs {ElapsedMs}",
                    operationName,
                    attempt,
                    stopwatch.ElapsedMilliseconds);
                throw;
            }
        }

        throw new InvalidOperationException("Meal Plan write retry loop exited unexpectedly.");
    }

    private static CommandDefinition Command(
        string sql,
        object? parameters,
        MySqlTransaction transaction,
        CancellationToken ct) =>
        new(
            sql,
            parameters,
            transaction,
            commandTimeout: MealPlanRules.SqlCommandTimeoutSeconds,
            cancellationToken: ct);

    private static async Task<bool> TryRollbackAsync(MySqlTransaction transaction)
    {
        try
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return true;
        }
        catch
        {
            // Connection disposal is the final cleanup path. Never mask the original failure.
            return false;
        }
    }
}

/// <summary>Represents one Infrastructure-local mutation and its in-transaction readback.</summary>
internal sealed record MealPlanMutation<TPayload>(bool Changed, TPayload Payload)
{
    internal static MealPlanMutation<TPayload> ChangedResult(TPayload payload) => new(true, payload);

    internal static MealPlanMutation<TPayload> UnchangedResult(TPayload payload) => new(false, payload);
}

/// <summary>Provides bounded commands on the one active Meal Plan write connection and transaction.</summary>
internal sealed class MealPlanWriteContext
{
    private readonly MySqlConnection _connection;
    private readonly MySqlTransaction _transaction;

    internal MealPlanWriteContext(
        MySqlConnection connection,
        MySqlTransaction transaction,
        DateTime timestampUtc,
        long revision,
        CancellationToken cancellationToken)
    {
        _connection = connection;
        _transaction = transaction;
        TimestampUtc = timestampUtc;
        Revision = revision;
        CancellationToken = cancellationToken;
    }

    internal DateTime TimestampUtc { get; }

    internal long Revision { get; }

    internal CancellationToken CancellationToken { get; }

    internal int CommandCount {get;private set;}
    // Execute reports affected rows; scalar-returning legacy inserts are not included in this metric.
    internal long ReportedRows {get;private set;}

    internal async Task<int> ExecuteAsync(string sql, object? parameters = null)
    {
        var changed=await _connection.ExecuteAsync(Command(sql,parameters));
        ReportedRows+=changed;return changed;
    }

    internal Task<T> QuerySingleAsync<T>(string sql, object? parameters = null) =>
        _connection.QuerySingleAsync<T>(Command(sql, parameters));

    internal Task<T?> QuerySingleOrDefaultAsync<T>(string sql, object? parameters = null) =>
        _connection.QuerySingleOrDefaultAsync<T>(Command(sql, parameters));

    internal async Task<IReadOnlyList<T>> QueryAsync<T>(string sql, object? parameters = null) =>
        (await _connection.QueryAsync<T>(Command(sql, parameters))).AsList();

    internal Task<SqlMapper.GridReader> QueryMultipleAsync(string sql, object? parameters = null) =>
        _connection.QueryMultipleAsync(Command(sql, parameters));

    private CommandDefinition Command(string sql, object? parameters)
    {
        CommandCount++;
        return new(
            sql,
            parameters,
            _transaction,
            commandTimeout: MealPlanRules.SqlCommandTimeoutSeconds,
            cancellationToken: CancellationToken);
    }
}

using Appetee.Api.Tests.Infrastructure;
using Appetee.Application.Models.MealPlans;
using Appetee.Application.utils;
using Appetee.Infrastructure.Data;
using Appetee.Infrastructure.MealPlans;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MySqlConnector;

namespace Appetee.Api.Tests.Integration;

public sealed class F011B03WriteProtocolTests : IntegrationTestBase
{
    private const string InsertDaySql = """
        INSERT INTO meal_plan_days (user_id, plan_date, created_at, updated_at)
        VALUES (@UserId, @PlanDate, @TimestampUtc, @TimestampUtc);
        """;

    public F011B03WriteProtocolTests(AppeteeWebApplicationFactory factory)
        : base(factory) { }

    [Fact]
    public async Task ConcurrentFirstWrites_WithSameRevision_CommitExactlyOneChange()
    {
        var first = CaptureAsync(CreateTransaction().ExecuteAsync(
            "test.concurrent-first-write",
            1,
            0,
            InsertChangedDayAsync,
            CancellationToken.None));
        var second = CaptureAsync(CreateTransaction().ExecuteAsync(
            "test.concurrent-first-write",
            1,
            0,
            InsertChangedDayAsync,
            CancellationToken.None));

        var outcomes = await Task.WhenAll(first, second);

        var success = Assert.Single(outcomes, outcome => outcome.Result is not null);
        var failure = Assert.Single(outcomes, outcome => outcome.Exception is not null);
        Assert.True(success.Result!.Changed);
        Assert.Equal(1, success.Result.Revision);
        var conflict = Assert.IsType<ConflictException>(failure.Exception);
        Assert.Equal("meal_plan_revision_conflict", conflict.Code);
        Assert.Equal(1, await Factory.Database.QuerySingleOrDefaultAsync<int>(
            "SELECT COUNT(*) FROM meal_plan_days WHERE user_id = 1 AND plan_date = '2026-10-05';"));
        Assert.Equal(1, await Factory.Database.QuerySingleOrDefaultAsync<int>(
            "SELECT COUNT(*) FROM meal_plan_user_settings WHERE user_id = 1 AND revision = 1;"));
    }

    [Fact]
    public async Task NoOp_FirstWrite_LeavesSettingsAbsentAndRevisionUnchanged()
    {
        var result = await CreateTransaction().ExecuteAsync(
            "test.no-op",
            1,
            0,
            context => Task.FromResult(MealPlanMutation<string>.UnchangedResult("unchanged")),
            CancellationToken.None);

        Assert.False(result.Changed);
        Assert.Equal(0, result.Revision);
        Assert.Equal("unchanged", result.Payload);
        Assert.Equal(0, await Factory.Database.QuerySingleOrDefaultAsync<int>(
            "SELECT COUNT(*) FROM meal_plan_user_settings WHERE user_id = 1;"));
    }

    [Fact]
    public async Task FirstWrite_CallbackCanSetCurrentOnTheNewSettingsRow()
    {
        await Factory.Database.ExecuteAsync(
            """
            INSERT INTO nutrition_plans (
                user_id, name, mode, default_meals_per_day, created_at, updated_at
            ) VALUES (1, 'First Plan', 'Easy', 3, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6));
            """);
        var planId = await Factory.Database.QuerySingleOrDefaultAsync<int>(
            "SELECT id FROM nutrition_plans WHERE user_id = 1 AND name = 'First Plan';");

        var result = await CreateTransaction().ExecuteAsync(
            "test.first-current",
            1,
            0,
            async context =>
            {
                var changed = await context.ExecuteAsync(
                    """
                    UPDATE meal_plan_user_settings
                    SET current_nutrition_plan_id = @PlanId,
                        updated_at = @TimestampUtc
                    WHERE user_id = @UserId;
                    """,
                    new { PlanId = planId, TimestampUtc = context.TimestampUtc, UserId = 1 });
                return MealPlanMutation<int>.ChangedResult(changed);
            },
            CancellationToken.None);

        Assert.Equal(1, result.Payload);
        Assert.Equal(1, result.Revision);
        Assert.Equal(planId, await Factory.Database.QuerySingleOrDefaultAsync<int>(
            "SELECT current_nutrition_plan_id FROM meal_plan_user_settings WHERE user_id = 1;"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CallbackOrReadbackFailure_RollsBackRowsAndRevision(bool failDuringReadback)
    {
        var transaction = CreateTransaction();

        if (failDuringReadback)
        {
            await Assert.ThrowsAsync<MySqlException>(() => transaction.ExecuteAsync<int>(
                "test.readback-failure",
                1,
                0,
                async context =>
                {
                    await InsertDayAsync(context);
                    await context.QuerySingleAsync<int>("SELECT value FROM missing_b03_table;");
                    return MealPlanMutation<int>.ChangedResult(1);
                },
                CancellationToken.None));
        }
        else
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => transaction.ExecuteAsync<int>(
                "test.callback-failure",
                1,
                0,
                async context =>
                {
                    await InsertDayAsync(context);
                    throw new InvalidOperationException("Injected callback failure.");
                },
                CancellationToken.None));
        }

        Assert.Equal(0, await Factory.Database.QuerySingleOrDefaultAsync<int>(
            "SELECT COUNT(*) FROM meal_plan_days WHERE user_id = 1;"));
        Assert.Equal(0, await Factory.Database.QuerySingleOrDefaultAsync<int>(
            "SELECT COUNT(*) FROM meal_plan_user_settings WHERE user_id = 1;"));
    }

    [Fact]
    public async Task Cancellation_RollsBackWithCleanupTokenAndLeavesNoSettings()
    {
        using var cancellation = new CancellationTokenSource();
        var transaction = CreateTransaction();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => transaction.ExecuteAsync<int>(
            "test.cancellation",
            1,
            0,
            async context =>
            {
                await InsertDayAsync(context);
                cancellation.Cancel();
                context.CancellationToken.ThrowIfCancellationRequested();
                return MealPlanMutation<int>.ChangedResult(1);
            },
            cancellation.Token));

        Assert.Equal(0, await Factory.Database.QuerySingleOrDefaultAsync<int>(
            "SELECT COUNT(*) FROM meal_plan_days WHERE user_id = 1;"));
        Assert.Equal(0, await Factory.Database.QuerySingleOrDefaultAsync<int>(
            "SELECT COUNT(*) FROM meal_plan_user_settings WHERE user_id = 1;"));
    }

    [Fact]
    public async Task CapturedReadback_DoesNotObserveALaterTransaction()
    {
        var result = await CreateTransaction().ExecuteAsync(
            "test.captured-readback",
            1,
            0,
            async context =>
            {
                await InsertDayAsync(context);
                var capturedCount = await context.QuerySingleAsync<int>(
                    "SELECT COUNT(*) FROM meal_plan_days WHERE user_id = @UserId;",
                    new { UserId = 1 });
                return MealPlanMutation<int>.ChangedResult(capturedCount);
            },
            CancellationToken.None);

        await Factory.Database.ExecuteAsync(
            """
            INSERT INTO meal_plan_days (user_id, plan_date, created_at, updated_at)
            VALUES (1, '2026-10-06', UTC_TIMESTAMP(6), UTC_TIMESTAMP(6));
            """);

        Assert.Equal(1, result.Payload);
        Assert.Equal(1, result.Revision);
        Assert.Equal(2, await Factory.Database.QuerySingleOrDefaultAsync<int>(
            "SELECT COUNT(*) FROM meal_plan_days WHERE user_id = 1;"));
    }

    [Fact]
    public async Task MissingSessionUser_ReturnsUnauthorizedBeforeCallback()
    {
        var callbackCalled = false;

        var exception = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            CreateTransaction().ExecuteAsync(
                "test.missing-user",
                999999,
                0,
                context =>
                {
                    callbackCalled = true;
                    return Task.FromResult(MealPlanMutation<int>.ChangedResult(1));
                },
                CancellationToken.None));

        Assert.Equal(401, exception.StatusCode);
        Assert.False(callbackCalled);
    }

    [Fact]
    public void TransactionProtocol_IsRegisteredInTheExistingInfrastructureComposition()
    {
        using var scope = Factory.Services.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<MealPlanTransaction>());
    }

    private MealPlanTransaction CreateTransaction() =>
        new(
            new DbConnectionFactory(Factory.Database.ConnectionString),
            new AdjustableTimeProvider(new DateTimeOffset(2026, 10, 5, 12, 0, 0, TimeSpan.Zero)),
            NullLogger<MealPlanTransaction>.Instance);

    [Fact]
    public async Task DeadlockError_RetriesOnceAfterRollingBackTheWholeAttempt()
    {
        var attempts = 0;
        var result = await CreateTransaction().ExecuteAsync(
            "test.deadlock-retry", 1, 0,
            async context =>
            {
                attempts++;
                await InsertDayAsync(context);
                if (attempts == 1)
                    await context.ExecuteAsync(
                        "SIGNAL SQLSTATE '40001' SET MYSQL_ERRNO = 1213, MESSAGE_TEXT = 'Injected deadlock';");
                return MealPlanMutation<int>.ChangedResult(attempts);
            }, CancellationToken.None);
        Assert.Equal(2, attempts);
        Assert.Equal(1, result.Revision);
        Assert.Equal(1, await Factory.Database.QuerySingleOrDefaultAsync<int>(
            "SELECT COUNT(*) FROM meal_plan_days WHERE user_id = 1;"));
    }

    [Fact]
    public async Task OwnerAndRevisionLookups_UseExistingPrimaryKeys()
    {
        await Factory.Database.ExecuteAsync("""
            INSERT INTO meal_plan_user_settings (user_id, revision, created_at, updated_at)
            VALUES (1, 0, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6));
            """);
        foreach (var sql in new[] { MealPlanSql.LockUser, MealPlanSql.GetSettingsRevision })
        {
            var rows = await Factory.Database.QueryAsync<LookupPlan>(
                "EXPLAIN " + sql, new { CurrentUserId = 1 });
            Assert.Equal("PRIMARY", Assert.Single(rows).Key);
        }
    }

    private sealed class LookupPlan
    {
        public string? Key { get; set; }
    }

    [Fact]
    public async Task RepeatedDeadlockError_StopsAfterOneRetryAndRollsBackBothAttempts()
    {
        var attempts = 0;
        await Assert.ThrowsAsync<MySqlException>(() => CreateTransaction().ExecuteAsync<int>(
            "test.deadlock-limit", 1, 0,
            async context =>
            {
                attempts++;
                await InsertDayAsync(context);
                await context.ExecuteAsync(
                    "SIGNAL SQLSTATE '40001' SET MYSQL_ERRNO = 1213, MESSAGE_TEXT = 'Injected deadlock';");
                return MealPlanMutation<int>.ChangedResult(attempts);
            }, CancellationToken.None));
        Assert.Equal(2, attempts);
        Assert.Equal(0, await Factory.Database.QuerySingleOrDefaultAsync<int>(
            "SELECT COUNT(*) FROM meal_plan_days WHERE user_id = 1;"));
        Assert.Equal(0, await Factory.Database.QuerySingleOrDefaultAsync<int>(
            "SELECT COUNT(*) FROM meal_plan_user_settings WHERE user_id = 1;"));
    }

    [Fact]
    public async Task CopyDeadline_CancelsWholeOperationAndRollsBack()
    {
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CreateTransaction().ExecuteAsync<int>(
            "test.copy-deadline", 1, 0,
            async context =>
            {
                await InsertDayAsync(context);
                await Task.Delay(Timeout.InfiniteTimeSpan, context.CancellationToken);
                return MealPlanMutation<int>.ChangedResult(1);
            }, CancellationToken.None, isCopy: true));
        Assert.Equal(0, await Factory.Database.QuerySingleOrDefaultAsync<int>(
            "SELECT COUNT(*) FROM meal_plan_days WHERE user_id = 1;"));
        Assert.Equal(0, await Factory.Database.QuerySingleOrDefaultAsync<int>(
            "SELECT COUNT(*) FROM meal_plan_user_settings WHERE user_id = 1;"));
    }

    [Fact]
    public async Task RevisionExhaustion_RollsBackMutationWithoutWrapping()
    {
        await Factory.Database.ExecuteAsync("""
            INSERT INTO meal_plan_user_settings (user_id, revision, created_at, updated_at)
            VALUES (1, 9223372036854775807, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6));
            """);
        await Assert.ThrowsAsync<InternalServerException>(() => CreateTransaction().ExecuteAsync(
            "test.revision-exhaustion", 1, long.MaxValue,
            InsertChangedDayAsync, CancellationToken.None));
        Assert.Equal(long.MaxValue, await Factory.Database.QuerySingleOrDefaultAsync<long>(
            "SELECT revision FROM meal_plan_user_settings WHERE user_id = 1;"));
        Assert.Equal(0, await Factory.Database.QuerySingleOrDefaultAsync<int>(
            "SELECT COUNT(*) FROM meal_plan_days WHERE user_id = 1;"));
    }

    [Fact]
    public async Task ForeignTarget_IsUnavailableBeforeStaleRevisionComparison()
    {
        await Factory.Database.ExecuteAsync("""
            INSERT INTO meal_plan_days (user_id, plan_date, created_at, updated_at)
            VALUES (2, '2026-10-05', UTC_TIMESTAMP(6), UTC_TIMESTAMP(6));
            """);
        var called = false;
        await Assert.ThrowsAsync<NotFoundException>(() => CreateTransaction().ExecuteAsync(
            "test.ownership", 1, 99,
            context =>
            {
                called = true;
                return Task.FromResult(MealPlanMutation<int>.ChangedResult(1));
            },
            CancellationToken.None,
            validateOwnership: async context =>
            {
                var id = await context.QuerySingleOrDefaultAsync<int?>(
                    "SELECT id FROM meal_plan_days WHERE user_id = @UserId AND plan_date = @Date;",
                    new { UserId = 1, Date = new DateTime(2026, 10, 5) });
                if (id is null)
                    throw new NotFoundException("Meal Plan target is unavailable.");
            }));
        Assert.False(called);
        Assert.Equal(0, await Factory.Database.QuerySingleOrDefaultAsync<int>(
            "SELECT COUNT(*) FROM meal_plan_user_settings WHERE user_id = 1;"));
    }

    [Fact]
    public async Task ReadbackFailureAfterRevisionIncrement_RestoresExistingState()
    {
        await CreateTransaction().ExecuteAsync(
            "test.seed", 1, 0, InsertChangedDayAsync, CancellationToken.None);
        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateTransaction().ExecuteAsync(
            "test.failed-readback", 1, 1,
            async context =>
            {
                await context.ExecuteAsync("DELETE FROM meal_plan_days WHERE user_id = 1;");
                return MealPlanMutation<int>.ChangedResult(0);
            }, CancellationToken.None,
            readback: async (context, revision) =>
            {
                Assert.Equal(2, revision);
                Assert.Equal(2, await context.QuerySingleAsync<long>(
                    "SELECT revision FROM meal_plan_user_settings WHERE user_id = 1;"));
                throw new InvalidOperationException("Injected readback failure after increment.");
            }));
        Assert.Equal(1, await Factory.Database.QuerySingleOrDefaultAsync<long>(
            "SELECT revision FROM meal_plan_user_settings WHERE user_id = 1;"));
        Assert.Equal(1, await Factory.Database.QuerySingleOrDefaultAsync<int>(
            "SELECT COUNT(*) FROM meal_plan_days WHERE user_id = 1;"));
    }

    private static async Task<MealPlanMutation<int>> InsertChangedDayAsync(MealPlanWriteContext context)
    {
        await InsertDayAsync(context);
        var id = await context.QuerySingleAsync<int>("SELECT LAST_INSERT_ID();");
        return MealPlanMutation<int>.ChangedResult(id);
    }

    private static Task<int> InsertDayAsync(MealPlanWriteContext context) =>
        context.ExecuteAsync(
            InsertDaySql,
            new
            {
                UserId = 1,
                PlanDate = new DateTime(2026, 10, 5),
                context.TimestampUtc,
            });

    private static async Task<WriteOutcome> CaptureAsync(Task<MealPlanWriteResult<int>> task)
    {
        try
        {
            return new WriteOutcome(await task, null);
        }
        catch (Exception exception)
        {
            return new WriteOutcome(null, exception);
        }
    }

    private sealed record WriteOutcome(
        MealPlanWriteResult<int>? Result,
        Exception? Exception);
}

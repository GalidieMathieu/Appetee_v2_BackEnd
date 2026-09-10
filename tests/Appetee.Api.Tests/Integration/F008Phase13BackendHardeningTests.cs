// Purpose: Verifies F-008 Phase 13 backend observability, final query plans/scale, and OpenAPI contracts.
// Change reason: Add final backend hardening coverage without implementing the Phase 13 frontend UI.
// Created: 2026-08-28T20:20:53-06:00
// Last updated: 2026-08-28T20:20:53-06:00

using Appetee.Api.Tests.Infrastructure;
using Appetee.Application.Models.Recipes;
using Appetee.Application.RowData;
using Appetee.Infrastructure.Data;
using Appetee.Infrastructure.Recipes;
using Dapper;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using System.Diagnostics;
using System.Text.Json;

namespace Appetee.Api.Tests.Integration;

/// <summary>Exercises the backend-only performance, observability, and contract closure required by Phase 13.</summary>
public sealed class F008Phase13BackendHardeningTests : IntegrationTestBase
{
    private const int RepresentativeRecipeCount = 5_000;

    public F008Phase13BackendHardeningTests(AppeteeWebApplicationFactory factory)
        : base(factory) { }

    [Fact]
    public async Task RecipeObservability_ReportsDurationsWithoutSensitiveCriteriaOrIdentity()
    {
        var (_, user) = await CreateAuthenticatedClientAsync(
            dietIds: [],
            ingredientRestrictionIds: []);
        var logger = new CapturingLogger<RecipeQueries>();
        var queries = new RecipeQueries(
            new DbConnectionFactory(Factory.Database.ConnectionString),
            new TestBlobStorageService(),
            logger);

        await queries.DiscoverAsync(BrowseQuery(user.userId), CancellationToken.None);
        await queries.GetPreviewAsync(user.userId, 1, CancellationToken.None);

        Assert.Contains(logger.Fields, fields => fields.ContainsKey("CandidateDurationMs"));
        Assert.Contains(logger.Fields, fields => fields.ContainsKey("HydrationDurationMs"));
        Assert.Contains(logger.Fields, fields => fields.ContainsKey("PreviewDurationMs"));
        Assert.Contains(
            logger.Fields,
            fields => string.Equals(
                Convert.ToString(fields.GetValueOrDefault("DiscoveryMode")),
                "browse",
                StringComparison.Ordinal));

        var forbiddenFields = new[]
        {
            "CurrentUserId",
            "Cursor",
            "Criteria",
            "Search",
            "SearchTerms",
            "IngredientIds",
            "DietIds",
            "RestrictionIds",
        };
        Assert.All(
            logger.Fields,
            fields => Assert.DoesNotContain(fields.Keys, forbiddenFields.Contains));
    }

    [Fact]
    public async Task FinalDiscoveryPlans_CompleteWithinRepresentativeFiveThousandRecipeBudget()
    {
        await SeedRepresentativeCatalogAsync();
        var (_, user) = await CreateAuthenticatedClientAsync(
            dietIds: [],
            ingredientRestrictionIds: []);
        await using var connection = new MySqlConnection(Factory.Database.ConnectionString);
        await connection.OpenAsync();

        var firstBrowse = await ExplainAndExecuteAsync(
            connection,
            BrowseQuery(user.userId));
        var continuationRow = firstBrowse.Rows[19];
        var secondBrowse = await ExplainAndExecuteAsync(
            connection,
            BrowseQuery(
                user.userId,
                afterRank: continuationRow.SortRank,
                afterRecipeId: continuationRow.Id));
        var oneTermSearch = await ExplainAndExecuteAsync(
            connection,
            SearchQuery(user.userId, "chicken", ["chicken"]));
        var fourTermSearch = await ExplainAndExecuteAsync(
            connection,
            SearchQuery(
                user.userId,
                "chicken rice quick meal",
                ["chicken", "rice", "quick", "meal"]));

        foreach (var result in new[]
        {
            firstBrowse,
            secondBrowse,
            oneTermSearch,
            fourTermSearch,
        })
        {
            Assert.NotEmpty(result.Plan);
            Assert.Equal(21, result.Rows.Count);
            Assert.True(
                result.Duration < TimeSpan.FromSeconds(5),
                $"Representative discovery query took {result.Duration.TotalMilliseconds:F0} ms.");
        }

        Assert.DoesNotContain(
            secondBrowse.Rows.Select(row => row.Id),
            firstBrowse.Rows.Take(20).Select(row => row.Id).Contains);
    }

    [Fact]
    public async Task OpenApi_MatchesFinalImplementedRecipeAndIngredientContracts()
    {
        using var response = await Client.GetAsync("/swagger/v1/swagger.json");
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStreamAsync());
        var root = document.RootElement;
        var paths = root.GetProperty("paths");

        AssertResponseSchema(
            paths.GetProperty("/api/recipes").GetProperty("get"),
            "200",
            "RecipeDiscoveryPageDto");
        AssertResponseSchema(
            paths.GetProperty("/api/recipes/{id}/preview").GetProperty("get"),
            "200",
            "RecipePreviewDto");
        Assert.True(
            paths.GetProperty("/api/recipes/{id}/favorite")
                .GetProperty("put")
                .GetProperty("responses")
                .TryGetProperty("204", out _));
        Assert.True(
            paths.GetProperty("/api/recipes/{id}/favorite")
                .GetProperty("delete")
                .GetProperty("responses")
                .TryGetProperty("204", out _));

        var ingredientSchema = paths
            .GetProperty("/api/ingredients")
            .GetProperty("get")
            .GetProperty("responses")
            .GetProperty("200")
            .GetProperty("content")
            .GetProperty("application/json")
            .GetProperty("schema");
        Assert.Equal("array", ingredientSchema.GetProperty("type").GetString());
        Assert.EndsWith(
            "IngredientDto",
            ingredientSchema.GetProperty("items").GetProperty("$ref").GetString(),
            StringComparison.Ordinal);

        var previewProperties = root
            .GetProperty("components")
            .GetProperty("schemas")
            .GetProperty("RecipePreviewDto")
            .GetProperty("properties")
            .EnumerateObject()
            .Select(property => property.Name)
            .Order()
            .ToArray();
        Assert.Equal(
            new[]
            {
                "badges",
                "caloriesPerServing",
                "description",
                "estimatedCostPerServing",
                "id",
                "ingredients",
                "isSaved",
                "name",
                "previewImageUrl",
                "proteinPerServing",
                "totalTimeMinutes",
            }.Order(),
            previewProperties);
    }

    /// <summary>Runs EXPLAIN and the bounded candidate command while recording only aggregate timing.</summary>
    private static async Task<QueryReviewResult> ExplainAndExecuteAsync(
        MySqlConnection connection,
        RecipeDiscoveryQuery query)
    {
        var (sql, parameters) = RecipeDiscoverySqlBuilder.Build(query);
        var plan = (await connection.QueryAsync(
                new CommandDefinition($"EXPLAIN {sql}", parameters)))
            .AsList();
        var stopwatch = Stopwatch.StartNew();
        var rows = (await connection.QueryAsync<RecipeDiscoveryRowData>(
                new CommandDefinition(sql, parameters)))
            .AsList();
        stopwatch.Stop();

        return new QueryReviewResult(plan, rows, stopwatch.Elapsed);
    }

    private static RecipeDiscoveryQuery BrowseQuery(
        int currentUserId,
        long? afterRank = null,
        int? afterRecipeId = null) =>
        new(
            CurrentUserId: currentUserId,
            PageSize: 20,
            NormalizedSearch: null,
            EffectiveSearchTerms: [],
            CanonicalBadges: [],
            MaxTotalMinutes: null,
            AllowedDifficulties: [],
            SavedOnly: false,
            BrowseSeed: 1234,
            AfterRank: afterRank,
            AfterRecipeId: afterRecipeId);

    private static RecipeDiscoveryQuery SearchQuery(
        int currentUserId,
        string normalizedSearch,
        IReadOnlyList<string> terms) =>
        new(
            CurrentUserId: currentUserId,
            PageSize: 20,
            NormalizedSearch: normalizedSearch,
            EffectiveSearchTerms: terms,
            CanonicalBadges: [],
            MaxTotalMinutes: null,
            AllowedDifficulties: [],
            SavedOnly: false,
            BrowseSeed: null,
            AfterRank: null,
            AfterRecipeId: null);

    /// <summary>Adds enough compatible rows to exercise the approved maximum catalogue scale.</summary>
    private async Task SeedRepresentativeCatalogAsync()
    {
        var recipes = Enumerable.Range(2, RepresentativeRecipeCount - 1)
            .Select(id => new
            {
                Id = id,
                Name = $"Chicken Rice Quick Meal Recipe {id}",
            })
            .ToArray();

        await Factory.Database.ExecuteAsync(
            """
            INSERT INTO recipes (
                id,
                name,
                description,
                image_blob_name,
                instructions,
                prep_time_minutes,
                cook_time_minutes,
                total_time_minutes,
                servings,
                difficulty,
                estimated_cost_per_serving,
                calories_total,
                protein_total,
                carbs_total,
                calories_per_serving,
                protein_per_serving
            ) VALUES (
                @Id,
                @Name,
                'Phase 13 representative catalogue fixture.',
                NULL,
                JSON_ARRAY(JSON_OBJECT('title', 'Cook', 'instruction', 'Cook.')),
                10,
                20,
                30,
                2,
                'Easy',
                4.00,
                800.00,
                60.00,
                100.00,
                400.00,
                30.00
            );
            """,
            recipes);
    }

    /// <summary>Checks that an OpenAPI success response points to the approved DTO schema.</summary>
    private static void AssertResponseSchema(
        JsonElement operation,
        string statusCode,
        string expectedSchema)
    {
        var schemaReference = operation
            .GetProperty("responses")
            .GetProperty(statusCode)
            .GetProperty("content")
            .GetProperty("application/json")
            .GetProperty("schema")
            .GetProperty("$ref")
            .GetString();
        Assert.EndsWith(expectedSchema, schemaReference, StringComparison.Ordinal);
    }

    /// <summary>Captures structured log fields so privacy and performance contracts can be asserted.</summary>
    private sealed class CapturingLogger<T> : ILogger<T>
    {
        internal List<IReadOnlyDictionary<string, object?>> Fields { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull =>
            null;

        public bool IsEnabled(LogLevel logLevel) => true;

        /// <summary>Stores structured state fields without depending on formatted log text.</summary>
        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (state is not IEnumerable<KeyValuePair<string, object?>> properties)
                return;

            Fields.Add(properties.ToDictionary(property => property.Key, property => property.Value));
        }
    }

    /// <summary>Pairs a MySQL plan with bounded result rows and measured execution duration.</summary>
    private sealed record QueryReviewResult(
        IReadOnlyList<dynamic> Plan,
        IReadOnlyList<RecipeDiscoveryRowData> Rows,
        TimeSpan Duration);
}

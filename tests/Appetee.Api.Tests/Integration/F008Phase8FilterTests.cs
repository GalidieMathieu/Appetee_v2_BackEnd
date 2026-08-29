// Purpose: Verifies F-008 Phase 8 filters, pagination, API validation, and SQL plans through MySQL.
// Change reason: Add end-to-end coverage for badge-AND, maximum-time, and maximum-difficulty discovery behavior.
// Created: 2026-08-27T10:48:05-06:00
// Last updated: 2026-08-27T10:48:05-06:00

using Appetee.Api.Tests.Infrastructure;
using Appetee.Application.Dtos;
using Appetee.Application.Models.Recipes;
using Appetee.Infrastructure.Recipes;
using Dapper;
using MySqlConnector;
using System.Net;
using System.Text.Json;

namespace Appetee.Api.Tests.Integration;

/// <summary>Exercises Phase 8 filters against authenticated API requests and the real MySQL query shape.</summary>
public sealed class F008Phase8FilterTests : IntegrationTestBase
{
    public F008Phase8FilterTests(AppeteeWebApplicationFactory factory)
        : base(factory) { }

    [Fact]
    public async Task Filters_ApplyBadgeAndTimeAndExplicitDifficultySemantics()
    {
        await SeedFilterRecipesAsync();
        var (authClient, _) = await CreateAuthenticatedClientAsync(
            dietIds: [2, 3],
            ingredientRestrictionIds: []);
        using var client = authClient;

        var bothBadges = await GetPageAsync(
            client,
            "/api/recipes?badges=High%20Protein&badges=Budget%20Friendly");
        var withinTwentyFiveMinutes = await GetPageAsync(
            client,
            "/api/recipes?maxTotalMinutes=25");
        var mediumOrEasier = await GetPageAsync(
            client,
            "/api/recipes?maxDifficulty=Medium");
        var hardOrEasier = await GetPageAsync(
            client,
            "/api/recipes?maxDifficulty=Hard");
        var unsetDifficulty = await GetPageAsync(client, "/api/recipes");

        Assert.Equal([1, 3], SortedIds(bothBadges));
        Assert.Equal([1, 3], SortedIds(withinTwentyFiveMinutes));
        Assert.Equal([1, 2, 4], SortedIds(mediumOrEasier));
        Assert.Equal([1, 2, 3, 4], SortedIds(hardOrEasier));
        Assert.Equal(SortedIds(unsetDifficulty), SortedIds(hardOrEasier));
    }

    [Fact]
    public async Task Filters_ComposeWithSeededBrowsePaginationAndRankedSearch()
    {
        await SeedFilterRecipesAsync();
        var (authClient, _) = await CreateAuthenticatedClientAsync(
            dietIds: [2, 3],
            ingredientRestrictionIds: []);
        using var client = authClient;

        var firstPage = await GetPageAsync(
            client,
            "/api/recipes?badges=Budget%20Friendly&limit=1");
        var secondPage = await GetPageAsync(
            client,
            $"/api/recipes?badges=Budget%20Friendly&limit=1&cursor={Uri.EscapeDataString(firstPage.NextCursor!)}");
        var searched = await GetPageAsync(
            client,
            "/api/recipes?search=Phase%20Eight%20Chicken&badges=High%20Protein&maxDifficulty=Medium");

        Assert.True(firstPage.HasMore);
        Assert.NotNull(firstPage.NextCursor);
        Assert.NotEqual(
            Assert.Single(firstPage.Items).Id,
            Assert.Single(secondPage.Items).Id);
        Assert.Equal(2, Assert.Single(searched.Items).Id);
    }

    [Fact]
    public async Task FilterApi_RejectsInvalidValuesAndPublishesTheQueryContract()
    {
        var (authClient, _) = await CreateAuthenticatedClientAsync(
            dietIds: [2, 3],
            ingredientRestrictionIds: []);
        using var client = authClient;
        var invalidUrls = new[]
        {
            "/api/recipes?badges=high%20protein",
            "/api/recipes?badges=Unknown",
            "/api/recipes?maxTotalMinutes=0",
            "/api/recipes?maxTotalMinutes=1441",
            "/api/recipes?maxDifficulty=999",
        };

        foreach (var url in invalidUrls)
        {
            using var response = await client.GetAsync(url);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        using var openApiResponse = await Client.GetAsync("/swagger/v1/swagger.json");
        openApiResponse.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(
            await openApiResponse.Content.ReadAsStreamAsync());
        var parameters = document.RootElement
            .GetProperty("paths")
            .GetProperty("/api/recipes")
            .GetProperty("get")
            .GetProperty("parameters")
            .EnumerateArray()
            .ToDictionary(
                parameter => parameter.GetProperty("name").GetString()!,
                parameter => parameter);

        Assert.Equal(
            "array",
            parameters["badges"].GetProperty("schema").GetProperty("type").GetString());
        Assert.Contains("maxTotalMinutes", parameters.Keys);
        Assert.Contains("maxDifficulty", parameters.Keys);
        Assert.DoesNotContain("userId", parameters.Keys);
    }

    [Fact]
    public async Task CombinedFilterSql_ExplainsWithTheBadgePrimaryKeyPath()
    {
        await SeedFilterRecipesAsync();
        var (authClient, user) = await CreateAuthenticatedClientAsync(
            dietIds: [2, 3],
            ingredientRestrictionIds: []);
        using var client = authClient;
        var query = new RecipeDiscoveryQuery(
            CurrentUserId: user.userId,
            PageSize: 20,
            NormalizedSearch: null,
            EffectiveSearchTerms: [],
            CanonicalBadges: [RecipeBadgeValues.HighProtein, RecipeBadgeValues.BudgetFriendly],
            MaxTotalMinutes: 45,
            AllowedDifficulties: ["Easy", "Medium"],
            SavedOnly: false,
            BrowseSeed: 1234,
            AfterRank: null,
            AfterRecipeId: null);
        var (sql, parameters) = RecipeDiscoverySqlBuilder.Build(query);

        await using var connection = new MySqlConnection(Factory.Database.ConnectionString);
        await connection.OpenAsync();
        var rows = (await connection.QueryAsync(
            new CommandDefinition($"EXPLAIN {sql}", parameters)))
            .Cast<IDictionary<string, object>>()
            .ToArray();
        var badgePlan = rows.Single(
            row => string.Equals(
                Convert.ToString(row["table"]),
                "rb_filter",
                StringComparison.OrdinalIgnoreCase));

        Assert.NotEmpty(rows);
        Assert.Equal("PRIMARY", Convert.ToString(badgePlan["key"]));
    }

    /// <summary>Adds compatible fixtures that isolate badge, time, and difficulty boundary behavior.</summary>
    private async Task SeedFilterRecipesAsync()
    {
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
            ) VALUES
                (2, 'Phase Eight Chicken Easy', 'Phase 8 easy fixture.', NULL,
                    JSON_ARRAY(JSON_OBJECT('title', 'Cook', 'instruction', 'Cook.')),
                    10, 30, 40, 2, 'Easy', 4.00, 800.00, 60.00, 100.00, 400.00, 30.00),
                (3, 'Phase Eight Chicken Hard', 'Phase 8 hard fixture.', NULL,
                    JSON_ARRAY(JSON_OBJECT('title', 'Cook', 'instruction', 'Cook.')),
                    5, 15, 20, 2, 'Hard', 4.00, 800.00, 60.00, 100.00, 400.00, 30.00),
                (4, 'Phase Eight Fiber Easy', 'Phase 8 long fixture.', NULL,
                    JSON_ARRAY(JSON_OBJECT('title', 'Cook', 'instruction', 'Cook.')),
                    20, 40, 60, 2, 'Easy', 4.00, 800.00, 60.00, 100.00, 400.00, 30.00);

            INSERT INTO diet_recipes (recipe_id, diet_id) VALUES
                (2, 2), (2, 3),
                (3, 2), (3, 3),
                (4, 2), (4, 3);

            INSERT INTO recipe_badges (recipe_id, badge) VALUES
                (2, 'High Protein'),
                (3, 'High Protein'), (3, 'Budget Friendly'),
                (4, 'High Fiber'), (4, 'Budget Friendly');

            INSERT INTO recipe_ingredients (
                recipe_id,
                ingredient_id,
                quantity,
                unit,
                display_order,
                featured_order,
                note
            ) VALUES
                (2, 2, 100.000, 'g', 1, 1, NULL),
                (3, 2, 100.000, 'g', 1, 1, NULL),
                (4, 2, 100.000, 'g', 1, 1, NULL);
            """);
    }

    private static int[] SortedIds(RecipeDiscoveryPageDto page) =>
        page.Items.Select(item => item.Id).Order().ToArray();

    /// <summary>Reads and deserializes one successful discovery page while preserving failures in assertions.</summary>
    private static async Task<RecipeDiscoveryPageDto> GetPageAsync(
        HttpClient client,
        string url)
    {
        using var response = await client.GetAsync(url);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);

        return JsonSerializer.Deserialize<RecipeDiscoveryPageDto>(
                body,
                new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException("Discovery response was empty.");
    }
}

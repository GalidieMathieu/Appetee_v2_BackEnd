// Purpose: Verifies F-008 Phase 10 ingredient ALL/ANY filtering, compatibility, API validation, and SQL plans.
// Change reason: Add end-to-end backend coverage for repeated selected ingredient IDs in recipe discovery.
// Created: 2026-08-28T08:53:55-06:00
// Last updated: 2026-08-28T08:53:55-06:00

using Appetee.Api.Tests.Infrastructure;
using Appetee.Application.Dtos;
using Appetee.Application.Models.Recipes;
using Appetee.Infrastructure.Recipes;
using Dapper;
using MySqlConnector;
using System.Net;
using System.Text.Json;

namespace Appetee.Api.Tests.Integration;

/// <summary>Exercises selected-ingredient semantics through authenticated HTTP requests and real MySQL.</summary>
public sealed class F008Phase10IngredientFilterTests : IntegrationTestBase
{
    public F008Phase10IngredientFilterTests(AppeteeWebApplicationFactory factory)
        : base(factory) { }

    [Fact]
    public async Task IngredientFilter_DefaultAndExplicitAllRequireEveryIdWhileAnyRequiresOne()
    {
        await SeedIngredientFilterRecipesAsync();
        var (authClient, _) = await CreateAuthenticatedClientAsync(
            dietIds: [2, 3],
            ingredientRestrictionIds: []);
        using var client = authClient;

        var defaultAll = await GetPageAsync(
            client,
            "/api/recipes?ingredientIds=1&ingredientIds=2&ingredientIds=3");
        var explicitAll = await GetPageAsync(
            client,
            "/api/recipes?ingredientIds=1&ingredientIds=2&ingredientIds=3&requireAllIngredients=true");
        var any = await GetPageAsync(
            client,
            "/api/recipes?ingredientIds=1&ingredientIds=2&ingredientIds=3&requireAllIngredients=false");
        var duplicates = await GetPageAsync(
            client,
            "/api/recipes?ingredientIds=1&ingredientIds=1&ingredientIds=2");

        Assert.Equal([1, 5], SortedIds(defaultAll));
        Assert.Equal(SortedIds(defaultAll), SortedIds(explicitAll));
        Assert.Equal([1, 2, 3, 4, 5], SortedIds(any));
        Assert.Equal([1, 2, 5], SortedIds(duplicates));
    }

    [Fact]
    public async Task IngredientFilter_ComposesWithSearchAndNeverBypassesPermanentRestrictions()
    {
        await SeedIngredientFilterRecipesAsync();
        var (compatibleClient, _) = await CreateAuthenticatedClientAsync(
            dietIds: [2, 3],
            ingredientRestrictionIds: []);
        var (restrictedClient, _) = await CreateAuthenticatedClientAsync(
            dietIds: [2, 3],
            ingredientRestrictionIds: [3]);
        using var compatible = compatibleClient;
        using var restricted = restrictedClient;

        var searched = await GetPageAsync(
            compatible,
            "/api/recipes?search=Phase%20Ten&ingredientIds=2&ingredientIds=3");
        var restrictionScoped = await GetPageAsync(
            restricted,
            "/api/recipes?ingredientIds=1&requireAllIngredients=false");

        Assert.Equal([4, 5], SortedIds(searched));
        Assert.Equal(2, Assert.Single(restrictionScoped.Items).Id);
    }

    [Fact]
    public async Task IngredientFilterApi_RejectsInvalidIdsAndPublishesRepeatedIdAndModeContract()
    {
        var (authClient, _) = await CreateAuthenticatedClientAsync(
            dietIds: [2, 3],
            ingredientRestrictionIds: []);
        using var client = authClient;
        var invalidUrls = new[]
        {
            "/api/recipes?ingredientIds=0",
            "/api/recipes?ingredientIds=-1",
            "/api/recipes?ingredientIds=1&ingredientIds=2&ingredientIds=3&ingredientIds=4",
            "/api/recipes?ingredientIds=1&ingredientIds=1&ingredientIds=1&ingredientIds=1",
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
            parameters["ingredientIds"].GetProperty("schema").GetProperty("type").GetString());
        Assert.Equal(
            "boolean",
            parameters["requireAllIngredients"].GetProperty("schema").GetProperty("type").GetString());
        Assert.True(
            parameters["requireAllIngredients"].GetProperty("schema").GetProperty("default").GetBoolean());
        Assert.DoesNotContain("userId", parameters.Keys);
    }

    [Fact]
    public async Task IngredientFilterSql_AllAndAnyExplainWithRelationshipPrimaryKeyPath()
    {
        await SeedIngredientFilterRecipesAsync();
        var (authClient, user) = await CreateAuthenticatedClientAsync(
            dietIds: [2, 3],
            ingredientRestrictionIds: []);
        using var client = authClient;

        var allPlan = await ExplainIngredientFilterAsync(user.userId, requireAll: true);
        var anyPlan = await ExplainIngredientFilterAsync(user.userId, requireAll: false);
        var allFilterPlan = FilterPlan(allPlan);
        var anyFilterPlan = FilterPlan(anyPlan);

        Assert.Contains("PRIMARY", Convert.ToString(allFilterPlan["possible_keys"]));
        Assert.Contains("PRIMARY", Convert.ToString(anyFilterPlan["possible_keys"]));
        Assert.False(string.IsNullOrWhiteSpace(Convert.ToString(allFilterPlan["key"])));
        Assert.False(string.IsNullOrWhiteSpace(Convert.ToString(anyFilterPlan["key"])));
        Assert.Contains(
            allPlan,
            row => string.Equals(
                Convert.ToString(row["table"]),
                "ri_restriction",
                StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Runs EXPLAIN for one validated ALL or ANY selected-ingredient query.</summary>
    private async Task<IDictionary<string, object>[]> ExplainIngredientFilterAsync(
        int currentUserId,
        bool requireAll)
    {
        var query = new RecipeDiscoveryQuery(
            CurrentUserId: currentUserId,
            PageSize: 20,
            NormalizedSearch: null,
            EffectiveSearchTerms: [],
            CanonicalBadges: [],
            MaxTotalMinutes: null,
            AllowedDifficulties: [],
            SavedOnly: false,
            BrowseSeed: 1234,
            AfterRank: null,
            AfterRecipeId: null)
        {
            IngredientIds = [1, 2, 3],
            RequireAllIngredients = requireAll,
        };
        var (sql, parameters) = RecipeDiscoverySqlBuilder.Build(query);

        await using var connection = new MySqlConnection(Factory.Database.ConnectionString);
        await connection.OpenAsync();
        return (await connection.QueryAsync(
                new CommandDefinition($"EXPLAIN {sql}", parameters)))
            .Cast<IDictionary<string, object>>()
            .ToArray();
    }

    /// <summary>Finds the selected-ingredient relationship access row in a MySQL EXPLAIN result.</summary>
    private static IDictionary<string, object> FilterPlan(
        IEnumerable<IDictionary<string, object>> plan) =>
        plan.Single(
            row => string.Equals(
                Convert.ToString(row["table"]),
                "ri_filter",
                StringComparison.OrdinalIgnoreCase));

    /// <summary>Adds compatible recipes with controlled overlapping ingredient sets for ALL/ANY boundaries.</summary>
    private async Task SeedIngredientFilterRecipesAsync()
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
                (2, 'Phase Ten Alpha Bowl', 'Phase 10 ingredients 1 and 2.', NULL,
                    JSON_ARRAY(JSON_OBJECT('title', 'Cook', 'instruction', 'Cook.')),
                    10, 20, 30, 2, 'Easy', 4.00, 800.00, 60.00, 100.00, 400.00, 30.00),
                (3, 'Phase Ten Beta Bowl', 'Phase 10 ingredients 1 and 3.', NULL,
                    JSON_ARRAY(JSON_OBJECT('title', 'Cook', 'instruction', 'Cook.')),
                    10, 20, 30, 2, 'Easy', 4.00, 800.00, 60.00, 100.00, 400.00, 30.00),
                (4, 'Phase Ten Gamma Bowl', 'Phase 10 ingredients 2 and 3.', NULL,
                    JSON_ARRAY(JSON_OBJECT('title', 'Cook', 'instruction', 'Cook.')),
                    10, 20, 30, 2, 'Easy', 4.00, 800.00, 60.00, 100.00, 400.00, 30.00),
                (5, 'Phase Ten Delta Bowl', 'Phase 10 ingredients 1, 2, and 3.', NULL,
                    JSON_ARRAY(JSON_OBJECT('title', 'Cook', 'instruction', 'Cook.')),
                    10, 20, 30, 2, 'Easy', 4.00, 800.00, 60.00, 100.00, 400.00, 30.00);

            INSERT INTO diet_recipes (recipe_id, diet_id) VALUES
                (2, 2), (2, 3),
                (3, 2), (3, 3),
                (4, 2), (4, 3),
                (5, 2), (5, 3);

            INSERT INTO recipe_ingredients (
                recipe_id,
                ingredient_id,
                quantity,
                unit,
                display_order,
                featured_order,
                note
            ) VALUES
                (2, 1, 100.000, 'g', 1, 1, NULL),
                (2, 2, 100.000, 'g', 2, 2, NULL),
                (3, 1, 100.000, 'g', 1, 1, NULL),
                (3, 3, 100.000, 'g', 2, 2, NULL),
                (4, 2, 100.000, 'g', 1, 1, NULL),
                (4, 3, 100.000, 'g', 2, 2, NULL),
                (5, 1, 100.000, 'g', 1, 1, NULL),
                (5, 2, 100.000, 'g', 2, 2, NULL),
                (5, 3, 100.000, 'g', 3, 3, NULL);
            """);
    }

    private static int[] SortedIds(RecipeDiscoveryPageDto page) =>
        page.Items.Select(item => item.Id).Order().ToArray();

    /// <summary>Reads and deserializes one successful authenticated discovery page.</summary>
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

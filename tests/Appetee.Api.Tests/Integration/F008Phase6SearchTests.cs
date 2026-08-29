// Purpose: Verifies F-008 Phase 6 text relevance and search cursor behavior through the authenticated API.
// Change reason: Supply neutral Phase 8 filter state to the expanded discovery query contract.
// Created: 2026-08-26T09:45:15-06:00
// Last updated: 2026-08-27T10:48:05-06:00

using Appetee.Api.Tests.Infrastructure;
using Appetee.Application.Dtos;
using Appetee.Application.Models.Recipes;
using Appetee.Application.RowData;
using Appetee.Application.Services.Recipes;
using Appetee.Infrastructure.Recipes;
using Dapper;
using MySqlConnector;
using System.Diagnostics;
using System.Net;
using System.Text.Json;

namespace Appetee.Api.Tests.Integration;

/// <summary>Exercises case-insensitive AND search, relevance, compatibility, literal matching, and keyset traversal.</summary>
public sealed class F008Phase6SearchTests : IntegrationTestBase
{
    public F008Phase6SearchTests(AppeteeWebApplicationFactory factory)
        : base(factory) { }

    [Fact]
    public async Task Search_IsCaseInsensitiveRequiresEveryTermAndIgnoresTermsAfterFour()
    {
        await SeedRecipeAsync(10, "Chicken Skillet", [2, 3], [1, 2]);
        var (authClient, _) = await CreateAuthenticatedClientAsync(
            dietIds: new[] { 2, 3 },
            ingredientRestrictionIds: Array.Empty<int>());
        using var client = authClient;

        var partial = await SearchAsync(client, "CHICK");
        var allTerms = await SearchAsync(client, "chicken broccoli");
        var firstFour = await SearchAsync(
            client,
            "chicken rice broccoli bowl impossible");

        Assert.Contains(partial.Items, item => item.Id == 1);
        Assert.Contains(partial.Items, item => item.Id == 10);
        Assert.Contains(allTerms.Items, item => item.Id == 1);
        Assert.DoesNotContain(allTerms.Items, item => item.Id == 10);
        Assert.Contains(firstFour.Items, item => item.Id == 1);
    }

    [Fact]
    public async Task Search_ExactFullNameRanksFirstAndTitleMatchOutranksIngredientOnlyMatch()
    {
        await SeedRecipeAsync(10, "Protein Plate", [2, 3], [1]);
        var (authClient, _) = await CreateAuthenticatedClientAsync(
            dietIds: new[] { 2, 3 },
            ingredientRestrictionIds: Array.Empty<int>());
        using var client = authClient;

        var exact = await SearchAsync(client, "Chicken Rice Bowl");
        var titleVsIngredient = await SearchAsync(client, "chicken");

        Assert.Equal(1, Assert.Single(exact.Items).Id);
        Assert.True(
            IndexOf(titleVsIngredient.Items, 1) < IndexOf(titleVsIngredient.Items, 10));
    }

    [Fact]
    public async Task SearchCursor_TraversesTiedScoresDeterministicallyWithoutDuplicates()
    {
        await SeedRecipeAsync(10, "Chicken Tie A", [2, 3], [2]);
        await SeedRecipeAsync(11, "Chicken Tie B", [2, 3], [2]);
        await SeedRecipeAsync(12, "Chicken Tie C", [2, 3], [2]);
        var (authClient, _) = await CreateAuthenticatedClientAsync(
            dietIds: new[] { 2, 3 },
            ingredientRestrictionIds: Array.Empty<int>());
        using var client = authClient;

        var ids = new List<int>();
        string? cursor = null;
        do
        {
            var url = "/api/recipes?search=chicken&limit=2";
            if (cursor is not null)
                url += $"&cursor={Uri.EscapeDataString(cursor)}";

            var page = await GetPageAsync(client, url);
            ids.AddRange(page.Items.Select(item => item.Id));
            if (page.HasMore)
            {
                Assert.NotNull(page.NextCursor);
                RecipeDiscoveryCursorCodec.DecodeSearch(page.NextCursor);
            }

            cursor = page.NextCursor;
        }
        while (cursor is not null);

        Assert.Equal(new[] { 1, 12, 11, 10 }, ids);
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public async Task Search_KeepsCompatibilityCumulativeAndBindsCursorToSearchCriteria()
    {
        await SeedRecipeAsync(20, "Chicken Forbidden", [1], [1]);
        await SeedRecipeAsync(21, "Chicken Compatible", [2, 3], [1]);
        var (authClient, _) = await CreateAuthenticatedClientAsync(
            dietIds: new[] { 2, 3 },
            ingredientRestrictionIds: Array.Empty<int>());
        using var client = authClient;

        var all = await SearchAsync(client, "chicken");
        var first = await GetPageAsync(client, "/api/recipes?search=chicken&limit=1");
        using var changedSearchResponse = await client.GetAsync(
            $"/api/recipes?search=rice&limit=1&cursor={Uri.EscapeDataString(first.NextCursor!)}");

        Assert.Contains(all.Items, item => item.Id == 1);
        Assert.Contains(all.Items, item => item.Id == 21);
        Assert.DoesNotContain(all.Items, item => item.Id == 20);
        Assert.Equal(HttpStatusCode.BadRequest, changedSearchResponse.StatusCode);
    }

    [Fact]
    public async Task Search_TreatsPercentUnderscoreAndBackslashAsLiterals()
    {
        await SeedRecipeAsync(30, "Percent 100% Dish", [2, 3], [2]);
        await SeedRecipeAsync(31, "Under_score Dish", [2, 3], [2]);
        await SeedRecipeAsync(32, @"Back\slash Dish", [2, 3], [2]);
        var (authClient, _) = await CreateAuthenticatedClientAsync(
            dietIds: new[] { 2, 3 },
            ingredientRestrictionIds: Array.Empty<int>());
        using var client = authClient;

        var percent = await SearchAsync(client, "%");
        var underscore = await SearchAsync(client, "_");
        var backslash = await SearchAsync(client, @"\");

        Assert.Equal(30, Assert.Single(percent.Items).Id);
        Assert.Equal(31, Assert.Single(underscore.Items).Id);
        Assert.Equal(32, Assert.Single(backslash.Items).Id);
    }

    [Fact]
    public async Task EmptySearchUsesFreshBrowseChainAndOversizedSearchIsRejected()
    {
        await SeedRecipeAsync(40, "Another Recipe", [2, 3], [2]);
        var (authClient, _) = await CreateAuthenticatedClientAsync(
            dietIds: new[] { 2, 3 },
            ingredientRestrictionIds: Array.Empty<int>());
        using var client = authClient;

        var first = await GetPageAsync(client, "/api/recipes?search=%20%20&limit=1");
        var second = await GetPageAsync(client, "/api/recipes?search=%20%20&limit=1");
        var firstCursor = RecipeDiscoveryCursorCodec.DecodeBrowse(first.NextCursor!);
        var secondCursor = RecipeDiscoveryCursorCodec.DecodeBrowse(second.NextCursor!);
        var oversized = new string('a', 101);
        using var oversizedResponse = await client.GetAsync(
            $"/api/recipes?search={oversized}");

        Assert.NotEqual(firstCursor.Seed, secondCursor.Seed);
        Assert.Equal(HttpStatusCode.BadRequest, oversizedResponse.StatusCode);
    }

    [Fact]
    public async Task SearchCandidateQuery_HasExplainPlanAndCompletesWithinFocusedBudget()
    {
        await SeedSearchCatalogAsync(1700);
        var (authClient, authResult) = await CreateAuthenticatedClientAsync(
            dietIds: new[] { 2, 3 },
            ingredientRestrictionIds: Array.Empty<int>());
        using var client = authClient;
        var (sql, parameters) = RecipeDiscoverySqlBuilder.Build(
            new RecipeDiscoveryQuery(
                CurrentUserId: authResult.userId,
                PageSize: 20,
                NormalizedSearch: "chicken",
                EffectiveSearchTerms: ["chicken"],
                CanonicalBadges: [],
                MaxTotalMinutes: null,
                AllowedDifficulties: [],
                SavedOnly: false,
                BrowseSeed: null,
                AfterRank: null,
                AfterRecipeId: null));

        await using var connection = new MySqlConnection(Factory.Database.ConnectionString);
        await connection.OpenAsync();
        var plan = (await connection.QueryAsync(
            new CommandDefinition($"EXPLAIN {sql}", parameters))).AsList();
        var stopwatch = Stopwatch.StartNew();
        var rows = (await connection.QueryAsync<RecipeDiscoveryRowData>(
            new CommandDefinition(sql, parameters))).AsList();
        stopwatch.Stop();

        Assert.NotEmpty(plan);
        Assert.Equal(21, rows.Count);
        Assert.True(
            stopwatch.Elapsed < TimeSpan.FromSeconds(2),
            $"Focused search query took {stopwatch.Elapsed.TotalMilliseconds:F0} ms.");
    }

    private async Task SeedRecipeAsync(
        int id,
        string name,
        IReadOnlyList<int> dietIds,
        IReadOnlyList<int> ingredientIds)
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
            ) VALUES (
                @Id,
                @Name,
                'Phase 6 search fixture.',
                NULL,
                JSON_ARRAY(JSON_OBJECT('title', 'Cook', 'instruction', 'Cook the recipe.')),
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
            new { Id = id, Name = name });
        await Factory.Database.ExecuteAsync(
            """
            INSERT INTO diet_recipes (recipe_id, diet_id)
            VALUES (@RecipeId, @DietId);
            """,
            dietIds.Select(dietId => new { RecipeId = id, DietId = dietId }).ToArray());
        await Factory.Database.ExecuteAsync(
            """
            INSERT INTO recipe_ingredients (
                recipe_id,
                ingredient_id,
                quantity,
                unit,
                display_order,
                featured_order,
                note
            ) VALUES (
                @RecipeId,
                @IngredientId,
                100.000,
                'g',
                @DisplayOrder,
                @FeaturedOrder,
                NULL
            );
            """,
            ingredientIds.Select((ingredientId, index) => new
            {
                RecipeId = id,
                IngredientId = ingredientId,
                DisplayOrder = index + 1,
                FeaturedOrder = index < 3 ? index + 1 : (int?)null,
            }).ToArray());
    }

    private async Task SeedSearchCatalogAsync(int count)
    {
        var recipes = Enumerable.Range(1000, count)
            .Select(id => new
            {
                Id = id,
                Name = $"Chicken Catalog Recipe {id}",
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
                'Phase 6 timing fixture.',
                NULL,
                JSON_ARRAY(JSON_OBJECT('title', 'Cook', 'instruction', 'Cook the recipe.')),
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
        await Factory.Database.ExecuteAsync(
            """
            INSERT INTO diet_recipes (recipe_id, diet_id)
            VALUES (@Id, 2), (@Id, 3);
            """,
            recipes);
        await Factory.Database.ExecuteAsync(
            """
            INSERT INTO recipe_ingredients (
                recipe_id,
                ingredient_id,
                quantity,
                unit,
                display_order,
                featured_order,
                note
            ) VALUES (@Id, 2, 100.000, 'g', 1, 1, NULL);
            """,
            recipes);
    }

    private static Task<RecipeDiscoveryPageDto> SearchAsync(
        HttpClient client,
        string search) =>
        GetPageAsync(
            client,
            $"/api/recipes?search={Uri.EscapeDataString(search)}");

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

    private static int IndexOf(IReadOnlyList<RecipeCardDto> items, int recipeId)
    {
        for (var index = 0; index < items.Count; index++)
        {
            if (items[index].Id == recipeId)
                return index;
        }

        return int.MaxValue;
    }
}

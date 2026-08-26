// Purpose: Verifies F-008 Phase 4 seeded keyset pagination through the authenticated API.
// Created: 2026-08-25T23:50:21-06:00
// Last updated: 2026-08-26T00:08:00-06:00

using Appetee.Api.Tests.Infrastructure;
using Appetee.Application.Dtos;
using Appetee.Application.Services.Recipes;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Appetee.Api.Tests.Integration;

/// <summary>Exercises stable browse traversal, cursor validation, and user/criteria isolation.</summary>
public sealed class F008Phase4PaginationTests : IntegrationTestBase
{
    private const int CompatibleRecipeCount = 55;
    private const string ValidCriteria = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";

    public F008Phase4PaginationTests(AppeteeWebApplicationFactory factory)
        : base(factory) { }

    [Fact]
    public async Task Discovery_TraversesAllEligibleRecipesWithoutDuplicatesOrSkips()
    {
        await SeedCompatibleRecipesAsync(CompatibleRecipeCount);
        var (authClient, _) = await CreateAuthenticatedClientAsync(
            dietIds: new[] { 2, 3 },
            ingredientRestrictionIds: Array.Empty<int>());
        using var client = authClient;

        var ids = new List<int>();
        string? cursor = null;
        int? browseSeed = null;
        var pageNumber = 0;

        do
        {
            var url = cursor is null
                ? "/api/recipes"
                : $"/api/recipes?cursor={Uri.EscapeDataString(cursor)}";
            var page = await GetPageAsync(client, url);

            Assert.NotNull(page);
            Assert.InRange(page!.Items.Count, 1, 20);
            ids.AddRange(page.Items.Select(item => item.Id));
            pageNumber++;

            if (page.HasMore)
            {
                Assert.NotNull(page.NextCursor);
                var decoded = RecipeDiscoveryCursorCodec.DecodeBrowse(page.NextCursor);
                browseSeed ??= decoded.Seed;
                Assert.Equal(browseSeed, decoded.Seed);
            }
            else
            {
                Assert.Null(page.NextCursor);
            }

            cursor = page.NextCursor;
            Assert.InRange(pageNumber, 1, 4);
        }
        while (cursor is not null);

        Assert.Equal(3, pageNumber);
        Assert.Equal(CompatibleRecipeCount, ids.Count);
        Assert.Equal(CompatibleRecipeCount, ids.Distinct().Count());
        Assert.Equal(
            Enumerable.Range(1, CompatibleRecipeCount),
            ids.Order());
    }

    [Fact]
    public async Task Discovery_FreshFirstRequestsGenerateDifferentBrowseOrders()
    {
        await SeedCompatibleRecipesAsync(CompatibleRecipeCount);
        var (authClient, _) = await CreateAuthenticatedClientAsync(
            dietIds: new[] { 2, 3 },
            ingredientRestrictionIds: Array.Empty<int>());
        using var client = authClient;

        var first = await GetPageAsync(client, "/api/recipes");
        var second = await GetPageAsync(client, "/api/recipes");
        var firstCursor = RecipeDiscoveryCursorCodec.DecodeBrowse(first!.NextCursor!);
        var secondCursor = RecipeDiscoveryCursorCodec.DecodeBrowse(second!.NextCursor!);

        Assert.NotEqual(firstCursor.Seed, secondCursor.Seed);
        Assert.NotEqual(
            first.Items.Select(item => item.Id),
            second.Items.Select(item => item.Id));
    }

    [Fact]
    public async Task Discovery_AcceptsBoundedLimitsAndRejectsLimitChangesWithinCursorChain()
    {
        await SeedCompatibleRecipesAsync(CompatibleRecipeCount);
        var (authClient, _) = await CreateAuthenticatedClientAsync(
            dietIds: new[] { 2, 3 },
            ingredientRestrictionIds: Array.Empty<int>());
        using var client = authClient;

        var one = await GetPageAsync(client, "/api/recipes?limit=1");
        var fifty = await GetPageAsync(client, "/api/recipes?limit=50");

        Assert.Single(one!.Items);
        Assert.True(one.HasMore);
        Assert.NotNull(one.NextCursor);
        Assert.Equal(50, fifty!.Items.Count);
        Assert.True(fifty.HasMore);

        using var zeroResponse = await client.GetAsync("/api/recipes?limit=0");
        using var tooLargeResponse = await client.GetAsync("/api/recipes?limit=51");
        using var changedLimitResponse = await client.GetAsync(
            $"/api/recipes?limit=2&cursor={Uri.EscapeDataString(one.NextCursor)}");

        Assert.Equal(HttpStatusCode.BadRequest, zeroResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, tooLargeResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, changedLimitResponse.StatusCode);
    }

    [Fact]
    public async Task Discovery_RejectsMalformedUnsupportedAndOversizedCursors()
    {
        var (authClient, _) = await CreateAuthenticatedClientAsync(
            dietIds: new[] { 2, 3 },
            ingredientRestrictionIds: Array.Empty<int>());
        using var client = authClient;
        var cursors = new[]
        {
            "not*a*cursor",
            EncodeRaw($"{{\"v\":2,\"mode\":\"browse\",\"seed\":1,\"rank\":0,\"id\":1,\"criteria\":\"{ValidCriteria}\"}}"),
            EncodeRaw($"{{\"v\":1,\"mode\":\"other\",\"seed\":1,\"rank\":0,\"id\":1,\"criteria\":\"{ValidCriteria}\"}}"),
            new string('A', RecipeDiscoveryCursorCodec.MaxEncodedLength + 1),
        };

        foreach (var cursor in cursors)
        {
            using var response = await client.GetAsync(
                $"/api/recipes?cursor={Uri.EscapeDataString(cursor)}");

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }

    [Fact]
    public async Task Discovery_RejectsCursorFromAnotherAuthenticatedUser()
    {
        await SeedCompatibleRecipesAsync(CompatibleRecipeCount);
        var (userAClient, _) = await CreateAuthenticatedClientAsync(
            dietIds: new[] { 2, 3 },
            ingredientRestrictionIds: Array.Empty<int>());
        var (userBClient, _) = await CreateAuthenticatedClientAsync(
            dietIds: new[] { 2, 3 },
            ingredientRestrictionIds: Array.Empty<int>());
        using var clientA = userAClient;
        using var clientB = userBClient;

        var firstPage = await GetPageAsync(
            clientA,
            "/api/recipes?limit=1");
        using var response = await clientB.GetAsync(
            $"/api/recipes?limit=1&cursor={Uri.EscapeDataString(firstPage!.NextCursor!)}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task OpenApi_PublishesOpaqueCursorAndLimitOnly()
    {
        using var response = await Client.GetAsync("/swagger/v1/swagger.json");
        response.EnsureSuccessStatusCode();
        using var document = System.Text.Json.JsonDocument.Parse(
            await response.Content.ReadAsStreamAsync());
        var parameters = document.RootElement
            .GetProperty("paths")
            .GetProperty("/api/recipes")
            .GetProperty("get")
            .GetProperty("parameters")
            .EnumerateArray()
            .Select(parameter => parameter.GetProperty("name").GetString())
            .ToArray();

        Assert.Contains("cursor", parameters);
        Assert.Contains("limit", parameters);
        Assert.DoesNotContain("seed", parameters);
        Assert.DoesNotContain("rank", parameters);
        Assert.DoesNotContain("id", parameters);
        Assert.DoesNotContain("userId", parameters);
    }

    private async Task SeedCompatibleRecipesAsync(int totalRecipeCount)
    {
        var recipes = Enumerable.Range(2, totalRecipeCount - 1)
            .Select(id => new
            {
                Id = id,
                Name = $"Pagination Recipe {id:D3}",
                Description = $"Recipe {id} used for pagination verification.",
                ImageBlobName = $"recipes/pagination-{id:D3}.avif",
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
                @Description,
                @ImageBlobName,
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
            ) VALUES (@Id, 1, 100.000, 'g', 1, 1, NULL);
            """,
            recipes);
    }

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

    private static string EncodeRaw(string json) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(json))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}

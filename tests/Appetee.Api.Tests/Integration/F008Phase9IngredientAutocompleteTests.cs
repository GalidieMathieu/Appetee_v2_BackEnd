// Purpose: Verifies F-008 Phase 9 ingredient autocomplete behavior, compatibility, API contract, and MySQL plan.
// Change reason: Add end-to-end coverage for bounded lightweight ingredient-name search at expected catalogue scale.
// Created: 2026-08-27T13:16:15-06:00
// Last updated: 2026-08-27T13:16:15-06:00

using Appetee.Api.Tests.Infrastructure;
using Appetee.Application.Dtos;
using Appetee.Infrastructure.Ingredients;
using Dapper;
using MySqlConnector;
using System.Diagnostics;
using System.Net;
using System.Text.Json;

namespace Appetee.Api.Tests.Integration;

/// <summary>Exercises anonymous catalogue compatibility and bounded autocomplete through the real API and MySQL.</summary>
public sealed class F008Phase9IngredientAutocompleteTests : IntegrationTestBase
{
    public F008Phase9IngredientAutocompleteTests(AppeteeWebApplicationFactory factory)
        : base(factory) { }

    [Fact]
    public async Task Autocomplete_IsTrimmedCaseInsensitiveLiteralAndBounded()
    {
        await SeedAutocompleteIngredientsAsync();

        var defaultResults = await GetIngredientsAsync(
            "/api/ingredients?search=chicken");
        var limitedResults = await GetIngredientsAsync(
            "/api/ingredients?search=chicken&limit=3");
        var exactResult = await GetIngredientsAsync(
            "/api/ingredients?search=%20CHICKEN%20BREAST%20");
        var literalPercent = await GetIngredientsAsync(
            "/api/ingredients?search=100%25");
        var literalUnderscore = await GetIngredientsAsync(
            "/api/ingredients?search=under_score");

        Assert.Equal(10, defaultResults.Count);
        Assert.Equal(3, limitedResults.Count);
        Assert.Equal(1, exactResult[0].id);
        Assert.Equal("Percent 100% Pure", Assert.Single(literalPercent).name);
        Assert.Equal("Under_score Ingredient", Assert.Single(literalUnderscore).name);
    }

    [Fact]
    public async Task NoSearchPreservesCatalogueAndOpenApiPublishesLightweightIngredientDto()
    {
        using var response = await Client.GetAsync("/api/ingredients");
        response.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(
            await response.Content.ReadAsStreamAsync());
        var items = payload.RootElement.EnumerateArray().ToArray();

        Assert.Equal(4, items.Length);
        Assert.All(
            items,
            item => Assert.Equal(
                ["id", "name"],
                item.EnumerateObject().Select(property => property.Name).ToArray()));

        using var openApiResponse = await Client.GetAsync("/swagger/v1/swagger.json");
        openApiResponse.EnsureSuccessStatusCode();
        using var openApi = JsonDocument.Parse(
            await openApiResponse.Content.ReadAsStreamAsync());
        var operation = openApi.RootElement
            .GetProperty("paths")
            .GetProperty("/api/ingredients")
            .GetProperty("get");
        var parameterNames = operation
            .GetProperty("parameters")
            .EnumerateArray()
            .Select(parameter => parameter.GetProperty("name").GetString())
            .ToArray();
        var responseItemReference = operation
            .GetProperty("responses")
            .GetProperty("200")
            .GetProperty("content")
            .GetProperty("application/json")
            .GetProperty("schema")
            .GetProperty("items")
            .GetProperty("$ref")
            .GetString();

        Assert.Contains("search", parameterNames);
        Assert.Contains("limit", parameterNames);
        Assert.EndsWith("/IngredientDto", responseItemReference, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Autocomplete_InvalidSearchOrLimitReturnsBadRequest()
    {
        var invalidUrls = new[]
        {
            "/api/ingredients?search=a",
            "/api/ingredients?search=%20%20",
            $"/api/ingredients?search={new string('a', 101)}",
            "/api/ingredients?search=valid&limit=0",
            "/api/ingredients?search=valid&limit=51",
            "/api/ingredients?limit=10",
        };

        foreach (var url in invalidUrls)
        {
            using var response = await Client.GetAsync(url);
            Assert.True(
                response.StatusCode == HttpStatusCode.BadRequest,
                $"Expected 400 for '{url}' but received {(int)response.StatusCode} {response.StatusCode}.");
        }
    }

    [Fact]
    public async Task Autocomplete_QueryRemainsBoundedAtFiveThousandIngredientsAndHasAReviewedPlan()
    {
        await SeedFiveThousandIngredientsAsync();
        var normalizedSearch = "Phase Nine Ingredient";
        var escapedSearch = IngredientQueries.EscapeLikePattern(normalizedSearch);
        var parameters = new
        {
            SearchExact = normalizedSearch,
            SearchStarts = $"{escapedSearch}%",
            SearchContains = $"%{escapedSearch}%",
            Take = 10,
        };

        await using var connection = new MySqlConnection(Factory.Database.ConnectionString);
        await connection.OpenAsync();
        var planJson = await connection.ExecuteScalarAsync<string>(
            new CommandDefinition(
                $"EXPLAIN FORMAT=JSON {IngredientSql.SearchByName}",
                parameters));
        var stopwatch = Stopwatch.StartNew();
        var results = (await connection.QueryAsync<IngredientDto>(
            new CommandDefinition(IngredientSql.SearchByName, parameters)))
            .AsList();
        stopwatch.Stop();

        Assert.Equal(10, results.Count);
        Assert.Contains("ingredients", planJson, StringComparison.OrdinalIgnoreCase);
        Assert.True(
            stopwatch.Elapsed < TimeSpan.FromSeconds(5),
            $"Expected the bounded 5,000-row autocomplete query under 5 seconds; observed {stopwatch.Elapsed}.");
    }

    /// <summary>Adds enough matching and metacharacter-bearing names to prove bounds and literal LIKE behavior.</summary>
    private async Task SeedAutocompleteIngredientsAsync()
    {
        await Factory.Database.ExecuteAsync(
            """
            INSERT INTO ingredients (name) VALUES
                ('Chicken Autocomplete 01'),
                ('Chicken Autocomplete 02'),
                ('Chicken Autocomplete 03'),
                ('Chicken Autocomplete 04'),
                ('Chicken Autocomplete 05'),
                ('Chicken Autocomplete 06'),
                ('Chicken Autocomplete 07'),
                ('Chicken Autocomplete 08'),
                ('Chicken Autocomplete 09'),
                ('Chicken Autocomplete 10'),
                ('Chicken Autocomplete 11'),
                ('Chicken Autocomplete 12'),
                ('Percent 100% Pure'),
                ('Under_score Ingredient');
            """);
    }

    /// <summary>Builds the approved approximate production-scale catalogue without changing shared reset data.</summary>
    private async Task SeedFiveThousandIngredientsAsync()
    {
        await Factory.Database.ExecuteAsync(
            """
            INSERT INTO ingredients (name)
            SELECT CONCAT('Phase Nine Ingredient ', LPAD(numbers.n, 4, '0'))
            FROM (
                SELECT
                    ones.n
                    + (tens.n * 10)
                    + (hundreds.n * 100)
                    + (thousands.n * 1000) AS n
                FROM
                    (SELECT 0 AS n UNION ALL SELECT 1 UNION ALL SELECT 2 UNION ALL SELECT 3 UNION ALL SELECT 4
                     UNION ALL SELECT 5 UNION ALL SELECT 6 UNION ALL SELECT 7 UNION ALL SELECT 8 UNION ALL SELECT 9) ones
                CROSS JOIN
                    (SELECT 0 AS n UNION ALL SELECT 1 UNION ALL SELECT 2 UNION ALL SELECT 3 UNION ALL SELECT 4
                     UNION ALL SELECT 5 UNION ALL SELECT 6 UNION ALL SELECT 7 UNION ALL SELECT 8 UNION ALL SELECT 9) tens
                CROSS JOIN
                    (SELECT 0 AS n UNION ALL SELECT 1 UNION ALL SELECT 2 UNION ALL SELECT 3 UNION ALL SELECT 4
                     UNION ALL SELECT 5 UNION ALL SELECT 6 UNION ALL SELECT 7 UNION ALL SELECT 8 UNION ALL SELECT 9) hundreds
                CROSS JOIN
                    (SELECT 0 AS n UNION ALL SELECT 1 UNION ALL SELECT 2 UNION ALL SELECT 3 UNION ALL SELECT 4
                     UNION ALL SELECT 5 UNION ALL SELECT 6 UNION ALL SELECT 7 UNION ALL SELECT 8 UNION ALL SELECT 9) thousands
            ) numbers
            WHERE numbers.n < 5000;
            """);
    }

    /// <summary>Reads one successful lightweight ingredient response for endpoint behavior assertions.</summary>
    private async Task<IReadOnlyList<IngredientDto>> GetIngredientsAsync(string url)
    {
        using var response = await Client.GetAsync(url);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);

        return JsonSerializer.Deserialize<IReadOnlyList<IngredientDto>>(
                body,
                new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException("Ingredient response was empty.");
    }
}

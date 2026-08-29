// Purpose: Verifies F-008 Phase 9 ingredient autocomplete validation, routing, SQL safety, and DTO boundaries.
// Change reason: Verify the restored GetAll compatibility path remains separate from bounded autocomplete.
// Created: 2026-08-27T13:16:15-06:00
// Last updated: 2026-08-27T13:46:36-06:00

using Appetee.Application.Abstractions.Ingredients;
using Appetee.Application.Dtos;
using Appetee.Application.Requests;
using Appetee.Application.Services.Ingredients;
using Appetee.Application.utils;
using Appetee.Infrastructure.Ingredients;
using Microsoft.Extensions.Logging.Abstractions;

namespace Appetee.Api.Tests.Unit;

/// <summary>Exercises autocomplete behavior without requiring an HTTP host or database.</summary>
public sealed class F008Phase9IngredientAutocompleteTests
{
    [Fact]
    public async Task IngredientService_NoSearchPreservesTheExistingFullCataloguePath()
    {
        var queries = new CapturingIngredientQueries
        {
            AllResults = [new IngredientDto(1, "Chicken Breast")],
        };
        var service = CreateService(queries);

        var results = await service.GetAll(CancellationToken.None);

        Assert.Single(results);
        Assert.Equal(1, queries.AllCalls);
        Assert.Empty(queries.SearchCalls);
    }

    [Fact]
    public async Task IngredientService_TrimsSearchAndAppliesTheDefaultBound()
    {
        var queries = new CapturingIngredientQueries();
        var service = CreateService(queries);

        await service.SearchAsync("  chick  ", null, CancellationToken.None);

        var call = Assert.Single(queries.SearchCalls);
        Assert.Equal("chick", call.Search);
        Assert.Equal(IngredientService.AutocompleteDefaultLimit, call.Limit);
        Assert.Equal(0, queries.AllCalls);
    }

    [Theory]
    [InlineData(null, 10)]
    [InlineData("a", null)]
    [InlineData("valid", 0)]
    [InlineData("valid", 51)]
    public async Task IngredientService_RejectsInvalidSearchAndLimitCombinations(
        string? search,
        int? limit)
    {
        var queries = new CapturingIngredientQueries();
        var service = CreateService(queries);

        await Assert.ThrowsAsync<ValidationException>(
            () => service.SearchAsync(search, limit, CancellationToken.None));

        Assert.Equal(0, queries.AllCalls);
        Assert.Empty(queries.SearchCalls);
    }

    [Fact]
    public async Task IngredientService_RejectsOversizedSearchBeforePersistence()
    {
        var queries = new CapturingIngredientQueries();
        var service = CreateService(queries);

        await Assert.ThrowsAsync<ValidationException>(
            () => service.SearchAsync(
                new string('a', IngredientService.AutocompleteMaximumSearchLength + 1),
                null,
                CancellationToken.None));

        Assert.Equal(0, queries.AllCalls);
        Assert.Empty(queries.SearchCalls);
    }

    [Fact]
    public void IngredientAutocompleteSql_IsParameterizedBoundedAndLiteralSafe()
    {
        Assert.Contains("name LIKE @SearchContains", IngredientSql.SearchByName, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("LIMIT @Take", IngredientSql.SearchByName, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("name = @SearchExact", IngredientSql.SearchByName, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SELECT *", IngredientSql.SearchByName, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(@"50\% \_ \\ value", IngredientQueries.EscapeLikePattern(@"50% _ \ value"));
    }

    [Fact]
    public void IngredientAutocompleteDto_ContainsOnlyIdentityFields()
    {
        var properties = typeof(IngredientDto).GetProperties();

        Assert.Equal(2, properties.Length);
        Assert.Equal(["id", "name"], properties.Select(property => property.Name));
    }

    private static IngredientService CreateService(IIngredientQueries queries) =>
        new(queries, NullLogger<IngredientService>.Instance);

    /// <summary>Captures catalogue/search routing while stubbing administrative ingredient persistence.</summary>
    private sealed class CapturingIngredientQueries : IIngredientQueries
    {
        internal int AllCalls { get; private set; }
        internal List<(string Search, int Limit)> SearchCalls { get; } = [];
        internal IReadOnlyList<IngredientDto> AllResults { get; init; } = [];

        public Task<IReadOnlyList<IngredientDto>> GetAllAsync(CancellationToken ct)
        {
            AllCalls++;
            return Task.FromResult(AllResults);
        }

        public Task<IReadOnlyList<IngredientDto>> SearchByNameAsync(
            string normalizedSearch,
            int limit,
            CancellationToken ct)
        {
            SearchCalls.Add((normalizedSearch, limit));
            return Task.FromResult<IReadOnlyList<IngredientDto>>([]);
        }

        public Task<IngredientAdminDetailDto?> GetIngredientWithDetailsByIdAsync(
            int id,
            CancellationToken ct) =>
            Task.FromResult<IngredientAdminDetailDto?>(null);

        public Task<IngredientAdminDetailDto?> CreateIngredientWithDetailsAsync(
            IngredientAdminDetailRequest request,
            CancellationToken ct) =>
            Task.FromResult<IngredientAdminDetailDto?>(null);
    }
}

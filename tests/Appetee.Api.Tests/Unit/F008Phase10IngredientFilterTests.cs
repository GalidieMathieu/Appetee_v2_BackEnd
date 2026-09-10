// Purpose: Verifies F-008 Phase 10 ingredient filter validation, cursor binding, and ALL/ANY SQL composition.
// Change reason: Supply a neutral F-009 Favorites stub for the expanded recipe query contract.
// Created: 2026-08-28T08:53:55-06:00
// Last updated: 2026-08-29T14:05:58-06:00

using Appetee.Application.Abstractions.Recipes;
using Appetee.Application.Dtos;
using Appetee.Application.Models.Recipes;
using Appetee.Application.Requests;
using Appetee.Application.Services.Recipes;
using Appetee.Application.utils;
using Appetee.Infrastructure.Recipes;

namespace Appetee.Api.Tests.Unit;

/// <summary>Exercises Phase 10 ingredient criteria without requiring an HTTP host or database.</summary>
public sealed class F008Phase10IngredientFilterTests
{
    [Fact]
    public async Task RecipeService_NormalizesIngredientIdsAndDefaultsToRequireAll()
    {
        var queries = new CapturingRecipeQueries();
        var service = new RecipeService(queries);

        await service.DiscoverAsync(
            42,
            new RecipeDiscoveryRequest { IngredientIds = [3, 1, 3] },
            CancellationToken.None);

        var query = Assert.Single(queries.DiscoveryQueries);
        Assert.Equal([1, 3], query.IngredientIds);
        Assert.True(query.RequireAllIngredients);
    }

    [Fact]
    public async Task RecipeService_PreservesAnyModeOnlyWhenIngredientsAreSelected()
    {
        var queries = new CapturingRecipeQueries();
        var service = new RecipeService(queries);

        await service.DiscoverAsync(
            42,
            new RecipeDiscoveryRequest
            {
                IngredientIds = [1, 2],
                RequireAllIngredients = false,
            },
            CancellationToken.None);
        await service.DiscoverAsync(
            42,
            new RecipeDiscoveryRequest { RequireAllIngredients = false },
            CancellationToken.None);

        Assert.False(queries.DiscoveryQueries[0].RequireAllIngredients);
        Assert.True(queries.DiscoveryQueries[1].RequireAllIngredients);
    }

    [Fact]
    public async Task RecipeService_RejectsInvalidIngredientSelectionsBeforePersistence()
    {
        var queries = new CapturingRecipeQueries();
        var service = new RecipeService(queries);
        var invalidRequests = new RecipeDiscoveryRequest[]
        {
            new() { IngredientIds = [0] },
            new() { IngredientIds = [-1] },
            new() { IngredientIds = [1, 2, 3, 4] },
            new() { IngredientIds = [1, 1, 1, 1] },
        };

        foreach (var request in invalidRequests)
        {
            await Assert.ThrowsAsync<ValidationException>(
                () => service.DiscoverAsync(42, request, CancellationToken.None));
        }

        Assert.Empty(queries.DiscoveryQueries);
    }

    [Fact]
    public async Task RecipeService_BindsIngredientIdsAndModeToCursorCriteria()
    {
        var queries = new CapturingRecipeQueries
        {
            Slice = new RecipeDiscoverySlice(
                [Card(7)],
                HasMore: true,
                new RecipeDiscoveryContinuation(1234, 7)),
        };
        var service = new RecipeService(queries);
        var initial = new RecipeDiscoveryRequest
        {
            IngredientIds = [2, 1],
            RequireAllIngredients = true,
            Limit = 1,
        };
        var page = await service.DiscoverAsync(42, initial, CancellationToken.None);

        await Assert.ThrowsAsync<ValidationException>(
            () => service.DiscoverAsync(
                42,
                initial with { IngredientIds = [1, 3], Cursor = page.NextCursor },
                CancellationToken.None));
        await Assert.ThrowsAsync<ValidationException>(
            () => service.DiscoverAsync(
                42,
                initial with { RequireAllIngredients = false, Cursor = page.NextCursor },
                CancellationToken.None));

        Assert.Single(queries.DiscoveryQueries);
    }

    [Fact]
    public void DiscoverySql_UsesCountDistinctForAllAndExistsForAny()
    {
        var (allSql, allParameters) = RecipeDiscoverySqlBuilder.Build(
            Query([1, 2, 3], requireAllIngredients: true));
        var (anySql, anyParameters) = RecipeDiscoverySqlBuilder.Build(
            Query([1, 2, 3], requireAllIngredients: false));

        Assert.Contains("COUNT(DISTINCT ri_filter.ingredient_id)", allSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(") = @IngredientCount", allSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("COUNT(DISTINCT ri_filter.ingredient_id)", anySql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("AND EXISTS", anySql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ri_filter.ingredient_id IN @IngredientIds", anySql, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(3, allParameters.Get<int>("IngredientCount"));
        Assert.Equal([1, 2, 3], allParameters.Get<int[]>("IngredientIds"));
        Assert.Equal([1, 2, 3], anyParameters.Get<int[]>("IngredientIds"));
    }

    [Fact]
    public void DiscoverySql_KeepsCompatibilityAndComposesIngredientFilterWithSearch()
    {
        var noFilterSql = RecipeDiscoverySqlBuilder.Build(Query()).Sql;
        var filteredSearchSql = RecipeDiscoverySqlBuilder.Build(
            Query(
                [1],
                requireAllIngredients: false,
                normalizedSearch: "chicken",
                terms: ["chicken"]))
            .Sql;

        Assert.DoesNotContain("ri_filter", noFilterSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("uir.user_id = @CurrentUserId", filteredSearchSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ri_restriction.recipe_id = r.id", filteredSearchSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ri_filter.ingredient_id IN @IngredientIds", filteredSearchSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("r.name LIKE @Term0Contains", filteredSearchSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ORDER BY ranked.SortRank DESC, ranked.Id DESC", filteredSearchSql, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Builds discovery persistence state with optional selected ingredients and search mode.</summary>
    private static RecipeDiscoveryQuery Query(
        IReadOnlyList<int>? ingredientIds = null,
        bool requireAllIngredients = true,
        string? normalizedSearch = null,
        IReadOnlyList<string>? terms = null) =>
        new RecipeDiscoveryQuery(
            CurrentUserId: 42,
            PageSize: 20,
            NormalizedSearch: normalizedSearch,
            EffectiveSearchTerms: terms ?? [],
            CanonicalBadges: [],
            MaxTotalMinutes: null,
            AllowedDifficulties: [],
            SavedOnly: false,
            BrowseSeed: normalizedSearch is null ? 1234 : null,
            AfterRank: null,
            AfterRecipeId: null)
        {
            IngredientIds = ingredientIds ?? [],
            RequireAllIngredients = requireAllIngredients,
        };

    private static RecipeCardDto Card(int id) =>
        new(
            id,
            $"Recipe {id}",
            CardImageUrl: null,
            TotalTimeMinutes: 30,
            CaloriesPerServing: 400,
            EstimatedCostPerServing: 4,
            Badges: [],
            FeaturedIngredients: [],
            IsSaved: false);

    /// <summary>Captures discovery state while stubbing recipe persistence unrelated to Phase 10.</summary>
    private sealed class CapturingRecipeQueries : IRecipeQueries
    {
        internal List<RecipeDiscoveryQuery> DiscoveryQueries { get; } = [];

        internal RecipeDiscoverySlice Slice { get; set; } =
            new([], HasMore: false, Continuation: null);

        public Task<RecipeDiscoverySlice> DiscoverAsync(
            RecipeDiscoveryQuery query,
            CancellationToken ct)
        {
            DiscoveryQueries.Add(query);
            return Task.FromResult(Slice);
        }

        public Task<RecipePreviewDto?> GetPreviewAsync(
            int currentUserId,
            int recipeId,
            CancellationToken ct) =>
            Task.FromResult<RecipePreviewDto?>(null);

        public Task<IReadOnlyList<RecipeCardDto>> GetFavoritesAsync(
            int currentUserId,
            int? limit,
            CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<RecipeCardDto>>([]);

        public Task<bool> SaveFavoriteAsync(
            int currentUserId,
            int recipeId,
            CancellationToken ct) =>
            Task.FromResult(false);

        public Task RemoveFavoriteAsync(
            int currentUserId,
            int recipeId,
            CancellationToken ct) =>
            Task.CompletedTask;

        public Task<RecipeSummaryDto?> CreateRecipeWithDetailsAsync(
            RecipeDetailRequest request,
            CancellationToken ct) =>
            Task.FromResult<RecipeSummaryDto?>(null);

        public Task<RecipeSummaryDto?> UpdateRecipeWithDetailsAsync(
            int id,
            RecipeDetailRequest request,
            CancellationToken ct) =>
            Task.FromResult<RecipeSummaryDto?>(null);

        public Task<RecipeDetailDto?> GetRecipeWithDetailsByIdAsync(
            int id,
            CancellationToken ct) =>
            Task.FromResult<RecipeDetailDto?>(null);
    }
}

// Purpose: Verifies F-008 Phase 8 filter validation, normalization, SQL composition, and cursor binding.
// Change reason: Supply a neutral F-010 Cooking View stub for the expanded recipe query contract.
// Created: 2026-08-27T10:48:05-06:00
// Last updated: 2026-08-31T18:01:27-06:00

using Appetee.Application.Abstractions.Recipes;
using Appetee.Application.Dtos;
using Appetee.Application.Models.Recipes;
using Appetee.Application.Requests;
using Appetee.Application.Services.Recipes;
using Appetee.Application.utils;
using Appetee.Infrastructure.Recipes;

namespace Appetee.Api.Tests.Unit;

/// <summary>Exercises Phase 8 discovery filters without requiring an HTTP host or database.</summary>
public sealed class F008Phase8FilterTests
{
    [Fact]
    public async Task RecipeService_CanonicalizesFiltersAndKeepsFilterOnlyRequestsInBrowseMode()
    {
        var queries = new CapturingRecipeQueries();
        var service = new RecipeService(queries);

        await service.DiscoverAsync(
            42,
            new RecipeDiscoveryRequest
            {
                Badges =
                [
                    RecipeBadgeValues.BudgetFriendly,
                    RecipeBadgeValues.HighProtein,
                    RecipeBadgeValues.HighProtein,
                ],
                MaxTotalMinutes = 45,
                MaxDifficulty = RecipeDifficulty.Medium,
            },
            CancellationToken.None);

        var query = Assert.Single(queries.DiscoveryQueries);
        Assert.Equal(
            [RecipeBadgeValues.HighProtein, RecipeBadgeValues.BudgetFriendly],
            query.CanonicalBadges);
        Assert.Equal(45, query.MaxTotalMinutes);
        Assert.Equal(["Easy", "Medium"], query.AllowedDifficulties);
        Assert.False(query.IsSearch);
        Assert.NotNull(query.BrowseSeed);
    }

    [Fact]
    public async Task RecipeService_SearchAndFiltersUseRankedSearchWithoutBrowseSeed()
    {
        var queries = new CapturingRecipeQueries();
        var service = new RecipeService(queries);

        await service.DiscoverAsync(
            42,
            new RecipeDiscoveryRequest
            {
                Search = " chicken ",
                Badges = [RecipeBadgeValues.HighProtein],
                MaxTotalMinutes = 30,
                MaxDifficulty = RecipeDifficulty.Easy,
            },
            CancellationToken.None);

        var query = Assert.Single(queries.DiscoveryQueries);
        Assert.True(query.IsSearch);
        Assert.Null(query.BrowseSeed);
        Assert.Equal(["Easy"], query.AllowedDifficulties);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(RecipeDifficulty.Hard)]
    public async Task RecipeService_HardOrUnsetDifficultyAddsNoUnnecessaryRestriction(
        RecipeDifficulty? maxDifficulty)
    {
        var queries = new CapturingRecipeQueries();
        var service = new RecipeService(queries);

        await service.DiscoverAsync(
            42,
            new RecipeDiscoveryRequest { MaxDifficulty = maxDifficulty },
            CancellationToken.None);

        Assert.Empty(Assert.Single(queries.DiscoveryQueries).AllowedDifficulties);
    }

    [Fact]
    public async Task RecipeService_RejectsInvalidFilterValuesBeforePersistence()
    {
        var queries = new CapturingRecipeQueries();
        var service = new RecipeService(queries);
        var invalidRequests = new RecipeDiscoveryRequest[]
        {
            new() { Badges = ["high protein"] },
            new() { Badges = [string.Empty] },
            new() { MaxTotalMinutes = 0 },
            new() { MaxTotalMinutes = 1441 },
            new() { MaxDifficulty = (RecipeDifficulty)999 },
        };

        foreach (var request in invalidRequests)
        {
            await Assert.ThrowsAsync<ValidationException>(
                () => service.DiscoverAsync(42, request, CancellationToken.None));
        }

        Assert.Empty(queries.DiscoveryQueries);
    }

    [Fact]
    public async Task RecipeService_BindsEveryFilterToCursorCriteria()
    {
        var queries = new CapturingRecipeQueries
        {
            Slice = new RecipeDiscoverySlice(
                [Card(7)],
                HasMore: true,
                new RecipeDiscoveryContinuation(1234, 7)),
        };
        var service = new RecipeService(queries);
        var initialRequest = new RecipeDiscoveryRequest
        {
            Badges = [RecipeBadgeValues.HighProtein],
            MaxTotalMinutes = 30,
            MaxDifficulty = RecipeDifficulty.Medium,
            Limit = 1,
        };
        var firstPage = await service.DiscoverAsync(
            42,
            initialRequest,
            CancellationToken.None);

        var changedRequests = new RecipeDiscoveryRequest[]
        {
            initialRequest with { Badges = [RecipeBadgeValues.HighFiber], Cursor = firstPage.NextCursor },
            initialRequest with { MaxTotalMinutes = 31, Cursor = firstPage.NextCursor },
            initialRequest with { MaxDifficulty = RecipeDifficulty.Easy, Cursor = firstPage.NextCursor },
        };

        foreach (var request in changedRequests)
        {
            await Assert.ThrowsAsync<ValidationException>(
                () => service.DiscoverAsync(42, request, CancellationToken.None));
        }

        Assert.Single(queries.DiscoveryQueries);
    }

    [Fact]
    public void DiscoverySql_ComposesAllPhase8PredicatesAndParameters()
    {
        var (sql, parameters) = RecipeDiscoverySqlBuilder.Build(
            Query(
                badges: [RecipeBadgeValues.HighProtein, RecipeBadgeValues.BudgetFriendly],
                maxTotalMinutes: 45,
                allowedDifficulties: ["Easy", "Medium"]));

        Assert.Contains("COUNT(DISTINCT rb_filter.badge)", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("rb_filter.badge IN @Badges", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(") = @BadgeCount", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("r.total_time_minutes <= @MaxTotalMinutes", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("r.difficulty IN @AllowedDifficulties", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, parameters.Get<int>("BadgeCount"));
        Assert.Equal(45, parameters.Get<int>("MaxTotalMinutes"));
        Assert.Equal(["Easy", "Medium"], parameters.Get<string[]>("AllowedDifficulties"));
    }

    [Fact]
    public void DiscoverySql_OmitsUnusedPhase8PredicatesAndPreservesDiscoveryOrdering()
    {
        var browseSql = RecipeDiscoverySqlBuilder.Build(Query()).Sql;
        var searchSql = RecipeDiscoverySqlBuilder.Build(
            Query(normalizedSearch: "chicken", terms: ["chicken"])).Sql;

        Assert.DoesNotContain("rb_filter", browseSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("@MaxTotalMinutes", browseSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("@AllowedDifficulties", browseSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ORDER BY ranked.SortRank ASC, ranked.Id ASC", browseSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ORDER BY ranked.SortRank DESC, ranked.Id DESC", searchSql, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Builds a validated persistence query with only the filter state relevant to each SQL assertion.</summary>
    private static RecipeDiscoveryQuery Query(
        IReadOnlyList<string>? badges = null,
        int? maxTotalMinutes = null,
        IReadOnlyList<string>? allowedDifficulties = null,
        string? normalizedSearch = null,
        IReadOnlyList<string>? terms = null) =>
        new(
            CurrentUserId: 42,
            PageSize: 20,
            NormalizedSearch: normalizedSearch,
            EffectiveSearchTerms: terms ?? [],
            CanonicalBadges: badges ?? [],
            MaxTotalMinutes: maxTotalMinutes,
            AllowedDifficulties: allowedDifficulties ?? [],
            SavedOnly: false,
            BrowseSeed: normalizedSearch is null ? 1234 : null,
            AfterRank: null,
            AfterRecipeId: null);

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

    /// <summary>Captures discovery queries while stubbing persistence operations unrelated to Phase 8 filters.</summary>
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

        public Task<RecipeCookingViewDto?> GetCookingViewAsync(
            int currentUserId,
            int recipeId,
            CancellationToken ct) =>
            Task.FromResult<RecipeCookingViewDto?>(null);

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

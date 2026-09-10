// Purpose: Verifies F-009 Phase 1 Favorites validation, delegation, and fixed SQL query shapes.
// Change reason: Preserve Favorites coverage while supplying a neutral F-010 Cooking View query stub.
// Created: 2026-08-29T14:05:58-06:00
// Last updated: 2026-08-31T18:01:27-06:00

using Appetee.Application.Abstractions.Recipes;
using Appetee.Application.Dtos;
using Appetee.Application.Models.Recipes;
using Appetee.Application.Requests;
using Appetee.Application.Services.Recipes;
using Appetee.Application.utils;

namespace Appetee.Api.Tests.Unit;

/// <summary>Protects Favorites list bounds and current-user persistence composition.</summary>
public sealed class F009Phase1FavoritesTests
{
    [Theory]
    [InlineData(null)]
    [InlineData(1)]
    [InlineData(50)]
    public async Task GetFavorites_AcceptsSupportedLimitsAndDelegatesCurrentUser(
        int? limit)
    {
        var queries = new CapturingRecipeQueries
        {
            FavoritesResult = [Card(7)],
        };
        var service = new RecipeService(queries);

        var result = await service.GetFavoritesAsync(42, limit, CancellationToken.None);

        Assert.Equal(7, Assert.Single(result).Id);
        Assert.Equal((42, limit), Assert.Single(queries.FavoriteCalls));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(51)]
    public async Task GetFavorites_RejectsUnsupportedLimitsBeforePersistence(int limit)
    {
        var queries = new CapturingRecipeQueries();
        var service = new RecipeService(queries);

        await Assert.ThrowsAsync<ValidationException>(
            () => service.GetFavoritesAsync(42, limit, CancellationToken.None));

        Assert.Empty(queries.FavoriteCalls);
    }

    [Fact]
    public async Task GetFavorites_RejectsInvalidServerOwnedIdentity()
    {
        var queries = new CapturingRecipeQueries();
        var service = new RecipeService(queries);

        await Assert.ThrowsAsync<ValidationException>(
            () => service.GetFavoritesAsync(0, null, CancellationToken.None));

        Assert.Empty(queries.FavoriteCalls);
    }

    [Fact]
    public void FavoriteCandidateSql_IsOwnedCompatibleOrderedAndOptionallyBounded()
    {
        Assert.Contains("FROM favorite_recipes fr", RecipeSql.GetFavoriteCandidates, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("fr.user_id = @CurrentUserId", RecipeSql.GetFavoriteCandidates, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ud.user_id = @CurrentUserId", RecipeSql.GetFavoriteCandidates, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("uir.user_id = @CurrentUserId", RecipeSql.GetFavoriteCandidates, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ORDER BY fr.created_at DESC, r.id DESC", RecipeSql.GetFavoriteCandidates, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("LIMIT", RecipeSql.GetFavoriteCandidates, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("LIMIT @Limit", RecipeSql.GetFavoriteCandidatesWithLimit, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CAST(1 AS SIGNED)", RecipeSql.GetFavoriteCandidates, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FavoriteHydration_ReusesCanonicalSetBasedCardCommand()
    {
        Assert.Equal(2, RecipeSql.HydrateDiscoveryCards.Split(';').Count(part => !string.IsNullOrWhiteSpace(part)));
        Assert.Contains("recipe_id IN @RecipeIds", RecipeSql.HydrateDiscoveryCards, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("featured_order IS NOT NULL", RecipeSql.HydrateDiscoveryCards, StringComparison.OrdinalIgnoreCase);
    }

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
            IsSaved: true);

    /// <summary>Captures Favorites calls while neutrally implementing unrelated recipe persistence operations.</summary>
    private sealed class CapturingRecipeQueries : IRecipeQueries
    {
        internal List<(int CurrentUserId, int? Limit)> FavoriteCalls { get; } = [];
        internal IReadOnlyList<RecipeCardDto> FavoritesResult { get; init; } = [];

        public Task<IReadOnlyList<RecipeCardDto>> GetFavoritesAsync(
            int currentUserId,
            int? limit,
            CancellationToken ct)
        {
            FavoriteCalls.Add((currentUserId, limit));
            return Task.FromResult(FavoritesResult);
        }

        public Task<RecipeDiscoverySlice> DiscoverAsync(
            RecipeDiscoveryQuery query,
            CancellationToken ct) =>
            Task.FromResult(new RecipeDiscoverySlice([], false, null));

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

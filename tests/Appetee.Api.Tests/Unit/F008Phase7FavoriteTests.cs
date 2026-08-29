// Purpose: Verifies F-008 Phase 7 favorite orchestration, saved-only SQL, and cursor binding.
// Change reason: Supply a neutral F-009 Favorites stub for the expanded recipe query contract.
// Created: 2026-08-26T17:29:19-06:00
// Last updated: 2026-08-29T14:05:58-06:00

using Appetee.Application.Abstractions.Recipes;
using Appetee.Application.Dtos;
using Appetee.Application.Models.Recipes;
using Appetee.Application.Requests;
using Appetee.Application.Services.Recipes;
using Appetee.Application.utils;
using Appetee.Infrastructure.Recipes;

namespace Appetee.Api.Tests.Unit;

/// <summary>Exercises current-user favorite delegation and saved-only discovery invariants.</summary>
public sealed class F008Phase7FavoriteTests
{
    [Fact]
    public async Task RecipeService_DelegatesValidatedFavoriteMutations()
    {
        var queries = new CapturingRecipeQueries { SaveResult = true };
        var service = new RecipeService(queries);

        var saved = await service.SaveFavoriteAsync(42, 7, CancellationToken.None);
        await service.RemoveFavoriteAsync(42, 7, CancellationToken.None);

        Assert.True(saved);
        Assert.Equal((42, 7), Assert.Single(queries.SaveCalls));
        Assert.Equal((42, 7), Assert.Single(queries.RemoveCalls));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    [InlineData(-1, 1)]
    [InlineData(1, -1)]
    public async Task RecipeService_RejectsInvalidFavoriteIdentifiers(
        int currentUserId,
        int recipeId)
    {
        var queries = new CapturingRecipeQueries();
        var service = new RecipeService(queries);

        await Assert.ThrowsAsync<ValidationException>(
            () => service.SaveFavoriteAsync(
                currentUserId,
                recipeId,
                CancellationToken.None));
        await Assert.ThrowsAsync<ValidationException>(
            () => service.RemoveFavoriteAsync(
                currentUserId,
                recipeId,
                CancellationToken.None));

        Assert.Empty(queries.SaveCalls);
        Assert.Empty(queries.RemoveCalls);
    }

    [Fact]
    public async Task SavedOnly_IsPassedToPersistenceAndBoundIntoCursorCriteria()
    {
        var queries = new CapturingRecipeQueries
        {
            Slice = new RecipeDiscoverySlice(
                [Card(7)],
                HasMore: true,
                new RecipeDiscoveryContinuation(1234, 7)),
        };
        var service = new RecipeService(queries);

        var page = await service.DiscoverAsync(
            42,
            new RecipeDiscoveryRequest { SavedOnly = true, Limit = 1 },
            CancellationToken.None);
        var query = Assert.Single(queries.DiscoveryQueries);

        Assert.True(query.SavedOnly);
        await Assert.ThrowsAsync<ValidationException>(
            () => service.DiscoverAsync(
                42,
                new RecipeDiscoveryRequest
                {
                    SavedOnly = false,
                    Limit = 1,
                    Cursor = page.NextCursor,
                },
                CancellationToken.None));
        Assert.Single(queries.DiscoveryQueries);
    }

    [Fact]
    public void DiscoverySql_AddsCurrentUserSavedPredicateOnlyWhenRequested()
    {
        var unsavedSql = RecipeDiscoverySqlBuilder.Build(BrowseQuery(savedOnly: false)).Sql;
        var savedSql = RecipeDiscoverySqlBuilder.Build(BrowseQuery(savedOnly: true)).Sql;

        Assert.DoesNotContain("fr_saved", unsavedSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("FROM favorite_recipes fr_saved", savedSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("fr_saved.user_id = @CurrentUserId", savedSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("fr_saved.recipe_id = r.id", savedSql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FavoriteSql_IsCompatibilityScopedOnPutAndCurrentUserScopedOnDelete()
    {
        Assert.Contains("r.id = @RecipeId", RecipeSql.IsCompatibleFavoriteCandidate, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ud.user_id = @CurrentUserId", RecipeSql.IsCompatibleFavoriteCandidate, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("uir.user_id = @CurrentUserId", RecipeSql.IsCompatibleFavoriteCandidate, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("INSERT INTO favorite_recipes", RecipeSql.EnsureFavorite, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ON DUPLICATE KEY UPDATE", RecipeSql.EnsureFavorite, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("user_id = @CurrentUserId", RecipeSql.RemoveFavorite, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("recipe_id = @RecipeId", RecipeSql.RemoveFavorite, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("user_diets", RecipeSql.RemoveFavorite, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("user_ingredient_restrictions", RecipeSql.RemoveFavorite, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Builds seeded browse persistence state while varying only favorite membership filtering.</summary>
    private static RecipeDiscoveryQuery BrowseQuery(bool savedOnly) =>
        new(
            CurrentUserId: 42,
            PageSize: 20,
            NormalizedSearch: null,
            EffectiveSearchTerms: [],
            CanonicalBadges: [],
            MaxTotalMinutes: null,
            AllowedDifficulties: [],
            SavedOnly: savedOnly,
            BrowseSeed: 1234,
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
            IsSaved: true);

    /// <summary>Captures favorite and discovery calls while stubbing unrelated recipe operations.</summary>
    private sealed class CapturingRecipeQueries : IRecipeQueries
    {
        internal List<(int CurrentUserId, int RecipeId)> SaveCalls { get; } = [];
        internal List<(int CurrentUserId, int RecipeId)> RemoveCalls { get; } = [];
        internal List<RecipeDiscoveryQuery> DiscoveryQueries { get; } = [];
        internal bool SaveResult { get; set; }
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
            CancellationToken ct)
        {
            SaveCalls.Add((currentUserId, recipeId));
            return Task.FromResult(SaveResult);
        }

        public Task RemoveFavoriteAsync(
            int currentUserId,
            int recipeId,
            CancellationToken ct)
        {
            RemoveCalls.Add((currentUserId, recipeId));
            return Task.CompletedTask;
        }

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

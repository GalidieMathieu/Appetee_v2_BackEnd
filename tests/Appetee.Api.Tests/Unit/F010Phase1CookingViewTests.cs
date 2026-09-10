// Purpose: Verifies F-010 Phase 1 Cooking View validation, delegation, and bounded SQL shape.
// Change reason: Cover the dedicated Cooking View and its compile-time shared read-model contract.
// Created: 2026-08-31T18:01:27-06:00
// Last updated: 2026-08-31T18:28:42-06:00

using Appetee.Application.Abstractions.Recipes;
using Appetee.Application.Dtos;
using Appetee.Application.Models.Recipes;
using Appetee.Application.Requests;
using Appetee.Application.Services.Recipes;
using Appetee.Application.utils;
using Appetee.Infrastructure.Recipes;

namespace Appetee.Api.Tests.Unit;

/// <summary>Exercises the application boundary and SQL contract for Recipe Cooking View.</summary>
public sealed class F010Phase1CookingViewTests
{
    [Fact]
    public void PreviewAndCookingView_ShareOnlyTheStableRecipeReadContract()
    {
        Assert.True(typeof(RecipeReadDto).IsAssignableFrom(typeof(RecipePreviewDto)));
        Assert.True(typeof(RecipeReadDto).IsAssignableFrom(typeof(RecipeCookingViewDto)));
        Assert.False(typeof(RecipeReadDto).IsAssignableFrom(typeof(RecipeDetailDto)));
    }

    [Fact]
    public async Task RecipeService_ValidatesAndDelegatesCookingViewIdentity()
    {
        var expected = CookingView(7);
        var queries = new CapturingRecipeQueries { CookingViewResult = expected };
        var service = new RecipeService(queries);

        var actual = await service.GetCookingViewAsync(42, 7, CancellationToken.None);

        Assert.Same(expected, actual);
        Assert.Equal((42, 7), Assert.Single(queries.CookingViewCalls));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(1, 0)]
    [InlineData(1, -1)]
    public async Task RecipeService_RejectsInvalidCookingViewIdentifiersBeforePersistence(
        int currentUserId,
        int recipeId)
    {
        var queries = new CapturingRecipeQueries();
        var service = new RecipeService(queries);

        await Assert.ThrowsAsync<ValidationException>(
            () => service.GetCookingViewAsync(currentUserId, recipeId, CancellationToken.None));

        Assert.Empty(queries.CookingViewCalls);
    }

    [Fact]
    public void CookingViewSql_UsesOneBoundedCommandAndCanonicalCompatibility()
    {
        var sql = RecipeSql.GetCompatibleCookingView;

        Assert.Equal(3, sql.Count(character => character == ';'));
        Assert.Contains("r.id = @RecipeId", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ud.user_id = @CurrentUserId", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("uir.user_id = @CurrentUserId", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("r.image_blob_name    AS ImageBlobName", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("r.servings           AS BaseServings", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("r.instructions       AS Instructions", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("FROM recipe_badges rb", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ri.quantity", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ri.unit", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ORDER BY ri.display_order", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("card_image_blob_name", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ingredient_nutrition", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("estimated_cost", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("@Servings", sql, StringComparison.OrdinalIgnoreCase);
    }

    private static RecipeCookingViewDto CookingView(int id) =>
        new(
            Id: id,
            Name: $"Recipe {id}",
            ImageUrl: null,
            Description: "Cooking description.",
            TotalTimeMinutes: 30,
            BaseServings: 4,
            CaloriesTotal: 1600,
            ProteinTotal: 120,
            CarbsTotal: 180,
            Badges: [],
            Ingredients: [],
            Steps: []);

    /// <summary>Captures Cooking View calls while supplying neutral behavior for unrelated recipe operations.</summary>
    private sealed class CapturingRecipeQueries : IRecipeQueries
    {
        internal List<(int CurrentUserId, int RecipeId)> CookingViewCalls { get; } = [];

        internal RecipeCookingViewDto? CookingViewResult { get; init; }

        public Task<RecipeCookingViewDto?> GetCookingViewAsync(
            int currentUserId,
            int recipeId,
            CancellationToken ct)
        {
            CookingViewCalls.Add((currentUserId, recipeId));
            return Task.FromResult(CookingViewResult);
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

        public Task<RecipeDiscoverySlice> DiscoverAsync(
            RecipeDiscoveryQuery query,
            CancellationToken ct) =>
            Task.FromResult(new RecipeDiscoverySlice([], false, null));

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

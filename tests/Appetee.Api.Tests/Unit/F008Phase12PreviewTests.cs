// Purpose: Verifies F-008 Phase 12 Preview validation, delegation, and bounded SQL shape.
// Change reason: Supply a neutral F-009 Favorites stub for the expanded recipe query contract.
// Created: 2026-08-28T11:50:10-06:00
// Last updated: 2026-08-29T14:05:58-06:00

using Appetee.Application.Abstractions.Recipes;
using Appetee.Application.Dtos;
using Appetee.Application.Models.Recipes;
using Appetee.Application.Requests;
using Appetee.Application.Services.Recipes;
using Appetee.Application.utils;
using Appetee.Infrastructure.Recipes;

namespace Appetee.Api.Tests.Unit;

/// <summary>Exercises the application boundary and SQL contract for Recipe Quick Preview.</summary>
public sealed class F008Phase12PreviewTests
{
    [Fact]
    public async Task RecipeService_ValidatesAndDelegatesPreviewIdentity()
    {
        var expected = Preview(7);
        var queries = new CapturingRecipeQueries { PreviewResult = expected };
        var service = new RecipeService(queries);

        var actual = await service.GetPreviewAsync(42, 7, CancellationToken.None);

        Assert.Same(expected, actual);
        Assert.Equal((42, 7), Assert.Single(queries.PreviewCalls));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(1, 0)]
    [InlineData(1, -1)]
    public async Task RecipeService_RejectsInvalidPreviewIdentifiersBeforePersistence(
        int currentUserId,
        int recipeId)
    {
        var queries = new CapturingRecipeQueries();
        var service = new RecipeService(queries);

        await Assert.ThrowsAsync<ValidationException>(
            () => service.GetPreviewAsync(currentUserId, recipeId, CancellationToken.None));

        Assert.Empty(queries.PreviewCalls);
    }

    [Fact]
    public void PreviewSql_UsesIndependentBoundedResultSetsAndCanonicalCompatibility()
    {
        var sql = RecipeSql.GetCompatiblePreview;

        Assert.Contains("r.id = @RecipeId", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ud.user_id = @CurrentUserId", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("uir.user_id = @CurrentUserId", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("r.image_blob_name            AS PreviewImageBlobName", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("fr_preview.user_id = @CurrentUserId", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("FROM recipe_badges rb", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("FROM recipe_ingredients ri", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ORDER BY ri.display_order", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("card_image_blob_name", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("instructions", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ingredient_nutrition", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("JOIN recipe_badges", sql, StringComparison.OrdinalIgnoreCase);
    }

    private static RecipePreviewDto Preview(int id) =>
        new(
            Id: id,
            Name: $"Recipe {id}",
            Description: "Preview description.",
            PreviewImageUrl: null,
            TotalTimeMinutes: 30,
            CaloriesPerServing: 400,
            ProteinPerServing: 30,
            EstimatedCostPerServing: 4,
            Badges: [],
            Ingredients: [],
            IsSaved: false);

    /// <summary>Captures Preview calls while supplying neutral behavior for unrelated recipe operations.</summary>
    private sealed class CapturingRecipeQueries : IRecipeQueries
    {
        internal List<(int CurrentUserId, int RecipeId)> PreviewCalls { get; } = [];

        internal RecipePreviewDto? PreviewResult { get; init; }

        public Task<RecipePreviewDto?> GetPreviewAsync(
            int currentUserId,
            int recipeId,
            CancellationToken ct)
        {
            PreviewCalls.Add((currentUserId, recipeId));
            return Task.FromResult(PreviewResult);
        }

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

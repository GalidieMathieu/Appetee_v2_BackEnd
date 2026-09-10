// Purpose: Defines persistence operations required by recipe application use cases.
// Change reason: Expose the F-010 compatibility-scoped Cooking View query.
// Created: Existing file; original timestamp was not recorded.
// Last updated: 2026-08-31T18:01:27-06:00

using Appetee.Application.Dtos;
using Appetee.Application.Models.Recipes;
using Appetee.Application.Requests;

namespace Appetee.Application.Abstractions.Recipes
{
    public interface IRecipeQueries
    {
        Task<RecipeDiscoverySlice> DiscoverAsync(
            RecipeDiscoveryQuery query,
            CancellationToken ct);

        Task<RecipePreviewDto?> GetPreviewAsync(
            int currentUserId,
            int recipeId,
            CancellationToken ct);

        Task<RecipeCookingViewDto?> GetCookingViewAsync(
            int currentUserId,
            int recipeId,
            CancellationToken ct);

        Task<IReadOnlyList<RecipeCardDto>> GetFavoritesAsync(
            int currentUserId,
            int? limit,
            CancellationToken ct);

        Task<bool> SaveFavoriteAsync(
            int currentUserId,
            int recipeId,
            CancellationToken ct);

        Task RemoveFavoriteAsync(
            int currentUserId,
            int recipeId,
            CancellationToken ct);

        Task<RecipeSummaryDto?> CreateRecipeWithDetailsAsync(
            RecipeDetailRequest request,
            CancellationToken ct);

        Task<RecipeSummaryDto?> UpdateRecipeWithDetailsAsync(
            int id,
            RecipeDetailRequest request,
            CancellationToken ct);

        Task<RecipeDetailDto?> GetRecipeWithDetailsByIdAsync(
            int id,
            CancellationToken ct);
    }
}

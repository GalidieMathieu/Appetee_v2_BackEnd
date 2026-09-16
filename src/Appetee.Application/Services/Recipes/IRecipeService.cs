// Purpose: Defines application use cases for recipe discovery, Quick Preview, Cooking View, favorites, details, and authoring.
// Change reason: Expose the validated F-010 Cooking View use case.
// Created: Existing file; original timestamp was not recorded.
// Last updated: 2026-08-31T18:01:27-06:00

using Appetee.Application.Dtos;
using Appetee.Application.Requests;

namespace Appetee.Application.Services.Recipes
{
    public interface IRecipeService
    {
        Task<RecipeDiscoveryPageDto> DiscoverAsync(
            int currentUserId,
            RecipeDiscoveryRequest request,
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

// Purpose: Defines persistence operations required by recipe application use cases.
// Change reason: Expose the F-008 Phase 12 compatibility-scoped Quick Preview query.
// Created: Existing file; original timestamp was not recorded.
// Last updated: 2026-08-28T11:50:10-06:00

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

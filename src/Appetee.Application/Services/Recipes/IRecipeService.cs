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

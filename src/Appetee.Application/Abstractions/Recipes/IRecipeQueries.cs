using Appetee.Application.Dtos;
using Appetee.Application.Requests;

namespace Appetee.Application.Abstractions.Recipes
{
    public interface IRecipeQueries
    {
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

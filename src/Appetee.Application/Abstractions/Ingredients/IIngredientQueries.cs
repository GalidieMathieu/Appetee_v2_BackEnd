using Appetee.Application.Dtos;
using Appetee.Application.Requests;
using Microsoft.AspNetCore.Mvc;


namespace Appetee.Application.Abstractions.Ingredients
{
    public interface IIngredientQueries
    {
        Task<IReadOnlyList<IngredientDto>> GetAllDiets(CancellationToken ct);

        Task<IngredientAdminDetailDto?> GetIngredientWithDetailsByIdAsync(int id, CancellationToken ct);

        Task<ActionResult<IngredientAdminDetailDto>> CreateIngredientWithDetailsAsync(
            IngredientAdminDetailRequest request, 
            CancellationToken ct);
    }

}

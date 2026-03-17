using Appetee.Application.Dtos;
using Appetee.Application.Requests;
using Microsoft.AspNetCore.Mvc;

namespace Appetee.Application.Services.Ingredients
{
    public interface IIngredientService
    {
        Task<IReadOnlyList<IngredientDto>> GetAll(CancellationToken ct);

        Task<IngredientAdminDetailDto?> GetIngredientWithDetailsByIdAsync(int id, CancellationToken ct);

        Task<ActionResult<IngredientAdminDetailDto>> CreateIngredientWithDetailsAsync(
            IngredientAdminDetailRequest request, 
            CancellationToken ct);
    }
}

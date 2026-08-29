// Purpose: Defines application operations for ingredient catalogue reads and administrative ingredient details.
// Change reason: Preserve GetAll for existing consumers and add a separate F-008 Phase 9 autocomplete operation.
// Created: Existing file; original timestamp was not recorded.
// Last updated: 2026-08-27T13:46:36-06:00

using Appetee.Application.Dtos;
using Appetee.Application.Requests;

namespace Appetee.Application.Services.Ingredients
{
    public interface IIngredientService
    {
        Task<IReadOnlyList<IngredientDto>> GetAll(CancellationToken ct);

        Task<IReadOnlyList<IngredientDto>> SearchAsync(
            string? search,
            int? limit,
            CancellationToken ct);

        Task<IngredientAdminDetailDto?> GetIngredientWithDetailsByIdAsync(int id, CancellationToken ct);

        Task<IngredientAdminDetailDto?> CreateIngredientWithDetailsAsync(
            IngredientAdminDetailRequest request, 
            CancellationToken ct);
    }
}

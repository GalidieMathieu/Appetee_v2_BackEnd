// Purpose: Defines persistence operations for lightweight ingredient reads and administrative ingredient details.
// Change reason: Add separate F-008 Phase 9 bounded name-search persistence without changing full catalogue reads.
// Created: Existing file; original timestamp was not recorded.
// Last updated: 2026-08-27T13:16:15-06:00

using Appetee.Application.Dtos;
using Appetee.Application.Requests;


namespace Appetee.Application.Abstractions.Ingredients
{
    public interface IIngredientQueries
    {
        Task<IReadOnlyList<IngredientDto>> GetAllAsync(CancellationToken ct);

        Task<IReadOnlyList<IngredientDto>> SearchByNameAsync(
            string normalizedSearch,
            int limit,
            CancellationToken ct);

        Task<IngredientAdminDetailDto?> GetIngredientWithDetailsByIdAsync(int id, CancellationToken ct);

        Task<IngredientAdminDetailDto?> CreateIngredientWithDetailsAsync(
            IngredientAdminDetailRequest request, 
            CancellationToken ct);
    }

}

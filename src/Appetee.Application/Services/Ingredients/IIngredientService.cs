using Appetee.Application.Dtos;

namespace Appetee.Application.Services.Ingredients
{
    public interface IIngredientService
    {
        Task<IReadOnlyList<IngredientDto>> GetAll(CancellationToken ct);
    }
}

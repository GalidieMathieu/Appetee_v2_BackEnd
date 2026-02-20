using Appetee.Application.Abstractions.Ingredients;
using Appetee.Application.Dtos;

namespace Appetee.Application.Services.Ingredients
{
    public sealed class IngredientService : IIngredientService
    {
        private readonly IIngredientQueries _queries;
        public IngredientService(IIngredientQueries queries)
        {
            _queries = queries;
        }

        public Task<IReadOnlyList<IngredientDto>> GetAll(CancellationToken ct) => _queries.GetAllDiets(ct);
    }
}

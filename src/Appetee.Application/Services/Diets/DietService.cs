using Appetee.Application.Abstractions.Diets;
using Appetee.Application.Dtos;


namespace Appetee.Application.Services.Diets
{
    public sealed class DietService : IDietService
    {
        private readonly IDietQueries _queries;
        public DietService(IDietQueries queries)
        {
            _queries = queries;
        }

        public Task<IReadOnlyList<DietDto>> GetAll(CancellationToken ct)=> _queries.GetAllDiets(ct);
    }
}

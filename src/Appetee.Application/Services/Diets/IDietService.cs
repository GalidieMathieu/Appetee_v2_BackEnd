
using Appetee.Application.Dtos;

namespace Appetee.Application.Services.Diets
{
    public interface IDietService
    {
        Task<IReadOnlyList<DietDto>> GetAll(CancellationToken ct);
    }
}

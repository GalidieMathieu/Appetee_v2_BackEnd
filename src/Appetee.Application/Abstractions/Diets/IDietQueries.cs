using Appetee.Application.Dtos;
using System;
using System.Collections.Generic;
using System.Text;

namespace Appetee.Application.Abstractions.Diets
{
    public interface IDietQueries
    {
        Task<IReadOnlyList<DietDto>> GetAllDiets(CancellationToken ct);
    }
}

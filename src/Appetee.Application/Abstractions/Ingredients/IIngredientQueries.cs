using Appetee.Application.Dtos;
using System;
using System.Collections.Generic;
using System.Text;

namespace Appetee.Application.Abstractions.Ingredients
{
    public interface IIngredientQueries
    {
        Task<IReadOnlyList<IngredientDto>> GetAllDiets(CancellationToken ct);
    }

}

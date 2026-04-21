using Appetee.Application.Abstractions.Ingredients;
using Appetee.Application.Dtos;
using Appetee.Application.Requests;
using Microsoft.AspNetCore.Mvc;
using Appetee.Application.utils;

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

        public Task<IngredientAdminDetailDto?> CreateIngredientWithDetailsAsync(
            IngredientAdminDetailRequest request,
            CancellationToken ct)
        {

            // Minimal validation (keep consistent with your existing validation approach)
            if (string.IsNullOrWhiteSpace(request.Name))
                throw new ValidationException("ingredient name is required.");

            if (request.Basis <= 0)
                throw new ValidationException("basis cannot be negative");

            if (request.CaloriesKcal <= 0)
                throw new ValidationException("kcal cannot be negative");

            if (request.Image is null)
            {
                throw new ValidationException("No image for ingredient");
            }


            return _queries.CreateIngredientWithDetailsAsync(request , ct);
        }

        public Task<IngredientAdminDetailDto?> GetIngredientWithDetailsByIdAsync(int id, CancellationToken ct)
            => _queries.GetIngredientWithDetailsByIdAsync(id, ct);
    }
}

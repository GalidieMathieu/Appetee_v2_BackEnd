using Appetee.Application.Abstractions.Ingredients;
using Appetee.Application.Dtos;
using Appetee.Application.Requests;
using Microsoft.AspNetCore.Mvc;
using Appetee.Application.utils;
using Microsoft.Extensions.Logging;

namespace Appetee.Application.Services.Ingredients
{
    public sealed class IngredientService : IIngredientService
    {
        private readonly IIngredientQueries _queries;
        private readonly ILogger<IngredientService> _logger;

        public IngredientService(IIngredientQueries queries, ILogger<IngredientService> logger)
        {
            _queries = queries;
            _logger = logger;
        }

        public Task<IReadOnlyList<IngredientDto>> GetAll(CancellationToken ct) => _queries.GetAllDiets(ct);

        public Task<IngredientAdminDetailDto?> CreateIngredientWithDetailsAsync(
            IngredientAdminDetailRequest request,
            CancellationToken ct)
        {
            _logger.LogInformation(
                "Ingredient creation started. HasImage {HasImage}",
                request.Image is not null);

            var basisUnit = (request.BasisUnit ?? string.Empty).Trim().ToLowerInvariant();

            // Minimal validation (keep consistent with your existing validation approach)
            if (string.IsNullOrWhiteSpace(request.Name))
                throw new ValidationException("ingredient name is required.");

            if (request.Basis <= 0)
                throw new ValidationException("basis must be greater than zero.");

            ValidateRequiredNonNegative(request.Price, "price");
            ValidateRequiredNonNegative(request.CaloriesKcal, "calories kcal");
            ValidateRequiredNonNegative(request.ProteinG, "protein g");
            ValidateRequiredNonNegative(request.CarbsG, "carbs g");

            if (basisUnit is not ("g" or "ml"))
                throw new ValidationException("basis unit must be either 'g' or 'ml'.");

            if (request.Image is null)
            {
                throw new ValidationException("No image for ingredient");
            }

            _logger.LogDebug(
                "Ingredient image validation passed. Content type {ContentType}; File length {FileLength}",
                request.Image.ContentType,
                request.Image.Length);

            return _queries.CreateIngredientWithDetailsAsync(
                request with { BasisUnit = basisUnit },
                ct);
        }

        private static void ValidateRequiredNonNegative(decimal? value, string field)
        {
            if (value is null)
                throw new ValidationException($"{field} is required.");

            if (value < 0)
                throw new ValidationException($"{field} cannot be negative.");
        }

        public Task<IngredientAdminDetailDto?> GetIngredientWithDetailsByIdAsync(int id, CancellationToken ct)
            => _queries.GetIngredientWithDetailsByIdAsync(id, ct);
    }
}

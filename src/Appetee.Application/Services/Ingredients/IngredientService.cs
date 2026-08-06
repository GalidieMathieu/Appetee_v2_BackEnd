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
                throw new ValidationException("basis cannot be negative");

            if (request.CaloriesKcal <= 0)
                throw new ValidationException("kcal cannot be negative");

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

        public Task<IngredientAdminDetailDto?> GetIngredientWithDetailsByIdAsync(int id, CancellationToken ct)
            => _queries.GetIngredientWithDetailsByIdAsync(id, ct);
    }
}

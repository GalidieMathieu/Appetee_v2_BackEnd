// Purpose: Validates ingredient catalogue/autocomplete reads and orchestrates administrative ingredient operations.
// Change reason: Preserve GetAll and isolate F-008 Phase 9 validation in a separate autocomplete operation.
// Created: Existing file; original timestamp was not recorded.
// Last updated: 2026-08-27T13:46:36-06:00

using Appetee.Application.Abstractions.Ingredients;
using Appetee.Application.Dtos;
using Appetee.Application.Requests;
using Appetee.Application.utils;
using Microsoft.Extensions.Logging;

namespace Appetee.Application.Services.Ingredients
{
    public sealed class IngredientService : IIngredientService
    {
        internal const int AutocompleteDefaultLimit = 10;
        internal const int AutocompleteMaximumLimit = 50;
        internal const int AutocompleteMinimumSearchLength = 2;
        internal const int AutocompleteMaximumSearchLength = 100;

        private readonly IIngredientQueries _queries;
        private readonly ILogger<IngredientService> _logger;

        public IngredientService(IIngredientQueries queries, ILogger<IngredientService> logger)
        {
            _queries = queries;
            _logger = logger;
        }

        public Task<IReadOnlyList<IngredientDto>> GetAll(CancellationToken ct) =>
            _queries.GetAllAsync(ct);

        /// <summary>Validates and normalizes ingredient autocomplete so every search remains bounded.</summary>
        public Task<IReadOnlyList<IngredientDto>> SearchAsync(
            string? search,
            int? limit,
            CancellationToken ct)
        {
            if (search is null)
                throw new ValidationException("ingredient search is required.");

            if (search.Length > AutocompleteMaximumSearchLength)
            {
                throw new ValidationException(
                    $"ingredient search cannot exceed {AutocompleteMaximumSearchLength} characters.");
            }

            var normalizedSearch = search.Trim();
            if (normalizedSearch.Length < AutocompleteMinimumSearchLength)
            {
                throw new ValidationException(
                    $"ingredient search must contain at least {AutocompleteMinimumSearchLength} characters.");
            }

            var effectiveLimit = limit ?? AutocompleteDefaultLimit;
            if (effectiveLimit is < 1 or > AutocompleteMaximumLimit)
            {
                throw new ValidationException(
                    $"ingredient search limit must be between 1 and {AutocompleteMaximumLimit}.");
            }

            return _queries.SearchByNameAsync(normalizedSearch, effectiveLimit, ct);
        }

        /// <summary>Validates authoritative ingredient nutrition and image inputs before persistence.</summary>
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

        /// <summary>Delegates the complete administrative ingredient read without affecting lightweight catalogue DTOs.</summary>
        public Task<IngredientAdminDetailDto?> GetIngredientWithDetailsByIdAsync(int id, CancellationToken ct)
            => _queries.GetIngredientWithDetailsByIdAsync(id, ct);
    }
}

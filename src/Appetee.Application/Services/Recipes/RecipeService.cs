using Appetee.Application.Abstractions.Recipes;
using Appetee.Application.Dtos;
using Appetee.Application.Models.Recipes;
using Appetee.Application.Requests;
using Appetee.Application.utils;

namespace Appetee.Application.Services.Recipes
{
    public sealed class RecipeService : IRecipeService
    {
        private readonly IRecipeQueries _queries;

        public RecipeService(IRecipeQueries queries)
        {
            _queries = queries;
        }

        public Task<RecipeSummaryDto?> CreateRecipeWithDetailsAsync(
            RecipeDetailRequest request,
            CancellationToken ct)
        {
            var normalizedRequest = NormalizeRequest(request);
            ValidateRecipeRequest(normalizedRequest, requireImage: true);

            return _queries.CreateRecipeWithDetailsAsync(normalizedRequest, ct);
        }

        public Task<RecipeSummaryDto?> UpdateRecipeWithDetailsAsync(
            int id,
            RecipeDetailRequest request,
            CancellationToken ct)
        {
            if (id <= 0)
                throw new ValidationException("recipe id must be greater than zero.");

            var normalizedRequest = NormalizeRequest(request);
            ValidateRecipeRequest(normalizedRequest, requireImage: false);

            return _queries.UpdateRecipeWithDetailsAsync(id, normalizedRequest, ct);
        }

        public Task<RecipeDetailDto?> GetRecipeWithDetailsByIdAsync(
            int id,
            CancellationToken ct)
        {
            if (id <= 0)
                throw new ValidationException("recipe id must be greater than zero.");

            return _queries.GetRecipeWithDetailsByIdAsync(id, ct);
        }

        private static RecipeDetailRequest NormalizeRequest(RecipeDetailRequest request) =>
            request with
            {
                Name = request.Name?.Trim() ?? string.Empty,
                Instructions = request.Instructions?.Trim() ?? string.Empty,
                Badges = (request.Badges ?? [])
                    .Select(badge => badge?.Trim() ?? string.Empty)
                    .Where(badge => badge.Length > 0)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                DietIds = (request.DietIds ?? [])
                    .Where(id => id > 0)
                    .Distinct()
                    .ToList(),
                Ingredients = (request.Ingredients ?? [])
                    .Where(ingredient => ingredient.IngredientId > 0)
                    .Select(ingredient => ingredient with
                    {
                        Unit = string.IsNullOrWhiteSpace(ingredient.Unit) ? null : ingredient.Unit.Trim(),
                    })
                    .ToList(),
            };

        private static void ValidateRecipeRequest(RecipeDetailRequest normalizedRequest, bool requireImage)
        {
            if (string.IsNullOrWhiteSpace(normalizedRequest.Name))
                throw new ValidationException("recipe name is required.");

            if (requireImage && (normalizedRequest.Image is null || normalizedRequest.Image.Length == 0))
                throw new ValidationException("recipe image is required.");

            if (!requireImage && normalizedRequest.Image is not null && normalizedRequest.Image.Length == 0)
                throw new ValidationException("recipe image cannot be empty.");

            if (normalizedRequest.PrepTimeMinutes <= 0)
                throw new ValidationException("prep time must be greater than zero.");

            if (normalizedRequest.Servings <= 0)
                throw new ValidationException("servings must be greater than zero.");

            if (normalizedRequest.Difficulty is null)
                throw new ValidationException("difficulty must be Easy, Medium, or Hard.");

            if (normalizedRequest.CaloriesTotal < 0 ||
                normalizedRequest.ProteinTotal < 0 ||
                normalizedRequest.CarbsTotal < 0)
                throw new ValidationException("nutrition totals cannot be negative.");

            if (normalizedRequest.EstimatedCostPerServing < 0)
                throw new ValidationException("estimated cost per serving cannot be negative.");

            if (string.IsNullOrWhiteSpace(normalizedRequest.Instructions))
                throw new ValidationException("instructions are required.");

            if (normalizedRequest.Ingredients.Count == 0)
                throw new ValidationException("at least one ingredient is required.");

            var invalidBadge = normalizedRequest.Badges.FirstOrDefault(badge => !RecipeBadgeValues.IsValid(badge));
            if (invalidBadge is not null)
                throw new ValidationException($"invalid badge '{invalidBadge}'.");

            var duplicateIngredientId = normalizedRequest.Ingredients
                .GroupBy(ingredient => ingredient.IngredientId)
                .Where(group => group.Key > 0 && group.Count() > 1)
                .Select(group => group.Key)
                .FirstOrDefault();
            if (duplicateIngredientId > 0)
                throw new ValidationException($"ingredient '{duplicateIngredientId}' was selected more than once.");

            foreach (var ingredient in normalizedRequest.Ingredients)
            {
                if (ingredient.Quantity is null || ingredient.Quantity <= 0)
                    throw new ValidationException($"ingredient '{ingredient.IngredientId}' quantity must be greater than zero when provided.");

                if (string.IsNullOrWhiteSpace(ingredient.Unit))
                    throw new ValidationException($"ingredient '{ingredient.IngredientId}' unit is required.");
            }
        }
    }
}

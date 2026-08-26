using Appetee.Application.Abstractions.Recipes;
using Appetee.Application.Dtos;
using Appetee.Application.Models.Recipes;
using Appetee.Application.Requests;
using Appetee.Application.utils;
using System.Security.Cryptography;

namespace Appetee.Application.Services.Recipes
{
    public sealed class RecipeService : IRecipeService
    {
        private readonly IRecipeQueries _queries;

        public RecipeService(IRecipeQueries queries)
        {
            _queries = queries;
        }

        public async Task<RecipeDiscoveryPageDto> DiscoverAsync(
            int currentUserId,
            RecipeDiscoveryRequest request,
            CancellationToken ct)
        {
            if (currentUserId <= 0)
                throw new ValidationException("current user id must be greater than zero.");

            ArgumentNullException.ThrowIfNull(request);

            if (request.Limit is < 1 or > 50)
                throw new ValidationException("limit must be between 1 and 50.");

            var criteria = new RecipeDiscoveryCriteria(
                CurrentUserId: currentUserId,
                EffectiveSearchTerms: [],
                IngredientIds: [],
                RequireAllIngredients: true,
                CanonicalBadges: [],
                MaxTotalMinutes: null,
                MaxDifficulty: null,
                SavedOnly: false,
                PageSize: request.Limit);
            var criteriaFingerprint = RecipeDiscoveryCriteriaFingerprint.Create(criteria);

            BrowseCursorV1? cursor = null;
            if (request.Cursor is not null)
            {
                cursor = RecipeDiscoveryCursorCodec.DecodeBrowse(request.Cursor);
                if (!string.Equals(
                        cursor.Criteria,
                        criteriaFingerprint,
                        StringComparison.Ordinal))
                {
                    throw new ValidationException(
                        "cursor does not match the current discovery criteria.");
                }
            }

            var browseSeed = cursor?.Seed
                ?? RandomNumberGenerator.GetInt32(1, int.MaxValue);
            var query = new RecipeDiscoveryQuery(
                CurrentUserId: currentUserId,
                PageSize: request.Limit,
                BrowseSeed: browseSeed,
                AfterRank: cursor?.Rank,
                AfterRecipeId: cursor?.Id);
            var slice = await _queries.DiscoverAsync(query, ct);

            string? nextCursor = null;
            if (slice.HasMore)
            {
                var continuation = slice.Continuation
                    ?? throw new InternalServerException(
                        "Recipe discovery continuation state is unavailable.");
                nextCursor = RecipeDiscoveryCursorCodec.Encode(
                    new BrowseCursorV1(
                        V: RecipeDiscoveryCursorCodec.CurrentVersion,
                        Mode: RecipeDiscoveryCursorCodec.BrowseMode,
                        Seed: browseSeed,
                        Rank: continuation.Rank,
                        Id: continuation.RecipeId,
                        Criteria: criteriaFingerprint));
            }

            return new RecipeDiscoveryPageDto(
                slice.Items,
                nextCursor,
                slice.HasMore);
        }

        public Task<RecipeSummaryDto?> CreateRecipeWithDetailsAsync(
            RecipeDetailRequest request,
            CancellationToken ct)
        {
            var normalizedRequest = PrepareRequest(request, requireImage: true);

            return _queries.CreateRecipeWithDetailsAsync(normalizedRequest, ct);
        }

        public Task<RecipeSummaryDto?> UpdateRecipeWithDetailsAsync(
            int id,
            RecipeDetailRequest request,
            CancellationToken ct)
        {
            if (id <= 0)
                throw new ValidationException("recipe id must be greater than zero.");

            var normalizedRequest = PrepareRequest(request, requireImage: false);

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

        private static RecipeDetailRequest PrepareRequest(RecipeDetailRequest request, bool requireImage)
        {
            var normalizedRequest = NormalizeRequest(request);
            ValidateRecipeRequest(normalizedRequest, requireImage);

            return normalizedRequest with
            {
                Instructions = normalizedRequest.Instructions
                    .Where(step => !string.IsNullOrEmpty(step.Title) && !string.IsNullOrEmpty(step.Instruction))
                    .ToList(),
            };
        }

        private static RecipeDetailRequest NormalizeRequest(RecipeDetailRequest request) =>
            request with
            {
                Name = request.Name?.Trim() ?? string.Empty,
                Description = request.Description?.Trim() ?? string.Empty,
                Instructions = (request.Instructions ?? [])
                    .Select(step => step is null
                        ? new RecipeInstructionStepRequest()
                        : step with
                        {
                            Title = step.Title?.Trim() ?? string.Empty,
                            Instruction = step.Instruction?.Trim() ?? string.Empty,
                        })
                    .ToList(),
                Badges = (request.Badges ?? [])
                    .Select(badge => badge?.Trim() ?? string.Empty)
                    .Where(badge => badge.Length > 0)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                DietIds = (request.DietIds ?? [])
                    .Distinct()
                    .ToList(),
                Ingredients = (request.Ingredients ?? [])
                    .Select(ingredient => ingredient with
                    {
                        Unit = string.IsNullOrWhiteSpace(ingredient.Unit)
                            ? null
                            : ingredient.Unit.Trim().ToLowerInvariant(),
                    })
                    .ToList(),
            };

        private static void ValidateRecipeRequest(RecipeDetailRequest normalizedRequest, bool requireImage)
        {
            if (string.IsNullOrWhiteSpace(normalizedRequest.Name))
                throw new ValidationException("recipe name is required.");

            if (string.IsNullOrWhiteSpace(normalizedRequest.Description))
                throw new ValidationException("recipe description is required.");

            if (normalizedRequest.Description.Length > 500)
                throw new ValidationException("recipe description cannot exceed 500 characters.");

            if (requireImage && (normalizedRequest.Image is null || normalizedRequest.Image.Length == 0))
                throw new ValidationException("recipe image is required.");

            if (!requireImage && normalizedRequest.Image is not null && normalizedRequest.Image.Length == 0)
                throw new ValidationException("recipe image cannot be empty.");

            if (normalizedRequest.PrepTimeMinutes < 0)
                throw new ValidationException("prep time cannot be negative.");

            if (normalizedRequest.CookTimeMinutes < 0)
                throw new ValidationException("cook time cannot be negative.");

            if (normalizedRequest.TotalTimeMinutes <= 0)
                throw new ValidationException("total time must be greater than zero.");

            if (normalizedRequest.TotalTimeMinutes < normalizedRequest.PrepTimeMinutes ||
                normalizedRequest.TotalTimeMinutes < normalizedRequest.CookTimeMinutes)
            {
                throw new ValidationException("total time cannot be less than prep time or cook time.");
            }

            if (normalizedRequest.Servings <= 0)
                throw new ValidationException("servings must be greater than zero.");

            if (normalizedRequest.Difficulty is null)
                throw new ValidationException("difficulty must be Easy, Medium, or Hard.");

            var completeInstructionCount = 0;
            for (var index = 0; index < normalizedRequest.Instructions.Count; index++)
            {
                var step = normalizedRequest.Instructions[index];
                var titleIsBlank = string.IsNullOrEmpty(step.Title);
                var instructionIsBlank = string.IsNullOrEmpty(step.Instruction);

                if (titleIsBlank != instructionIsBlank)
                {
                    throw new ValidationException(
                        $"instruction step at index {index} requires both title and instruction.");
                }

                if (!titleIsBlank)
                    completeInstructionCount++;
            }

            if (completeInstructionCount == 0)
                throw new ValidationException("at least one complete instruction step is required.");

            if (normalizedRequest.Ingredients.Count == 0)
                throw new ValidationException("at least one ingredient is required.");

            if (normalizedRequest.DietIds.Any(id => id <= 0))
                throw new ValidationException("diet ids must be greater than zero.");

            if (normalizedRequest.Ingredients.Any(ingredient => ingredient.IngredientId <= 0))
                throw new ValidationException("ingredient ids must be greater than zero.");

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

            var featuredOrders = normalizedRequest.Ingredients
                .Where(ingredient => ingredient.FeaturedOrder.HasValue)
                .Select(ingredient => ingredient.FeaturedOrder!.Value)
                .ToArray();
            if (featuredOrders.Length is < 1 or > 3)
                throw new ValidationException("between one and three recipe ingredients must be featured.");

            if (featuredOrders.Any(order => order is < 1 or > 3))
                throw new ValidationException("featured ingredient order must be between one and three.");

            var duplicateFeaturedOrder = featuredOrders
                .GroupBy(order => order)
                .FirstOrDefault(group => group.Count() > 1)?.Key;
            if (duplicateFeaturedOrder.HasValue)
                throw new ValidationException($"featured ingredient order '{duplicateFeaturedOrder}' was selected more than once.");

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

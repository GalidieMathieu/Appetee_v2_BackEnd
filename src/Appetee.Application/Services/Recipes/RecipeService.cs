// Purpose: Orchestrates recipe discovery, favorites, detail reads, and authoritative recipe writes.
// Change reason: Add validated F-009 Favorites list orchestration.
// Created: Existing file; original timestamp was not recorded.
// Last updated: 2026-08-29T14:05:58-06:00

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

        /// <summary>Normalizes public criteria, binds them to the cursor, and coordinates bounded discovery.</summary>
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

            var (normalizedSearch, effectiveSearchTerms) =
                RecipeSearchNormalizer.Normalize(request.Search);
            var isSearch = effectiveSearchTerms.Count > 0;
            var ingredientIds = NormalizeDiscoveryIngredientIds(request.IngredientIds);
            var requireAllIngredients =
                ingredientIds.Count == 0 || request.RequireAllIngredients;
            var canonicalBadges = NormalizeDiscoveryBadges(request.Badges);
            ValidateDiscoveryRangeFilters(request.MaxTotalMinutes, request.MaxDifficulty);
            var allowedDifficulties = GetAllowedDifficulties(request.MaxDifficulty);

            var criteria = new RecipeDiscoveryCriteria(
                CurrentUserId: currentUserId,
                EffectiveSearchTerms: effectiveSearchTerms,
                IngredientIds: ingredientIds,
                RequireAllIngredients: requireAllIngredients,
                CanonicalBadges: canonicalBadges,
                MaxTotalMinutes: request.MaxTotalMinutes,
                MaxDifficulty: request.MaxDifficulty,
                SavedOnly: request.SavedOnly,
                PageSize: request.Limit);
            var criteriaFingerprint = RecipeDiscoveryCriteriaFingerprint.Create(criteria);

            BrowseCursorV1? browseCursor = null;
            SearchCursorV1? searchCursor = null;
            if (request.Cursor is not null)
            {
                var cursorCriteria = isSearch
                    ? (searchCursor = RecipeDiscoveryCursorCodec.DecodeSearch(request.Cursor)).Criteria
                    : (browseCursor = RecipeDiscoveryCursorCodec.DecodeBrowse(request.Cursor)).Criteria;
                if (!string.Equals(
                        cursorCriteria,
                        criteriaFingerprint,
                        StringComparison.Ordinal))
                {
                    throw new ValidationException(
                        "cursor does not match the current discovery criteria.");
                }
            }

            int? browseSeed = isSearch
                ? null
                : browseCursor?.Seed
                  ?? RandomNumberGenerator.GetInt32(1, int.MaxValue);
            var query = new RecipeDiscoveryQuery(
                CurrentUserId: currentUserId,
                PageSize: request.Limit,
                NormalizedSearch: normalizedSearch,
                EffectiveSearchTerms: effectiveSearchTerms,
                CanonicalBadges: canonicalBadges,
                MaxTotalMinutes: request.MaxTotalMinutes,
                AllowedDifficulties: allowedDifficulties,
                SavedOnly: request.SavedOnly,
                BrowseSeed: browseSeed,
                AfterRank: searchCursor?.Rank ?? browseCursor?.Rank,
                AfterRecipeId: searchCursor?.Id ?? browseCursor?.Id)
            {
                IngredientIds = ingredientIds,
                RequireAllIngredients = requireAllIngredients,
            };
            var slice = await _queries.DiscoverAsync(query, ct);

            string? nextCursor = null;
            if (slice.HasMore)
            {
                var continuation = slice.Continuation
                    ?? throw new InternalServerException(
                        "Recipe discovery continuation state is unavailable.");
                nextCursor = isSearch
                    ? RecipeDiscoveryCursorCodec.Encode(
                        new SearchCursorV1(
                            V: RecipeDiscoveryCursorCodec.CurrentVersion,
                            Mode: RecipeDiscoveryCursorCodec.SearchMode,
                            Rank: continuation.Rank,
                            Id: continuation.RecipeId,
                            Criteria: criteriaFingerprint))
                    : RecipeDiscoveryCursorCodec.Encode(
                        new BrowseCursorV1(
                            V: RecipeDiscoveryCursorCodec.CurrentVersion,
                            Mode: RecipeDiscoveryCursorCodec.BrowseMode,
                            Seed: browseSeed!.Value,
                            Rank: continuation.Rank,
                            Id: continuation.RecipeId,
                            Criteria: criteriaFingerprint));
            }

            return new RecipeDiscoveryPageDto(
                slice.Items,
                nextCursor,
                slice.HasMore);
        }

        /// <summary>Validates, deduplicates, and sorts selected ingredients for SQL and cursor stability.</summary>
        private static IReadOnlyList<int> NormalizeDiscoveryIngredientIds(
            IReadOnlyList<int>? ingredientIds)
        {
            if (ingredientIds is null || ingredientIds.Count == 0)
                return [];

            if (ingredientIds.Count > 3)
                throw new ValidationException("no more than three ingredients may be selected.");

            if (ingredientIds.Any(id => id <= 0))
                throw new ValidationException("ingredient ids must be greater than zero.");

            return ingredientIds
                .Distinct()
                .Order()
                .ToArray();
        }

        /// <summary>Canonicalizes repeated badge inputs so SQL counts and cursor fingerprints share one stable set.</summary>
        private static IReadOnlyList<string> NormalizeDiscoveryBadges(
            IReadOnlyList<string>? badges)
        {
            if (badges is null || badges.Count == 0)
                return [];

            if (badges.Count > RecipeBadgeValues.All.Count)
                throw new ValidationException("no more than nine badges may be selected.");

            if (badges.Any(badge =>
                    string.IsNullOrEmpty(badge)
                    || badge.Length > 50
                    || !RecipeBadgeValues.IsValid(badge)))
            {
                throw new ValidationException("one or more badges are invalid.");
            }

            return RecipeBadgeValues
                .Order(badges.Distinct(StringComparer.Ordinal))
                .ToArray();
        }

        /// <summary>Rejects filter values that cannot be represented safely by the bounded discovery query.</summary>
        private static void ValidateDiscoveryRangeFilters(
            int? maxTotalMinutes,
            RecipeDifficulty? maxDifficulty)
        {
            if (maxTotalMinutes is <= 0 or > 1440)
                throw new ValidationException("max total minutes must be between 1 and 1440.");

            if (maxDifficulty.HasValue
                && !Enum.IsDefined(maxDifficulty.Value))
            {
                throw new ValidationException("max difficulty must be Easy, Medium, or Hard.");
            }
        }

        /// <summary>Translates ordinal difficulty semantics into explicit stored values without string sorting.</summary>
        private static IReadOnlyList<string> GetAllowedDifficulties(
            RecipeDifficulty? maxDifficulty) =>
            maxDifficulty switch
            {
                RecipeDifficulty.Easy => [RecipeDifficulty.Easy.ToString()],
                RecipeDifficulty.Medium =>
                [
                    RecipeDifficulty.Easy.ToString(),
                    RecipeDifficulty.Medium.ToString(),
                ],
                _ => [],
            };

        public Task<bool> SaveFavoriteAsync(
            int currentUserId,
            int recipeId,
            CancellationToken ct)
        {
            ValidateCurrentUserRecipeId(currentUserId, recipeId);
            return _queries.SaveFavoriteAsync(currentUserId, recipeId, ct);
        }

        public Task<RecipePreviewDto?> GetPreviewAsync(
            int currentUserId,
            int recipeId,
            CancellationToken ct)
        {
            ValidateCurrentUserRecipeId(currentUserId, recipeId);
            return _queries.GetPreviewAsync(currentUserId, recipeId, ct);
        }

        /// <summary>Validates the optional collection bound before loading current-user compatible Favorites.</summary>
        public Task<IReadOnlyList<RecipeCardDto>> GetFavoritesAsync(
            int currentUserId,
            int? limit,
            CancellationToken ct)
        {
            if (currentUserId <= 0)
                throw new ValidationException("current user id must be greater than zero.");

            if (limit is < 1 or > 50)
                throw new ValidationException("limit must be between 1 and 50.");

            return _queries.GetFavoritesAsync(currentUserId, limit, ct);
        }

        public Task RemoveFavoriteAsync(
            int currentUserId,
            int recipeId,
            CancellationToken ct)
        {
            ValidateCurrentUserRecipeId(currentUserId, recipeId);
            return _queries.RemoveFavoriteAsync(currentUserId, recipeId, ct);
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

        /// <summary>Normalizes authored recipe input, validates invariants, and removes incomplete empty instruction rows.</summary>
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

        /// <summary>Canonicalizes authored text, collections, units, and duplicate selections before validation.</summary>
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

        /// <summary>Enforces persisted recipe, instruction, badge, ingredient, and featured-order invariants.</summary>
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

        /// <summary>Protects current-user recipe reads and mutations from invalid internal or route identifiers.</summary>
        private static void ValidateCurrentUserRecipeId(int currentUserId, int recipeId)
        {
            if (currentUserId <= 0)
                throw new ValidationException("current user id must be greater than zero.");

            if (recipeId <= 0)
                throw new ValidationException("recipe id must be greater than zero.");
        }
    }
}

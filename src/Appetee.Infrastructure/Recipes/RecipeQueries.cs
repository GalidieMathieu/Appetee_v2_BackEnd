// Purpose: Implements bounded recipe discovery, Quick Preview, Cooking View, favorites, details, and writes with Dapper/MySQL.
// Change reason: Add the F-010 compatibility-scoped Cooking View read in one Dapper round-trip.
// Created: Existing file; original timestamp was not recorded.
// Last updated: 2026-08-31T18:01:27-06:00

using Appetee.Application.Abstractions.Recipes;
using Appetee.Application.Dtos;
using Appetee.Application.Models.Recipes;
using Appetee.Application.Requests;
using Appetee.Application.RowData;
using Appetee.Application.utils;
using Appetee.Infrastructure.Data;
using Microsoft.Extensions.Logging;
using System.Data;
using System.Diagnostics;
using System.Text.Json;

using Dapper;

namespace Appetee.Infrastructure.Recipes
{
    public sealed class RecipeQueries : IRecipeQueries
    {
        private static readonly JsonSerializerOptions InstructionJsonOptions = new(JsonSerializerDefaults.Web);
        private readonly IBlobStorageService _blobStorageService;
        private readonly IDbConnectionFactory _db;
        private readonly ILogger<RecipeQueries> _logger;

        public RecipeQueries(
            IDbConnectionFactory db,
            IBlobStorageService blobStorageService,
            ILogger<RecipeQueries> logger)
        {
            _db = db ?? throw new ValidationException(nameof(db));
            _blobStorageService = blobStorageService ?? throw new ValidationException(nameof(blobStorageService));
            _logger = logger ?? throw new ValidationException(nameof(logger));
        }

        /// <summary>Executes bounded candidate and hydration commands while recording aggregate privacy-safe timings.</summary>
        public async Task<RecipeDiscoverySlice> DiscoverAsync(
            RecipeDiscoveryQuery query,
            CancellationToken ct)
        {
            using var conn = await _db.CreateOpenConnectionAsync(ct);
            var (candidateSql, candidateParameters) = RecipeDiscoverySqlBuilder.Build(query);

            var candidateStartedAt = Stopwatch.GetTimestamp();
            var recipeRows = (await conn.QueryAsync<RecipeDiscoveryRowData>(
                new CommandDefinition(
                    candidateSql,
                    candidateParameters,
                    cancellationToken: ct))).AsList();
            var candidateDurationMs = Stopwatch
                .GetElapsedTime(candidateStartedAt)
                .TotalMilliseconds;

            _logger.LogDebug(
                "Recipe discovery {DiscoveryMode} candidate query completed in {CandidateDurationMs} ms with {CandidateCount} rows.",
                query.IsSearch ? "search" : "browse",
                candidateDurationMs,
                recipeRows.Count);

            var hasMore = recipeRows.Count > query.PageSize;
            if (hasMore)
                recipeRows.RemoveRange(query.PageSize, recipeRows.Count - query.PageSize);

            if (recipeRows.Count == 0)
            {
                _logger.LogDebug(
                    "Recipe discovery {DiscoveryMode} returned no compatible candidates.",
                    query.IsSearch ? "search" : "browse");

                return new RecipeDiscoverySlice([], HasMore: false, Continuation: null);
            }

            var cards = await HydrateCardsAsync(conn, recipeRows, "discovery", ct);

            var continuation = hasMore
                ? new RecipeDiscoveryContinuation(
                    recipeRows[^1].SortRank,
                    recipeRows[^1].Id)
                : null;

            _logger.LogDebug(
                "Recipe discovery {DiscoveryMode} page returned {ReturnedCount} cards for page size {PageSize}; has more: {HasMore}.",
                query.IsSearch ? "search" : "browse",
                cards.Count,
                query.PageSize,
                hasMore);

            return new RecipeDiscoverySlice(cards, hasMore, continuation);
        }

        /// <summary>Loads current-user Favorites using fixed SQL shapes and shared set-based card hydration.</summary>
        public async Task<IReadOnlyList<RecipeCardDto>> GetFavoritesAsync(
            int currentUserId,
            int? limit,
            CancellationToken ct)
        {
            using var conn = await _db.CreateOpenConnectionAsync(ct);
            var startedAt = Stopwatch.GetTimestamp();
            var sql = limit.HasValue
                ? RecipeSql.GetFavoriteCandidatesWithLimit
                : RecipeSql.GetFavoriteCandidates;
            var recipeRows = (await conn.QueryAsync<RecipeDiscoveryRowData>(
                new CommandDefinition(
                    sql,
                    new
                    {
                        CurrentUserId = currentUserId,
                        Limit = limit,
                    },
                    cancellationToken: ct))).AsList();

            _logger.LogDebug(
                "Favorites candidate query completed in {CandidateDurationMs} ms with {CandidateCount} rows; limited: {HasLimit}.",
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds,
                recipeRows.Count,
                limit.HasValue);

            if (recipeRows.Count == 0)
                return [];

            return await HydrateCardsAsync(conn, recipeRows, "favorites", ct);
        }

        /// <summary>Hydrates badges and featured ingredients in one bounded relationship command for every card-list use case.</summary>
        private async Task<IReadOnlyList<RecipeCardDto>> HydrateCardsAsync(
            IDbConnection conn,
            IReadOnlyList<RecipeDiscoveryRowData> recipeRows,
            string queryKind,
            CancellationToken ct)
        {
            var recipeIds = recipeRows.Select(row => row.Id).ToArray();
            var hydrationStartedAt = Stopwatch.GetTimestamp();
            using var grid = await conn.QueryMultipleAsync(
                new CommandDefinition(
                    RecipeSql.HydrateDiscoveryCards,
                    new { RecipeIds = recipeIds },
                    cancellationToken: ct));

            var badgeRows = (await grid.ReadAsync<RecipeBadgeRowData>()).AsList();
            var ingredientRows = (await grid.ReadAsync<RecipeFeaturedIngredientRowData>()).AsList();

            _logger.LogDebug(
                "Recipe card hydration for {QueryKind} completed in {HydrationDurationMs} ms for {HydratedRecipeCount} recipes.",
                queryKind,
                Stopwatch.GetElapsedTime(hydrationStartedAt).TotalMilliseconds,
                recipeIds.Length);

            var badgesByRecipe = badgeRows
                .GroupBy(row => row.RecipeId)
                .ToDictionary(
                    group => group.Key,
                    group => (IReadOnlyList<string>)RecipeBadgeValues
                        .Order(group.Select(row => row.Badge))
                        .ToList());
            var ingredientsByRecipe = ingredientRows
                .GroupBy(row => row.RecipeId)
                .ToDictionary(
                    group => group.Key,
                    group => (IReadOnlyList<FeaturedIngredientDto>)group
                        .OrderBy(row => row.FeaturedOrder)
                        .Select(row => new FeaturedIngredientDto(
                            row.Id,
                            row.Name,
                            row.FeaturedOrder))
                        .ToList());

            return recipeRows.Select(row => new RecipeCardDto(
                Id: row.Id,
                Name: row.Name,
                CardImageUrl: ResolveBlobUrl(row.CardImageBlobName),
                TotalTimeMinutes: row.TotalTimeMinutes,
                CaloriesPerServing: row.CaloriesPerServing,
                EstimatedCostPerServing: row.EstimatedCostPerServing,
                Badges: badgesByRecipe.GetValueOrDefault(row.Id) ?? [],
                FeaturedIngredients: ingredientsByRecipe.GetValueOrDefault(row.Id) ?? [],
                IsSaved: row.IsSaved != 0
            )).ToList();
        }

        /// <summary>Applies compatibility before the idempotent save and records only its coarse outcome.</summary>
        public async Task<bool> SaveFavoriteAsync(
            int currentUserId,
            int recipeId,
            CancellationToken ct)
        {
            using var conn = await _db.CreateOpenConnectionAsync(ct);
            var isCompatible = await conn.ExecuteScalarAsync<long>(
                new CommandDefinition(
                    RecipeSql.IsCompatibleFavoriteCandidate,
                    new
                    {
                        CurrentUserId = currentUserId,
                        RecipeId = recipeId,
                    },
                    cancellationToken: ct));

            if (isCompatible == 0)
            {
                _logger.LogDebug(
                    "Favorite save completed with result {FavoriteMutationResult}.",
                    "not-found-or-incompatible");
                return false;
            }

            await conn.ExecuteAsync(
                new CommandDefinition(
                    RecipeSql.EnsureFavorite,
                    new
                    {
                        CurrentUserId = currentUserId,
                        RecipeId = recipeId,
                    },
                    cancellationToken: ct));

            _logger.LogDebug(
                "Favorite save completed with result {FavoriteMutationResult}.",
                "saved-or-already-saved");

            return true;
        }

        /// <summary>Loads one compatibility-scoped Preview and its independent badge/ingredient result sets.</summary>
        public async Task<RecipePreviewDto?> GetPreviewAsync(
            int currentUserId,
            int recipeId,
            CancellationToken ct)
        {
            using var conn = await _db.CreateOpenConnectionAsync(ct);
            var previewStartedAt = Stopwatch.GetTimestamp();
            using var grid = await conn.QueryMultipleAsync(
                new CommandDefinition(
                    RecipeSql.GetCompatiblePreview,
                    new
                    {
                        CurrentUserId = currentUserId,
                        RecipeId = recipeId,
                    },
                    cancellationToken: ct));

            var recipe = await grid.ReadSingleOrDefaultAsync<RecipePreviewRowData>();
            if (recipe is null)
            {
                _logger.LogDebug(
                    "Recipe Preview query completed in {PreviewDurationMs} ms with result {PreviewResult}.",
                    Stopwatch.GetElapsedTime(previewStartedAt).TotalMilliseconds,
                    "not-found-or-incompatible");
                return null;
            }

            var badges = RecipeBadgeValues
                .Order(await grid.ReadAsync<string>())
                .ToArray();
            var ingredients = (await grid.ReadAsync<RecipePreviewIngredientRowData>())
                .Select(row => new RecipePreviewIngredientDto(row.Id, row.Name))
                .ToArray();

            var preview = new RecipePreviewDto(
                Id: recipe.Id,
                Name: recipe.Name,
                Description: recipe.Description,
                PreviewImageUrl: ResolveBlobUrl(recipe.PreviewImageBlobName),
                TotalTimeMinutes: recipe.TotalTimeMinutes,
                CaloriesPerServing: recipe.CaloriesPerServing,
                ProteinPerServing: recipe.ProteinPerServing,
                EstimatedCostPerServing: recipe.EstimatedCostPerServing,
                Badges: badges,
                Ingredients: ingredients,
                IsSaved: recipe.IsSaved != 0);

            _logger.LogDebug(
                "Recipe Preview query completed in {PreviewDurationMs} ms with result {PreviewResult}.",
                Stopwatch.GetElapsedTime(previewStartedAt).TotalMilliseconds,
                "returned");

            return preview;
        }

        /// <summary>Loads one compatibility-scoped Cooking View and maps immutable authored order and base values.</summary>
        public async Task<RecipeCookingViewDto?> GetCookingViewAsync(
            int currentUserId,
            int recipeId,
            CancellationToken ct)
        {
            using var conn = await _db.CreateOpenConnectionAsync(ct);
            var cookingViewStartedAt = Stopwatch.GetTimestamp();
            using var grid = await conn.QueryMultipleAsync(
                new CommandDefinition(
                    RecipeSql.GetCompatibleCookingView,
                    new
                    {
                        CurrentUserId = currentUserId,
                        RecipeId = recipeId,
                    },
                    cancellationToken: ct));

            var recipe = await grid.ReadSingleOrDefaultAsync<RecipeCookingRowData>();
            if (recipe is null)
            {
                _logger.LogDebug(
                    "Recipe Cooking View query completed in {CookingViewDurationMs} ms with result {CookingViewResult}.",
                    Stopwatch.GetElapsedTime(cookingViewStartedAt).TotalMilliseconds,
                    "not-found-or-incompatible");
                return null;
            }

            var badges = RecipeBadgeValues
                .Order(await grid.ReadAsync<string>())
                .ToArray();
            var ingredients = (await grid.ReadAsync<RecipeCookingIngredientRowData>())
                .Select(row => new RecipeCookingIngredientDto(
                    row.Id,
                    row.Name,
                    row.Quantity,
                    row.Unit,
                    row.DisplayOrder))
                .ToArray();
            var steps = DeserializeInstructions(recipe.Instructions)
                .Select((step, index) => new RecipeCookingStepDto(
                    Order: index + 1,
                    Title: step.Title,
                    Instruction: step.Instruction))
                .ToArray();

            var cookingView = new RecipeCookingViewDto(
                Id: recipe.Id,
                Name: recipe.Name,
                ImageUrl: ResolveBlobUrl(recipe.ImageBlobName),
                Description: recipe.Description,
                TotalTimeMinutes: recipe.TotalTimeMinutes,
                BaseServings: recipe.BaseServings,
                CaloriesTotal: recipe.CaloriesTotal,
                ProteinTotal: recipe.ProteinTotal,
                CarbsTotal: recipe.CarbsTotal,
                Badges: badges,
                Ingredients: ingredients,
                Steps: steps);

            _logger.LogDebug(
                "Recipe Cooking View query completed in {CookingViewDurationMs} ms with result {CookingViewResult}.",
                Stopwatch.GetElapsedTime(cookingViewStartedAt).TotalMilliseconds,
                "returned");

            return cookingView;
        }

        /// <summary>Removes only the current-user membership and records no ownership identifiers.</summary>
        public async Task RemoveFavoriteAsync(
            int currentUserId,
            int recipeId,
            CancellationToken ct)
        {
            using var conn = await _db.CreateOpenConnectionAsync(ct);
            await conn.ExecuteAsync(
                new CommandDefinition(
                    RecipeSql.RemoveFavorite,
                    new
                    {
                        CurrentUserId = currentUserId,
                        RecipeId = recipeId,
                    },
                    cancellationToken: ct));

            _logger.LogDebug(
                "Favorite delete completed with result {FavoriteMutationResult}.",
                "removed-or-already-absent");
        }

        public async Task<RecipeSummaryDto?> CreateRecipeWithDetailsAsync(RecipeDetailRequest request, CancellationToken ct)
        {
            var blobName = await UploadRecipeImageAsync(request, ct);

            using var conn = await _db.CreateOpenConnectionAsync(ct);
            using var tran = conn.BeginTransaction();

            try
            {
                var recipeReferences = await LoadRecipeReferenceDataAsync(conn, tran, request, ct);

                var recipeId = await conn.ExecuteScalarAsync<int>(
                    new CommandDefinition(
                        RecipeSql.CreateRecipe,
                        new
                        {
                            request.Name,
                            request.Description,
                            PreviewImageBlobName = blobName,
                            InstructionsJson = SerializeInstructions(request.Instructions),
                            request.PrepTimeMinutes,
                            request.CookTimeMinutes,
                            request.TotalTimeMinutes,
                            request.Servings,
                            Difficulty = request.Difficulty!.Value.ToString(),
                            recipeReferences.Totals.EstimatedCostPerServing,
                            recipeReferences.Totals.CaloriesTotal,
                            recipeReferences.Totals.ProteinTotal,
                            recipeReferences.Totals.CarbsTotal,
                            recipeReferences.Totals.CaloriesPerServing,
                            recipeReferences.Totals.ProteinPerServing
                        },
                        transaction: tran,
                        cancellationToken: ct
                    )
                );

                if (recipeReferences.DietIds.Length > 0)
                {
                    var (sql, parameters) = BulkInsertSql.BuildBulkInsertRecipeDiets(recipeId, recipeReferences.DietIds);
                    await conn.ExecuteAsync(new CommandDefinition(sql, parameters, transaction: tran, cancellationToken: ct));
                }

                if (recipeReferences.BadgeValues.Length > 0)
                {
                    var (sql, parameters) = BulkInsertSql.BuildBulkInsertRecipeBadges(recipeId, recipeReferences.BadgeValues);
                    await conn.ExecuteAsync(new CommandDefinition(sql, parameters, transaction: tran, cancellationToken: ct));
                }

                if (recipeReferences.IngredientRequests.Length > 0)
                {
                    var (sql, parameters) = BulkInsertSql.BuildBulkInsertRecipeIngredients(recipeId, recipeReferences.IngredientRequests);
                    await conn.ExecuteAsync(new CommandDefinition(sql, parameters, transaction: tran, cancellationToken: ct));
                }

                tran.Commit();

                return BuildRecipeSummaryDto(recipeId, request, blobName, recipeReferences);
            }
            catch
            {
                try { tran.Rollback(); } catch { }

                await DeleteBlobIfExistsAsync(blobName, ct);

                throw;
            }
        }

        public async Task<RecipeSummaryDto?> UpdateRecipeWithDetailsAsync(int id, RecipeDetailRequest request, CancellationToken ct)
        {
            string? currentPreviewImageBlobName;
            using (var lookupConn = await _db.CreateOpenConnectionAsync(ct))
            {
                var existingRecipe = await lookupConn.QuerySingleOrDefaultAsync<RecipeImageBlobRowData>(
                    new CommandDefinition(
                        RecipeSql.GetImageBlobById,
                        new { id },
                        cancellationToken: ct
                    )
                );

                if (existingRecipe is null)
                    return null;

                currentPreviewImageBlobName = existingRecipe.PreviewImageBlobName;
            }

            var newPreviewImageBlobName = await UploadRecipeImageAsync(request, ct);
            var nextPreviewImageBlobName = newPreviewImageBlobName ?? currentPreviewImageBlobName;

            using var conn = await _db.CreateOpenConnectionAsync(ct);
            using var tran = conn.BeginTransaction();

            try
            {
                var recipeReferences = await LoadRecipeReferenceDataAsync(conn, tran, request, ct);

                var affected = await conn.ExecuteAsync(
                    new CommandDefinition(
                        RecipeSql.UpdateRecipe,
                        new
                        {
                            Id = id,
                            request.Name,
                            request.Description,
                            PreviewImageBlobName = nextPreviewImageBlobName,
                            ClearCardImage = !string.IsNullOrWhiteSpace(newPreviewImageBlobName),
                            InstructionsJson = SerializeInstructions(request.Instructions),
                            request.PrepTimeMinutes,
                            request.CookTimeMinutes,
                            request.TotalTimeMinutes,
                            request.Servings,
                            Difficulty = request.Difficulty!.Value.ToString(),
                            recipeReferences.Totals.EstimatedCostPerServing,
                            recipeReferences.Totals.CaloriesTotal,
                            recipeReferences.Totals.ProteinTotal,
                            recipeReferences.Totals.CarbsTotal,
                            recipeReferences.Totals.CaloriesPerServing,
                            recipeReferences.Totals.ProteinPerServing
                        },
                        transaction: tran,
                        cancellationToken: ct
                    )
                );

                if (affected == 0)
                {
                    try { tran.Rollback(); } catch { }
                    await DeleteBlobIfExistsAsync(newPreviewImageBlobName, ct);
                    return null;
                }

                await conn.ExecuteAsync(new CommandDefinition(
                    RecipeSql.DeleteRecipeDietsByRecipeId,
                    new { id },
                    transaction: tran,
                    cancellationToken: ct
                ));
                await conn.ExecuteAsync(new CommandDefinition(
                    RecipeSql.DeleteRecipeBadgesByRecipeId,
                    new { id },
                    transaction: tran,
                    cancellationToken: ct
                ));
                await conn.ExecuteAsync(new CommandDefinition(
                    RecipeSql.DeleteRecipeIngredientsByRecipeId,
                    new { id },
                    transaction: tran,
                    cancellationToken: ct
                ));

                if (recipeReferences.DietIds.Length > 0)
                {
                    var (sql, parameters) = BulkInsertSql.BuildBulkInsertRecipeDiets(id, recipeReferences.DietIds);
                    await conn.ExecuteAsync(new CommandDefinition(sql, parameters, transaction: tran, cancellationToken: ct));
                }

                if (recipeReferences.BadgeValues.Length > 0)
                {
                    var (sql, parameters) = BulkInsertSql.BuildBulkInsertRecipeBadges(id, recipeReferences.BadgeValues);
                    await conn.ExecuteAsync(new CommandDefinition(sql, parameters, transaction: tran, cancellationToken: ct));
                }

                if (recipeReferences.IngredientRequests.Length > 0)
                {
                    var (sql, parameters) = BulkInsertSql.BuildBulkInsertRecipeIngredients(id, recipeReferences.IngredientRequests);
                    await conn.ExecuteAsync(new CommandDefinition(sql, parameters, transaction: tran, cancellationToken: ct));
                }

                tran.Commit();

                if (!string.IsNullOrWhiteSpace(newPreviewImageBlobName)
                    && !string.IsNullOrWhiteSpace(currentPreviewImageBlobName)
                    && !string.Equals(newPreviewImageBlobName, currentPreviewImageBlobName, StringComparison.Ordinal))
                {
                    await DeleteBlobIfExistsAsync(currentPreviewImageBlobName, ct);
                }

                return BuildRecipeSummaryDto(id, request, nextPreviewImageBlobName, recipeReferences);
            }
            catch
            {
                try { tran.Rollback(); } catch { }
                await DeleteBlobIfExistsAsync(newPreviewImageBlobName, ct);
                throw;
            }
        }

        public async Task<RecipeDetailDto?> GetRecipeWithDetailsByIdAsync(int id, CancellationToken ct)
        {
            using var conn = await _db.CreateOpenConnectionAsync(ct);

            using var grid = await conn.QueryMultipleAsync(
                new CommandDefinition(
                    RecipeSql.GetWithDetailsById,
                    new { id },
                    cancellationToken: ct
                )
            );

            var recipe = await grid.ReadSingleOrDefaultAsync<RecipeDetailRowData>();
            if (recipe is null)
            {
                return null;
            }

            var diets = (await grid.ReadAsync<DietDto>()).AsList();
            var badges = RecipeBadgeValues.Order(await grid.ReadAsync<string>()).ToList();
            var ingredientRows = (await grid.ReadAsync<RecipeIngredientDetailRowData>()).AsList();

            string? previewImageUrl = null;
            if (!string.IsNullOrWhiteSpace(recipe.PreviewImageBlobName))
            {
                try
                {
                    previewImageUrl = _blobStorageService.GetUri(recipe.PreviewImageBlobName).ToString();
                }
                catch
                {
                    previewImageUrl = null;
                }
            }

            var ingredients = ingredientRows.Select(row =>
            {
                string? ingredientImageUrl = null;
                if (!string.IsNullOrWhiteSpace(row.ImageBlobName))
                {
                    try
                    {
                        ingredientImageUrl = _blobStorageService.GetUri(row.ImageBlobName).ToString();
                    }
                    catch
                    {
                        ingredientImageUrl = null;
                    }
                }

                return new RecipeIngredientDetailDto(
                    IngredientId: row.IngredientId,
                    Quantity: row.Quantity,
                    Unit: row.Unit,
                    DisplayOrder: row.DisplayOrder,
                    FeaturedOrder: row.FeaturedOrder,
                    Ingredient: new IngredientAdminDetailDto(
                        Id: row.Id,
                        Name: row.Name,
                        Basis: row.Basis,
                        BasisUnit: row.BasisUnit,
                        CaloriesKcal: row.CaloriesKcal,
                        Price: row.Price,
                        ImageUrl: ingredientImageUrl,
                        ProteinG: row.ProteinG,
                        FatG: row.FatG,
                        CarbsG: row.CarbsG,
                        SugarG: row.SugarG,
                        FiberG: row.FiberG,
                        SodiumMg: row.SodiumMg,
                        VitaminCMg: row.VitaminCMg,
                        IronMg: row.IronMg
                    )
                );
            }).ToList();

            var instructions = DeserializeInstructions(recipe.Instructions);

            return new RecipeDetailDto(
                Id: recipe.Id,
                Name: recipe.Name,
                Description: recipe.Description,
                PreviewImageUrl: previewImageUrl,
                PrepTimeMinutes: recipe.PrepTimeMinutes,
                CookTimeMinutes: recipe.CookTimeMinutes,
                TotalTimeMinutes: recipe.TotalTimeMinutes,
                Servings: recipe.Servings,
                Difficulty: recipe.Difficulty,
                Badges: badges.Count == 0 ? null : badges,
                Diets: diets.Count == 0 ? null : diets,
                EstimatedCostPerServing: recipe.EstimatedCostPerServing,
                Instructions: instructions,
                Ingredients: ingredients,
                CaloriesTotal: recipe.CaloriesTotal,
                ProteinTotal: recipe.ProteinTotal,
                CarbsTotal: recipe.CarbsTotal,
                CaloriesPerServing: recipe.CaloriesPerServing,
                ProteinPerServing: recipe.ProteinPerServing
            );
        }

        private async Task<(int[] DietIds, RecipeIngredientRequest[] IngredientRequests, string[] BadgeValues, List<DietDto> DietDtos, List<IngredientDto> IngredientDtos, RecipeCalculatedTotals Totals)> LoadRecipeReferenceDataAsync(
            System.Data.IDbConnection conn,
            System.Data.IDbTransaction tran,
            RecipeDetailRequest request,
            CancellationToken ct)
        {
            var dietIds = request.DietIds.Distinct().ToArray();
            var ingredientRequests = request.Ingredients.ToArray();
            var ingredientIds = ingredientRequests.Select(ingredient => ingredient.IngredientId).Distinct().ToArray();
            var badgeValues = RecipeBadgeValues
                .Order(request.Badges.Distinct(StringComparer.Ordinal))
                .ToArray();

            var dietDtos = new List<DietDto>();
            if (dietIds.Length > 0)
            {
                dietDtos = (await conn.QueryAsync<DietDto>(
                    new CommandDefinition(
                        DietSql.GetByIds,
                        new { Ids = dietIds },
                        transaction: tran,
                        cancellationToken: ct
                    ))).AsList();

                var foundDietIds = dietDtos.Select(diet => diet.id).ToHashSet();
                var missingDietIds = dietIds.Where(id => !foundDietIds.Contains(id)).ToArray();
                if (missingDietIds.Length > 0)
                    throw new ValidationException($"Invalid DietIds: {string.Join(", ", missingDietIds)}");
            }

            var ingredientDtos = new List<IngredientDto>();
            RecipeCalculatedTotals totals;
            if (ingredientIds.Length > 0)
            {
                var calculationRows = (await conn.QueryAsync<RecipeIngredientCalculationData>(
                    new CommandDefinition(
                        RecipeSql.GetIngredientCalculationDataByIds,
                        new { Ids = ingredientIds },
                        transaction: tran,
                        cancellationToken: ct
                    ))).AsList();

                var foundIngredientIds = calculationRows.Select(ingredient => ingredient.Id).ToHashSet();
                var missingIngredientIds = ingredientIds.Where(id => !foundIngredientIds.Contains(id)).ToArray();
                if (missingIngredientIds.Length > 0)
                    throw new ValidationException($"Invalid IngredientIds: {string.Join(", ", missingIngredientIds)}");

                var calculationData = calculationRows.ToDictionary(ingredient => ingredient.Id);
                totals = RecipeCalculator.Calculate(ingredientRequests, calculationData, request.Servings);
                ingredientDtos = calculationRows
                    .Select(ingredient => new IngredientDto(ingredient.Id, ingredient.Name))
                    .ToList();
            }
            else
            {
                throw new ValidationException("at least one ingredient is required.");
            }

            return (dietIds, ingredientRequests, badgeValues, dietDtos, ingredientDtos, totals);
        }

        private RecipeSummaryDto BuildRecipeSummaryDto(
            int recipeId,
            RecipeDetailRequest request,
            string? previewImageBlobName,
            (int[] DietIds, RecipeIngredientRequest[] IngredientRequests, string[] BadgeValues, List<DietDto> DietDtos, List<IngredientDto> IngredientDtos, RecipeCalculatedTotals Totals) recipeReferences)
        {
            var ingredientLookup = recipeReferences.IngredientDtos.ToDictionary(ingredient => ingredient.id);
            var orderedIngredients = recipeReferences.IngredientRequests
                .Select(ingredient => ingredientLookup[ingredient.IngredientId])
                .ToList();

            IReadOnlyList<DietDto>? orderedDiets = null;
            if (recipeReferences.DietDtos.Count > 0)
            {
                var dietLookup = recipeReferences.DietDtos.ToDictionary(diet => diet.id);
                orderedDiets = recipeReferences.DietIds.Select(dietId => dietLookup[dietId]).ToList();
            }

            return new RecipeSummaryDto(
                Id: recipeId,
                Name: request.Name,
                Description: request.Description!,
                PreviewImageUrl: ResolveBlobUrl(previewImageBlobName),
                PrepTimeMinutes: request.PrepTimeMinutes,
                CookTimeMinutes: request.CookTimeMinutes,
                TotalTimeMinutes: request.TotalTimeMinutes,
                Servings: request.Servings,
                Difficulty: request.Difficulty!.Value.ToString(),
                Badges: recipeReferences.BadgeValues.Length == 0 ? null : recipeReferences.BadgeValues,
                Diets: orderedDiets,
                EstimatedCostPerServing: recipeReferences.Totals.EstimatedCostPerServing,
                Ingredients: orderedIngredients,
                CaloriesTotal: recipeReferences.Totals.CaloriesTotal,
                ProteinTotal: recipeReferences.Totals.ProteinTotal,
                CarbsTotal: recipeReferences.Totals.CarbsTotal,
                CaloriesPerServing: recipeReferences.Totals.CaloriesPerServing,
                ProteinPerServing: recipeReferences.Totals.ProteinPerServing
            );
        }

        private async Task<string?> UploadRecipeImageAsync(RecipeDetailRequest request, CancellationToken ct)
        {
            if (request.Image is null || request.Image.Length == 0)
                return null;

            var blobName = $"recipes/{Guid.NewGuid():N}.avif";
            try
            {
                using var stream = request.Image.OpenReadStream();
                await _blobStorageService.UploadImageAsAvifAsync(stream, blobName, quality: 50, ct).ConfigureAwait(false);
                return blobName;
            }
            catch (ValidationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new InternalServerException("Failed to upload recipe image.", ex);
            }
        }

        private string? ResolveBlobUrl(string? blobName)
        {
            if (string.IsNullOrWhiteSpace(blobName))
                return null;

            try
            {
                return _blobStorageService.GetUri(blobName).ToString();
            }
            catch
            {
                return null;
            }
        }

        private async Task DeleteBlobIfExistsAsync(string? blobName, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(blobName))
                return;

            try
            {
                await _blobStorageService.DeleteAsync(blobName, ct).ConfigureAwait(false);
            }
            catch
            {
            }
        }
        private static string SerializeInstructions(IReadOnlyCollection<RecipeInstructionStepRequest> instructions) =>
            JsonSerializer.Serialize(instructions, InstructionJsonOptions);

        private static List<RecipeInstructionStepDto> DeserializeInstructions(string instructionsJson)
        {
            if (string.IsNullOrWhiteSpace(instructionsJson))
                return [];

            try
            {
                var steps = JsonSerializer.Deserialize<List<RecipeInstructionStepDto>>(
                    instructionsJson,
                    InstructionJsonOptions) ?? [];
                var normalizedSteps = steps
                    .Select(step => new RecipeInstructionStepDto(
                        step.Title?.Trim() ?? string.Empty,
                        step.Instruction?.Trim() ?? string.Empty))
                    .Where(step => step.Title.Length > 0 || step.Instruction.Length > 0)
                    .ToList();

                if (normalizedSteps.Count == 0 ||
                    normalizedSteps.Any(step => step.Title.Length == 0 || step.Instruction.Length == 0))
                {
                    throw new InternalServerException("Recipe instructions payload is invalid.");
                }

                return normalizedSteps;
            }
            catch (JsonException ex)
            {
                throw new InternalServerException("Recipe instructions payload is invalid.", ex);
            }
        }
    }
}

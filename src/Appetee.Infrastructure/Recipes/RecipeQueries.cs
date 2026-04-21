using Appetee.Application.Abstractions.Recipes;
using Appetee.Application.Dtos;
using Appetee.Application.Requests;
using Appetee.Application.RowData;
using Appetee.Application.utils;
using Appetee.Infrastructure.Data;

using Dapper;

namespace Appetee.Infrastructure.Recipes
{
    public sealed class RecipeQueries : IRecipeQueries
    {
        private readonly IBlobStorageService _blobStorageService;
        private readonly IDbConnectionFactory _db;

        public RecipeQueries(IDbConnectionFactory db, IBlobStorageService blobStorageService)
        {
            _db = db ?? throw new ValidationException(nameof(db));
            _blobStorageService = blobStorageService ?? throw new ValidationException(nameof(blobStorageService));
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
                            ImageBlobName = blobName,
                            request.Instructions,
                            request.PrepTimeMinutes,
                            request.Servings,
                            Difficulty = request.Difficulty!.Value.ToString(),
                            request.EstimatedCostPerServing,
                            request.CaloriesTotal,
                            request.ProteinTotal,
                            request.CarbsTotal
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
            string? currentImageBlobName;
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

                currentImageBlobName = existingRecipe.ImageBlobName;
            }

            var newBlobName = await UploadRecipeImageAsync(request, ct);
            var nextImageBlobName = newBlobName ?? currentImageBlobName;

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
                            ImageBlobName = nextImageBlobName,
                            request.Instructions,
                            request.PrepTimeMinutes,
                            request.Servings,
                            Difficulty = request.Difficulty!.Value.ToString(),
                            request.EstimatedCostPerServing,
                            request.CaloriesTotal,
                            request.ProteinTotal,
                            request.CarbsTotal
                        },
                        transaction: tran,
                        cancellationToken: ct
                    )
                );

                if (affected == 0)
                {
                    try { tran.Rollback(); } catch { }
                    await DeleteBlobIfExistsAsync(newBlobName, ct);
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

                if (!string.IsNullOrWhiteSpace(newBlobName)
                    && !string.IsNullOrWhiteSpace(currentImageBlobName)
                    && !string.Equals(newBlobName, currentImageBlobName, StringComparison.Ordinal))
                {
                    await DeleteBlobIfExistsAsync(currentImageBlobName, ct);
                }

                return BuildRecipeSummaryDto(id, request, nextImageBlobName, recipeReferences);
            }
            catch
            {
                try { tran.Rollback(); } catch { }
                await DeleteBlobIfExistsAsync(newBlobName, ct);
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
            var badges = (await grid.ReadAsync<string>()).AsList();
            var ingredientRows = (await grid.ReadAsync<RecipeIngredientDetailRowData>()).AsList();

            string? recipeImageUrl = null;
            if (!string.IsNullOrWhiteSpace(recipe.ImageBlobName))
            {
                try
                {
                    recipeImageUrl = _blobStorageService.GetUri(recipe.ImageBlobName).ToString();
                }
                catch
                {
                    recipeImageUrl = null;
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
                    Ingredient: new IngredientAdminDetailDto(
                        Id: row.Id,
                        Name: row.Name,
                        Basis: row.Basis,
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

            var instructions = recipe.Instructions
                .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(step => step.Trim())
                .Where(step => step.Length > 0)
                .ToList();

            return new RecipeDetailDto(
                Id: recipe.Id,
                Name: recipe.Name,
                ImageUrl: recipeImageUrl,
                PrepTimeMinutes: recipe.PrepTimeMinutes,
                Servings: recipe.Servings,
                Difficulty: recipe.Difficulty,
                Badges: badges.Count == 0 ? null : badges,
                Diets: diets.Count == 0 ? null : diets,
                EstimatedCostPerServing: recipe.EstimatedCostPerServing,
                Instructions: instructions,
                Ingredients: ingredients,
                CaloriesTotal: recipe.CaloriesTotal,
                ProteinTotal: recipe.ProteinTotal,
                CarbsTotal: recipe.CarbsTotal
            );
        }

        private async Task<(int[] DietIds, RecipeIngredientRequest[] IngredientRequests, string[] BadgeValues, List<DietDto> DietDtos, List<IngredientDto> IngredientDtos)> LoadRecipeReferenceDataAsync(
            System.Data.IDbConnection conn,
            System.Data.IDbTransaction tran,
            RecipeDetailRequest request,
            CancellationToken ct)
        {
            var dietIds = request.DietIds.Distinct().ToArray();
            var ingredientRequests = request.Ingredients.ToArray();
            var ingredientIds = ingredientRequests.Select(ingredient => ingredient.IngredientId).Distinct().ToArray();
            var badgeValues = request.Badges.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

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
            if (ingredientIds.Length > 0)
            {
                ingredientDtos = (await conn.QueryAsync<IngredientDto>(
                    new CommandDefinition(
                        IngredientSql.GetByIds,
                        new { Ids = ingredientIds },
                        transaction: tran,
                        cancellationToken: ct
                    ))).AsList();

                var foundIngredientIds = ingredientDtos.Select(ingredient => ingredient.id).ToHashSet();
                var missingIngredientIds = ingredientIds.Where(id => !foundIngredientIds.Contains(id)).ToArray();
                if (missingIngredientIds.Length > 0)
                    throw new ValidationException($"Invalid IngredientIds: {string.Join(", ", missingIngredientIds)}");
            }

            return (dietIds, ingredientRequests, badgeValues, dietDtos, ingredientDtos);
        }

        private RecipeSummaryDto BuildRecipeSummaryDto(
            int recipeId,
            RecipeDetailRequest request,
            string? imageBlobName,
            (int[] DietIds, RecipeIngredientRequest[] IngredientRequests, string[] BadgeValues, List<DietDto> DietDtos, List<IngredientDto> IngredientDtos) recipeReferences)
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
                ImageUrl: ResolveBlobUrl(imageBlobName),
                PrepTimeMinutes: request.PrepTimeMinutes,
                Servings: request.Servings,
                Difficulty: request.Difficulty!.Value.ToString(),
                Badges: recipeReferences.BadgeValues.Length == 0 ? null : recipeReferences.BadgeValues,
                Diets: orderedDiets,
                EstimatedCostPerServing: request.EstimatedCostPerServing,
                Ingredients: orderedIngredients,
                CaloriesTotal: request.CaloriesTotal,
                ProteinTotal: request.ProteinTotal,
                CarbsTotal: request.CarbsTotal
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
    }
}

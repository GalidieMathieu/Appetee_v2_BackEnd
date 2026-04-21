using Appetee.Application.Abstractions.Ingredients;
using Appetee.Application.Dtos;
using Appetee.Application.Requests;
using Appetee.Application.utils;
using Appetee.Application.RowData;
using Appetee.Infrastructure.Data;
using Dapper;

namespace Appetee.Infrastructure.Ingredients
{
    public sealed class IngredientQueries : IIngredientQueries
    {
        private readonly IBlobStorageService _blobStorageService;
        private readonly IDbConnectionFactory _db;
        public IngredientQueries(IDbConnectionFactory db, IBlobStorageService blobStorageService)
        {
            _db = db ?? throw new ValidationException(nameof(db));
            _blobStorageService = blobStorageService ?? throw new ValidationException(nameof(blobStorageService));
        }

        public async Task<IReadOnlyList<IngredientDto>> GetAllDiets(CancellationToken ct)
        {
            using var conn = await _db.CreateOpenConnectionAsync(ct);

            var rows = await conn.QueryAsync<IngredientDto>(
                    new CommandDefinition(IngredientSql.GetAll, cancellationToken: ct)
                );

            return rows.AsList();
        }

        public async Task<IngredientAdminDetailDto?> CreateIngredientWithDetailsAsync(IngredientAdminDetailRequest request, CancellationToken ct)
        {
            // Upload image first (if provided). Persist blob name to DB.
            string? imageUrl = null;
            string? blobName = null;

            if (request.Image is not null && request.Image.Length > 0)
            {
                blobName = $"ingredients/{Guid.NewGuid():N}.avif";
                try
                {
                    // Upload with cancellation support; don't hold DB transaction during network I/O.
                    using var s = request.Image.OpenReadStream();
                    await _blobStorageService.UploadImageAsAvifAsync(s, blobName, quality: 50, ct).ConfigureAwait(false);

                    // Resolve a public URL for immediate use in responses.
                    imageUrl = _blobStorageService.GetUri(blobName).ToString();
                }
                catch (Exception ex)
                {
                    // Surface a clear server error. Caller can map ApiException -> proper HTTP response.
                    throw new InternalServerException("Failed to upload ingredient image.", ex);
                }
            }

            using var conn = await _db.CreateOpenConnectionAsync(ct);
            // Use a transaction to ensure both inserts succeed or fail together
            using var tran = conn.BeginTransaction();
            try
            {
                var ingredientId = await conn.ExecuteScalarAsync<int>(
                    new CommandDefinition(
                        IngredientSql.CreateIngredient,
                        new { Name = request.Name, ImageBlobName = blobName },
                        transaction: tran,
                        cancellationToken: ct
                    )
                );

                await conn.ExecuteAsync(
                    new CommandDefinition(
                        IngredientSql.CreateIngredientDetails,
                        new
                        {
                            IngredientId = ingredientId,
                            Basis = request.Basis,
                            Price = request.Price,
                            CaloriesKcal = request.CaloriesKcal,
                            ProteinG = request.ProteinG,
                            FatG = request.FatG,
                            CarbsG = request.CarbsG,
                            SugarG = request.SugarG,
                            FiberG = request.FiberG,
                            SodiumMg = request.SodiumMg,
                            VitaminCMg = request.VitaminCMg,
                            IronMg = request.IronMg
                        },
                        transaction: tran,
                        cancellationToken: ct
                    )
                );

                tran.Commit();

                var dto = new IngredientAdminDetailDto(
                    ingredientId,
                    request.Name,
                    request.Basis,
                    request.CaloriesKcal,
                    request.Price,
                    imageUrl,
                    request.ProteinG,
                    request.FatG,
                    request.CarbsG,
                    request.SugarG,
                    request.FiberG,
                    request.SodiumMg,
                    request.VitaminCMg,
                    request.IronMg
                );

                return dto;
            }
            catch
            {
                try { tran.Rollback(); } catch { }

                // If we uploaded an image but DB work failed, attempt to clean up the uploaded blob.
                if (!string.IsNullOrWhiteSpace(blobName))
                {
                    try
                    {
                        await _blobStorageService.DeleteAsync(blobName, ct).ConfigureAwait(false);
                    }
                    catch
                    {
                        // Swallow deletion errors - original DB exception should be propagated.
                    }
                }

                throw;
            }
        }

        public async Task<IngredientAdminDetailDto?> GetIngredientWithDetailsByIdAsync(int id, CancellationToken ct)
        {
            using var conn = await _db.CreateOpenConnectionAsync(ct);

            // Read rowdata (includes blob name) then resolve URL via blob service
            var row = await conn.QueryFirstOrDefaultAsync<IngredientRowdataAdmin>(
                new CommandDefinition(
                    IngredientSql.GetWithDetailsById,
                    new { id },
                    cancellationToken: ct
                )
            );

            if (row is null) return null;

            string? imageUrl = null;
            if (!string.IsNullOrWhiteSpace(row.ImageBlobName))
            {
                try
                {
                    imageUrl = _blobStorageService.GetUri(row.ImageBlobName).ToString();
                }
                catch   
                {
                    // If resolving the URI fails, don't throw; return DTO without image.
                    imageUrl = null;
                }
            }

            var dto = new IngredientAdminDetailDto(
                row.Id,
                row.Name,
                row.Basis,
                row.CaloriesKcal,
                row.Price,
                imageUrl,
                row.ProteinG,
                row.FatG,
                row.CarbsG,
                row.SugarG,
                row.FiberG,
                row.SodiumMg,
                row.VitaminCMg,
                row.IronMg
            );

            return dto;
        }
    }
}

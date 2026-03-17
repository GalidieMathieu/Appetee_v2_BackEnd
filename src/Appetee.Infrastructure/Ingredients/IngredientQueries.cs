using Appetee.Application.Abstractions.Ingredients;
using Appetee.Application.Dtos;
using Appetee.Application.Requests;
using Appetee.Infrastructure.Data;
using Appetee.Infrastructure.Users;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Text;

namespace Appetee.Infrastructure.Ingredients
{
    public sealed class IngredientQueries : IIngredientQueries
    {
        private readonly IDbConnectionFactory _db;
        public IngredientQueries(IDbConnectionFactory db) => _db = db;

        public async Task<IReadOnlyList<IngredientDto>> GetAllDiets(CancellationToken ct)
        {
            using var conn = await _db.CreateOpenConnectionAsync(ct);

            var rows = await conn.QueryAsync<IngredientDto>(
                    new CommandDefinition(IngredientSql.GetAll, cancellationToken: ct)
                );

            return rows.AsList();
        }

        public async Task<ActionResult<IngredientAdminDetailDto>> CreateIngredientWithDetailsAsync(IngredientAdminDetailRequest request, CancellationToken ct)
        {
            using var conn = await _db.CreateOpenConnectionAsync(ct);
            // Use a transaction to ensure both inserts succeed or fail together
            using var tran = conn.BeginTransaction();
            try
            {
                var ingredientId = await conn.ExecuteScalarAsync<int>(
                    new CommandDefinition(
                        IngredientSql.CreateIngredient,
                        new { Name = request.Name },
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
                    request.ProteinG,
                    request.FatG,
                    request.CarbsG,
                    request.SugarG,
                    request.FiberG,
                    request.SodiumMg,
                    request.VitaminCMg,
                    request.IronMg
                );

                return new ActionResult<IngredientAdminDetailDto>(dto);
            }
            catch
            {
                try { tran.Rollback(); } catch { }
                throw;
            }
        }

        public async Task<IngredientAdminDetailDto?> GetIngredientWithDetailsByIdAsync(int id, CancellationToken ct)
        {
            using var conn = await _db.CreateOpenConnectionAsync(ct);

            var dto = await conn.QueryFirstOrDefaultAsync<IngredientAdminDetailDto>(
                new CommandDefinition(
                    IngredientSql.GetWithDetailsById,
                    new { id },
                    cancellationToken: ct
                )
            );

            return dto;
        }
    }
}

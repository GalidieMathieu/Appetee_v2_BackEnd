using Appetee.Application.Abstractions.Ingredients;
using Appetee.Application.Dtos;
using Appetee.Infrastructure.Data;
using Appetee.Infrastructure.Users;
using Dapper;
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
    }
}

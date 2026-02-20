using Appetee.Application.Abstractions.Diets;
using Appetee.Application.Dtos;
using Appetee.Infrastructure.Data;
using Appetee.Infrastructure.Users;
using Dapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace Appetee.Infrastructure.Diets
{
    public sealed class DietQueries : IDietQueries
    {
        private readonly IDbConnectionFactory _db;
        public DietQueries(IDbConnectionFactory db) => _db = db;

        public async Task<IReadOnlyList<DietDto>> GetAllDiets(CancellationToken ct)
        {
            using var conn = await _db.CreateOpenConnectionAsync(ct);

            var rows = await conn.QueryAsync<DietDto>(
                    new CommandDefinition(DietSql.GetAll, cancellationToken: ct)
                );

            return rows.AsList();
        }
    }
}

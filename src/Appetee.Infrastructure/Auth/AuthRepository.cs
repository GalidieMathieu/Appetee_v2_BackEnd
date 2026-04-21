using Appetee.Application.Abstractions.Auth;
using Appetee.Infrastructure.Data;
using Appetee.Infrastructure.Users;
using Dapper;
using MySqlConnector;
using System.Data.Common;
using Appetee.Application.utils;

namespace Appetee.Infrastructure.Auth
{
    public sealed class AuthRepository : IAuthRepository
    {
        private readonly IDbConnectionFactory _db;

        public AuthRepository(IDbConnectionFactory db)
        {
            _db = db;
        }

        public async Task<int> CreateUserAsync(
           string username,
           string email,
           string passwordHash,
           IReadOnlyList<int> ?DietIds,
           IReadOnlyList<int> ?IngredientRestrictionIds,
           CancellationToken ct)
        {

            // Normalize (null => empty), remove invalid/duplicates
            var dietIds = (DietIds ?? Array.Empty<int>())
                .Where(x => x > 0)
                .Distinct()
                .ToArray();

            var ingredientIds = (IngredientRestrictionIds ?? Array.Empty<int>())
                .Where(x => x > 0)
                .Distinct()
                .ToArray();

            using var conn = await _db.CreateOpenConnectionAsync(ct);

            // Needs DbConnection for BeginTransactionAsync
            var dbConn = (DbConnection)conn;

            await using var tx = await dbConn.BeginTransactionAsync(ct);

            try
            {
                // 1) Insert user
                await dbConn.ExecuteAsync(new CommandDefinition(
                    AuthSql.InsertUser,
                    new
                    {
                        Username = username,
                        Email = email,
                        PasswordHash = passwordHash,
                    },
                    transaction: tx,
                    cancellationToken: ct
                ));

                // 2) Get generated user_id (same connection/tx)
                var userId = await dbConn.QuerySingleAsync<int>(new CommandDefinition(
                    AuthSql.LastInsertId,
                    transaction: tx,
                    cancellationToken: ct
                ));

                if(userId <= 0)
                {
                    throw new InternalServerException("Invalid user, creation failed");
                }

                //3) Cleaning dietUSer and ingredient USer
                // 3.1) Validate diet IDs and ingredient (only if any)
                if (dietIds.Length > 0)
                {
                    var existingDietIds = (await conn.QueryAsync<int>(
                        new CommandDefinition(
                            DietSql.GetSomeByIds,
                            new { Ids = dietIds },
                            transaction: tx,
                            cancellationToken: ct
                        ))).ToHashSet();

                    var missing = dietIds.Where(id => !existingDietIds.Contains(id)).ToArray();
                    if (missing.Length > 0)
                        throw new ValidationException($"Invalid DietIds: {string.Join(", ", missing)}");
                }

                // 3.2) Validate ingredient IDs (only if any)
                if (ingredientIds.Length > 0)
                {
                    var existingIngredientIds = (await conn.QueryAsync<int>(
                        new CommandDefinition(
                            IngredientSql.GetSomeByIds,
                            new { Ids = ingredientIds },
                            transaction: tx,
                            cancellationToken: ct
                        ))).ToHashSet();

                    var missing = ingredientIds.Where(id => !existingIngredientIds.Contains(id)).ToArray();
                    if (missing.Length > 0)
                        throw new ValidationException($"Invalid IngredientRestrictionIds: {string.Join(", ", missing)}");
                }

                //3.3 Cleaning diets : 
                await conn.ExecuteAsync(new CommandDefinition(
                    UserSql.DeleteDietFromUserById,
                    new { id = userId },
                    transaction: tx,
                    cancellationToken: ct
                ));


                // 4) Update both diet and user : 
                if (dietIds.Length > 0)
                {
                    var (sql, p) = BulkInsertSql.BuildBulkInsertUserDiets(userId, dietIds);
                    await conn.ExecuteAsync(new CommandDefinition(
                        sql, p, transaction: tx, cancellationToken: ct
                        ));
                }

                // 5 cleaning ingredients : 
                await conn.ExecuteAsync(new CommandDefinition(UserSql.DeleteIngredientUserByID,
                    new { id = userId },
                    transaction: tx,
                    cancellationToken: ct));

                if(ingredientIds.Length > 0)
                {
                    var (sql, p) = BulkInsertSql.BuildBulkInsertUserIngredients(userId, ingredientIds);
                    await conn.ExecuteAsync(new CommandDefinition(
                        sql, p, transaction: tx, cancellationToken: ct
                        ));
                }
                

                await tx.CommitAsync(ct);
                return userId;
            }
            catch (MySqlException ex) when (ex.Number == 1062)
            {
                //this should not happened as we checking for the user email in the account step.
                await tx.RollbackAsync(ct);
                throw new ConflictException("Username or email already exists.", ex);
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        }

    }
}

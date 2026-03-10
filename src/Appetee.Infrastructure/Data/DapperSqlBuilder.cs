using Dapper;
using System.Data;
namespace Appetee.Infrastructure.Data
{
    /// <summary>
    /// All Bulk Insert will be there. 
    /// build SQL at runtime using a builder that returns (valuesSql, parameters)
    /// </summary>
    public static class BulkInsertSql
    {
        
        public static (string Sql, DynamicParameters Params) BuildBulkInsertUserIngredients(
                int userId,
                int[] ingredientIds
            )
        {
            // Example schema assumption:
            // user_ingredients(user_id, ingredient_id)
            var p = new DynamicParameters();
            p.Add("u", userId, DbType.Int32);

            var values = new List<string>(ingredientIds.Length);
            for (var i = 0; i < ingredientIds.Length; i++)
            {
                p.Add($"ing{i}", ingredientIds[i], DbType.Int32);
                values.Add($"(@u, @ing{i})");
            }

            var sql = $"""
                INSERT INTO user_ingredient_restrictions (user_id, ingredient_id)
                VALUES {string.Join(", ", values)};
                """;

            return (sql, p);
        }

        public static (string Sql, DynamicParameters Params) BuildBulkInsertUserDiets(
        int userId,
        int[] dietIds,
        bool hasAllergy
           )
        {
            // INSERT INTO user_diets (user_id, diet_id, has_allergy) VALUES (@u, @d0, @a0), (@u, @d1, @a1), ...
            var p = new DynamicParameters();
            p.Add("u", userId, DbType.Int32);

            var values = new List<string>(dietIds.Length);
            for (var i = 0; i < dietIds.Length; i++)
            {
                p.Add($"d{i}", dietIds[i], DbType.Int32);
                p.Add($"a{i}", hasAllergy ? 1 : 0, DbType.Boolean);

                values.Add($"(@u, @d{i}, @a{i})");
            }

            var sql = $"""
                INSERT INTO user_diets (user_id, diet_id, has_allergy)
                VALUES {string.Join(", ", values)};
                """;

            return (sql, p);
        }
    }
}

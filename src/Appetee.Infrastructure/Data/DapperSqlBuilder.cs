using Appetee.Application.Requests;
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
            int[] dietIds
        )
        {
            var p = new DynamicParameters();
            p.Add("u", userId, DbType.Int32);

            var values = new List<string>(dietIds.Length);
            for (var i = 0; i < dietIds.Length; i++)
            {
                p.Add($"d{i}", dietIds[i], DbType.Int32);
                values.Add($"(@u, @d{i})");
            }

            var sql = $"""
                INSERT INTO user_diets (user_id, diet_id)
                VALUES {string.Join(", ", values)};
                """;

            return (sql, p);
        }

        public static (string Sql, DynamicParameters Params) BuildBulkInsertRecipeDiets(
            int recipeId,
            int[] dietIds
        )
        {
            var p = new DynamicParameters();
            p.Add("r", recipeId, DbType.Int32);

            var values = new List<string>(dietIds.Length);
            for (var i = 0; i < dietIds.Length; i++)
            {
                p.Add($"d{i}", dietIds[i], DbType.Int32);
                values.Add($"(@r, @d{i})");
            }

            var sql = $"""
                INSERT INTO diet_recipes (recipe_id, diet_id)
                VALUES {string.Join(", ", values)};
                """;

            return (sql, p);
        }

        public static (string Sql, DynamicParameters Params) BuildBulkInsertRecipeBadges(
            int recipeId,
            string[] badges
        )
        {
            var p = new DynamicParameters();
            p.Add("r", recipeId, DbType.Int32);

            var values = new List<string>(badges.Length);
            for (var i = 0; i < badges.Length; i++)
            {
                p.Add($"b{i}", badges[i], DbType.String);
                values.Add($"(@r, @b{i})");
            }

            var sql = $"""
                INSERT INTO recipe_badges (recipe_id, badge)
                VALUES {string.Join(", ", values)};
                """;

            return (sql, p);
        }

        public static (string Sql, DynamicParameters Params) BuildBulkInsertRecipeIngredients(
            int recipeId,
            RecipeIngredientRequest[] ingredients
        )
        {
            var p = new DynamicParameters();
            p.Add("r", recipeId, DbType.Int32);

            var values = new List<string>(ingredients.Length);
            for (var i = 0; i < ingredients.Length; i++)
            {
                var ingredient = ingredients[i];
                p.Add($"ing{i}", ingredient.IngredientId, DbType.Int32);
                p.Add($"q{i}", ingredient.Quantity, DbType.Decimal);
                p.Add($"u{i}", ingredient.Unit, DbType.String);
                values.Add($"(@r, @ing{i}, @q{i}, @u{i})");
            }

            var sql = $"""
                INSERT INTO recipe_ingredients (recipe_id, ingredient_id, quantity, unit)
                VALUES {string.Join(", ", values)};
                """;

            return (sql, p);
        }
    }
}

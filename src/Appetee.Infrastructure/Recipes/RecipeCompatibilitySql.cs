// Purpose: Owns the reusable SQL predicate for authenticated recipe compatibility.
// Created: 2026-08-24T22:52:54-06:00
// Last updated: 2026-08-26T18:24:14-06:00

/// <summary>Provides the mandatory current-user diet and ingredient-restriction SQL predicate.</summary>
internal static class RecipeCompatibilitySql
{
    internal const string Predicate = """
        NOT EXISTS (
            SELECT 1
            FROM user_diets ud
            WHERE ud.user_id = @CurrentUserId
              AND NOT EXISTS (
                  SELECT 1
                  FROM diet_recipes dr
                  WHERE dr.recipe_id = r.id
                    AND dr.diet_id = ud.diet_id
              )
        )
        AND NOT EXISTS (
            SELECT 1
            FROM recipe_ingredients ri_restriction
            INNER JOIN user_ingredient_restrictions uir
                ON uir.user_id = @CurrentUserId
               AND uir.ingredient_id = ri_restriction.ingredient_id
            WHERE ri_restriction.recipe_id = r.id
        )
    """;
}

internal static class RecipeSql
{
    internal const string CreateRecipe = """
        INSERT INTO recipes (
            name,
            image_blob_name,
            instructions,
            prep_time_minutes,
            servings,
            difficulty,
            estimated_cost_per_serving,
            calories_total,
            protein_total,
            carbs_total
        )
        VALUES (
            @Name,
            @ImageBlobName,
            @Instructions,
            @PrepTimeMinutes,
            @Servings,
            @Difficulty,
            @EstimatedCostPerServing,
            @CaloriesTotal,
            @ProteinTotal,
            @CarbsTotal
        );
        SELECT LAST_INSERT_ID();
    """;

    internal const string GetImageBlobById = """
        SELECT
            id              AS Id,
            image_blob_name AS ImageBlobName
        FROM recipes
        WHERE id = @id
        LIMIT 1;
    """;

    internal const string UpdateRecipe = """
        UPDATE recipes
        SET
            name = @Name,
            image_blob_name = @ImageBlobName,
            instructions = @Instructions,
            prep_time_minutes = @PrepTimeMinutes,
            servings = @Servings,
            difficulty = @Difficulty,
            estimated_cost_per_serving = @EstimatedCostPerServing,
            calories_total = @CaloriesTotal,
            protein_total = @ProteinTotal,
            carbs_total = @CarbsTotal
        WHERE id = @Id;
    """;

    internal const string DeleteRecipeDietsByRecipeId = """
        DELETE FROM diet_recipes
        WHERE recipe_id = @id;
    """;

    internal const string DeleteRecipeBadgesByRecipeId = """
        DELETE FROM recipe_badges
        WHERE recipe_id = @id;
    """;

    internal const string DeleteRecipeIngredientsByRecipeId = """
        DELETE FROM recipe_ingredients
        WHERE recipe_id = @id;
    """;

    internal const string GetWithDetailsById = """
        SELECT
            r.id                         AS Id,
            r.name                       AS Name,
            r.image_blob_name            AS ImageBlobName,
            r.instructions               AS Instructions,
            r.prep_time_minutes          AS PrepTimeMinutes,
            r.servings                   AS Servings,
            r.difficulty                 AS Difficulty,
            r.estimated_cost_per_serving AS EstimatedCostPerServing,
            r.calories_total             AS CaloriesTotal,
            r.protein_total              AS ProteinTotal,
            r.carbs_total                AS CarbsTotal
        FROM recipes r
        WHERE r.id = @id
        LIMIT 1;

        SELECT
            d.id   AS id,
            d.name AS name
        FROM diets d
        INNER JOIN diet_recipes dr ON dr.diet_id = d.id
        WHERE dr.recipe_id = @id
        ORDER BY d.id;

        SELECT
            rb.badge
        FROM recipe_badges rb
        WHERE rb.recipe_id = @id
        ORDER BY rb.badge;

        SELECT
            ri.ingredient_id   AS IngredientId,
            ri.quantity        AS Quantity,
            ri.unit            AS Unit,
            i.id               AS Id,
            i.name             AS Name,
            n.basis            AS Basis,
            n.calories_kcal    AS CaloriesKcal,
            n.price            AS Price,
            i.image_blob_name  AS ImageBlobName,
            n.protein_g        AS ProteinG,
            n.fat_g            AS FatG,
            n.carbs_g          AS CarbsG,
            n.sugar_g          AS SugarG,
            n.fiber_g          AS FiberG,
            n.sodium_mg        AS SodiumMg,
            n.vitamin_c_mg     AS VitaminCMg,
            n.iron_mg          AS IronMg
        FROM recipe_ingredients ri
        INNER JOIN ingredients i ON i.id = ri.ingredient_id
        LEFT JOIN ingredient_nutrition n ON n.ingredient_id = i.id
        WHERE ri.recipe_id = @id
        ORDER BY i.id;
    """;
}

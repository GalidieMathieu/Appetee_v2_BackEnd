internal static class RecipeSql
{
    internal static readonly string DiscoverCandidates = $"""
        WITH ranked_recipes AS (
            SELECT
                r.id                         AS Id,
                r.name                       AS Name,
                COALESCE(r.card_image_blob_name, r.image_blob_name) AS CardImageBlobName,
                r.total_time_minutes         AS TotalTimeMinutes,
                r.calories_per_serving       AS CaloriesPerServing,
                r.estimated_cost_per_serving AS EstimatedCostPerServing,
                CAST(
                    EXISTS (
                        SELECT 1
                        FROM favorite_recipes fr
                        WHERE fr.user_id = @CurrentUserId
                          AND fr.recipe_id = r.id
                    )
                    AS SIGNED
                ) AS IsSaved,
                CAST(
                    CRC32(CONCAT(@BrowseSeed, ':', r.id))
                    AS SIGNED
                ) AS SortRank
            FROM recipes r
            WHERE {RecipeCompatibilitySql.Predicate}
        )
        SELECT
            ranked.Id,
            ranked.Name,
            ranked.CardImageBlobName,
            ranked.TotalTimeMinutes,
            ranked.CaloriesPerServing,
            ranked.EstimatedCostPerServing,
            ranked.IsSaved,
            ranked.SortRank
        FROM ranked_recipes ranked
        WHERE @HasCursor = 0
           OR ranked.SortRank > @CursorRank
           OR (
               ranked.SortRank = @CursorRank
               AND ranked.Id > @CursorId
           )
        ORDER BY ranked.SortRank ASC, ranked.Id ASC
        LIMIT @TakePlusOne;
    """;

    internal const string HydrateDiscoveryCards = """
        SELECT
            rb.recipe_id AS RecipeId,
            rb.badge     AS Badge
        FROM recipe_badges rb
        WHERE rb.recipe_id IN @RecipeIds
        ORDER BY rb.recipe_id, rb.badge;

        SELECT
            ri.recipe_id     AS RecipeId,
            i.id             AS Id,
            i.name           AS Name,
            ri.featured_order AS FeaturedOrder
        FROM recipe_ingredients ri
        INNER JOIN ingredients i ON i.id = ri.ingredient_id
        WHERE ri.recipe_id IN @RecipeIds
          AND ri.featured_order IS NOT NULL
        ORDER BY ri.recipe_id, ri.featured_order;
    """;

    internal const string CreateRecipe = """
        INSERT INTO recipes (
            name,
            description,
            image_blob_name,
            card_image_blob_name,
            instructions,
            prep_time_minutes,
            cook_time_minutes,
            total_time_minutes,
            servings,
            difficulty,
            estimated_cost_per_serving,
            calories_total,
            protein_total,
            carbs_total,
            calories_per_serving,
            protein_per_serving
        )
        VALUES (
            @Name,
            @Description,
            @PreviewImageBlobName,
            NULL,
            @InstructionsJson,
            @PrepTimeMinutes,
            @CookTimeMinutes,
            @TotalTimeMinutes,
            @Servings,
            @Difficulty,
            @EstimatedCostPerServing,
            @CaloriesTotal,
            @ProteinTotal,
            @CarbsTotal,
            @CaloriesPerServing,
            @ProteinPerServing
        );
        SELECT LAST_INSERT_ID();
    """;

    internal const string GetImageBlobById = """
        SELECT
            id              AS Id,
            image_blob_name AS PreviewImageBlobName
        FROM recipes
        WHERE id = @id
        LIMIT 1;
    """;

    internal const string GetIngredientCalculationDataByIds = """
        SELECT
            i.id              AS Id,
            i.name            AS Name,
            n.basis           AS Basis,
            n.basis_unit      AS BasisUnit,
            n.price           AS Price,
            n.calories_kcal   AS CaloriesKcal,
            n.protein_g       AS ProteinG,
            n.carbs_g         AS CarbsG
        FROM ingredients i
        LEFT JOIN ingredient_nutrition n ON n.ingredient_id = i.id
        WHERE i.id IN @Ids;
    """;

    internal const string UpdateRecipe = """
        UPDATE recipes
        SET
            name = @Name,
            description = @Description,
            image_blob_name = @PreviewImageBlobName,
            card_image_blob_name = CASE WHEN @ClearCardImage THEN NULL ELSE card_image_blob_name END,
            instructions = @InstructionsJson,
            prep_time_minutes = @PrepTimeMinutes,
            cook_time_minutes = @CookTimeMinutes,
            total_time_minutes = @TotalTimeMinutes,
            servings = @Servings,
            difficulty = @Difficulty,
            estimated_cost_per_serving = @EstimatedCostPerServing,
            calories_total = @CaloriesTotal,
            protein_total = @ProteinTotal,
            carbs_total = @CarbsTotal,
            calories_per_serving = @CaloriesPerServing,
            protein_per_serving = @ProteinPerServing
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
            r.description                AS Description,
            r.image_blob_name            AS PreviewImageBlobName,
            r.instructions               AS Instructions,
            r.prep_time_minutes          AS PrepTimeMinutes,
            r.cook_time_minutes          AS CookTimeMinutes,
            r.total_time_minutes         AS TotalTimeMinutes,
            r.servings                   AS Servings,
            r.difficulty                 AS Difficulty,
            r.estimated_cost_per_serving AS EstimatedCostPerServing,
            r.calories_total             AS CaloriesTotal,
            r.protein_total              AS ProteinTotal,
            r.carbs_total                AS CarbsTotal,
            r.calories_per_serving       AS CaloriesPerServing,
            r.protein_per_serving        AS ProteinPerServing
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
            ri.display_order   AS DisplayOrder,
            ri.featured_order  AS FeaturedOrder,
            i.id               AS Id,
            i.name             AS Name,
            n.basis            AS Basis,
            n.basis_unit       AS BasisUnit,
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
        ORDER BY ri.display_order;
    """;
}

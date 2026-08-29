// Purpose: Owns all SQL text for ingredient catalogue, autocomplete, detail, and write persistence.
// Change reason: Add the F-008 Phase 9 bounded case-insensitive ingredient-name search statement.
// Created: Existing file; original timestamp was not recorded.
// Last updated: 2026-08-27T13:16:15-06:00

internal static class IngredientSql
{
    internal const string GetById = """
        SELECT
            id            AS id,
            name          AS name
        FROM ingredients
        WHERE id = @id
        LIMIT 1;
    """;

    internal const string GetAll = """
        SELECT id as id, name as name from ingredients
        ORDER BY id DESC;
        """;

    internal const string SearchByName = """
        SELECT
            id   AS id,
            name AS name
        FROM ingredients
        WHERE name LIKE @SearchContains ESCAPE '\\'
        ORDER BY
            CASE
                WHEN name = @SearchExact THEN 0
                WHEN name LIKE @SearchStarts ESCAPE '\\' THEN 1
                ELSE 2
            END,
            name ASC,
            id ASC
        LIMIT @Take;
    """;

    internal const string GetSomeByIds = """
        SELECT id FROM ingredients WHERE id IN @Ids;
        """;

    internal const string GetByIds = """
        SELECT
            id AS id,
            name AS name
        FROM ingredients
        WHERE id IN @Ids;
        """;

    internal const string deleteAll = """DELETE FROM ingredients WHERE id = @id""";

    internal const string CreateIngredient = """
        INSERT INTO ingredients (name, image_blob_name)
        VALUES (@Name, @ImageBlobName);
        SELECT LAST_INSERT_ID();
    """;

    internal const string CreateIngredientDetails = """
        INSERT INTO ingredient_nutrition (
            ingredient_id,
            basis,
            basis_unit,
            calories_kcal,
            price,
            protein_g,
            fat_g,
            carbs_g,
            sugar_g,
            fiber_g,
            sodium_mg,
            vitamin_c_mg,
            iron_mg
        ) VALUES (
            @IngredientId,
            @Basis,
            @BasisUnit,
            @CaloriesKcal,
            @Price,
            @ProteinG,
            @FatG,
            @CarbsG,
            @SugarG,
            @FiberG,
            @SodiumMg,
            @VitaminCMg,
            @IronMg
        );
    """;

    internal const string GetWithDetailsById = """
        SELECT
            i.id            AS Id,
            i.name          AS Name,
            d.basis         AS Basis,
            d.basis_unit    AS BasisUnit,
            d.calories_kcal AS CaloriesKcal,
            d.price         AS Price,
            i.image_blob_name AS ImageBlobName,
            d.protein_g     AS ProteinG,
            d.fat_g         AS FatG,
            d.carbs_g       AS CarbsG,
            d.sugar_g       AS SugarG,
            d.fiber_g       AS FiberG,
            d.sodium_mg     AS SodiumMg,
            d.vitamin_c_mg  AS VitaminCMg,
            d.iron_mg       AS IronMg
        FROM ingredients i
        LEFT JOIN ingredient_nutrition d ON d.ingredient_id = i.id
        WHERE i.id = @id
        LIMIT 1;
    """;
}

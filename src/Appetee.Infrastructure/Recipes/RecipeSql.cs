// Purpose: Owns all SQL text used by recipe discovery, favorites, hydration, details, and writes.
// Change reason: Add the SQL-owned three-result F-008 Phase 12 Quick Preview command.
// Created: Existing file; original timestamp was not recorded.
// Last updated: 2026-08-28T11:50:10-06:00

using System.Text;

internal static class RecipeSql
{
    private const string BrowseSortExpression =
        "CRC32(CONCAT(@BrowseSeed, ':', r.id))";

    private const string BrowseContinuationPredicate = """
        @HasCursor = 0
        OR ranked.SortRank > @CursorRank
        OR (
            ranked.SortRank = @CursorRank
            AND ranked.Id > @CursorId
        )
    """;

    private const string SearchContinuationPredicate = """
        @HasCursor = 0
        OR ranked.SortRank < @CursorRank
        OR (
            ranked.SortRank = @CursorRank
            AND ranked.Id < @CursorId
        )
    """;

    private const string SavedOnlyPredicate = """
        AND EXISTS (
            SELECT 1
            FROM favorite_recipes fr_saved
            WHERE fr_saved.user_id = @CurrentUserId
              AND fr_saved.recipe_id = r.id
        )
    """;

    private const string BadgeAndPredicate = """
        AND (
            SELECT COUNT(DISTINCT rb_filter.badge)
            FROM recipe_badges rb_filter
            WHERE rb_filter.recipe_id = r.id
              AND rb_filter.badge IN @Badges
        ) = @BadgeCount
    """;

    private const string IngredientAnyPredicate = """
        AND EXISTS (
            SELECT 1
            FROM recipe_ingredients ri_filter
            WHERE ri_filter.recipe_id = r.id
              AND ri_filter.ingredient_id IN @IngredientIds
        )
    """;

    private const string IngredientAllPredicate = """
        AND (
            SELECT COUNT(DISTINCT ri_filter.ingredient_id)
            FROM recipe_ingredients ri_filter
            WHERE ri_filter.recipe_id = r.id
              AND ri_filter.ingredient_id IN @IngredientIds
        ) = @IngredientCount
    """;

    private const string MaxTotalMinutesPredicate = """
        AND r.total_time_minutes <= @MaxTotalMinutes
    """;

    private const string AllowedDifficultiesPredicate = """
        AND r.difficulty IN @AllowedDifficulties
    """;

    internal static readonly string IsCompatibleFavoriteCandidate = $"""
        SELECT EXISTS (
            SELECT 1
            FROM recipes r
            WHERE r.id = @RecipeId
              AND {RecipeCompatibilitySql.Predicate}
        );
    """;

    internal const string EnsureFavorite = """
        INSERT INTO favorite_recipes (user_id, recipe_id)
        VALUES (@CurrentUserId, @RecipeId)
        ON DUPLICATE KEY UPDATE recipe_id = @RecipeId;
    """;

    internal const string RemoveFavorite = """
        DELETE FROM favorite_recipes
        WHERE user_id = @CurrentUserId
          AND recipe_id = @RecipeId;
    """;

    internal static readonly string GetCompatiblePreview = $"""
        SELECT
            r.id                         AS Id,
            r.name                       AS Name,
            r.description                AS Description,
            r.image_blob_name            AS PreviewImageBlobName,
            r.total_time_minutes         AS TotalTimeMinutes,
            r.calories_per_serving       AS CaloriesPerServing,
            r.protein_per_serving        AS ProteinPerServing,
            r.estimated_cost_per_serving AS EstimatedCostPerServing,
            CAST(
                EXISTS (
                    SELECT 1
                    FROM favorite_recipes fr_preview
                    WHERE fr_preview.user_id = @CurrentUserId
                      AND fr_preview.recipe_id = r.id
                )
                AS SIGNED
            ) AS IsSaved
        FROM recipes r
        WHERE r.id = @RecipeId
          AND {RecipeCompatibilitySql.Predicate}
        LIMIT 1;

        SELECT rb.badge
        FROM recipe_badges rb
        WHERE rb.recipe_id = @RecipeId;

        SELECT
            i.id   AS Id,
            i.name AS Name
        FROM recipe_ingredients ri
        INNER JOIN ingredients i ON i.id = ri.ingredient_id
        WHERE ri.recipe_id = @RecipeId
        ORDER BY ri.display_order;
    """;

    /// <summary>Composes one bounded candidate query from approved discovery modes and optional predicates.</summary>
    internal static string BuildDiscoveryCandidates(
        bool isSearch,
        int searchTermCount,
        bool savedOnly,
        bool hasBadgeFilter = false,
        bool hasMaxTotalMinutesFilter = false,
        bool hasDifficultyFilter = false,
        bool hasIngredientFilter = false,
        bool requireAllIngredients = true)
    {
        if (isSearch && searchTermCount is < 1 or > 4)
            throw new ArgumentOutOfRangeException(nameof(searchTermCount));

        if (!isSearch && searchTermCount != 0)
            throw new ArgumentOutOfRangeException(nameof(searchTermCount));

        var optionalPredicates = new StringBuilder();
        if (isSearch)
            optionalPredicates.Append(BuildDiscoverySearchPredicate(searchTermCount));

        if (savedOnly)
            optionalPredicates.AppendLine(SavedOnlyPredicate);

        if (hasBadgeFilter)
            optionalPredicates.AppendLine(BadgeAndPredicate);

        if (hasMaxTotalMinutesFilter)
            optionalPredicates.AppendLine(MaxTotalMinutesPredicate);

        if (hasDifficultyFilter)
            optionalPredicates.AppendLine(AllowedDifficultiesPredicate);

        if (hasIngredientFilter)
        {
            optionalPredicates.AppendLine(
                requireAllIngredients
                    ? IngredientAllPredicate
                    : IngredientAnyPredicate);
        }

        var sortExpression = isSearch
            ? BuildDiscoverySearchScore(searchTermCount)
            : BrowseSortExpression;
        var continuationPredicate = isSearch
            ? SearchContinuationPredicate
            : BrowseContinuationPredicate;
        var orderBy = isSearch
            ? "ranked.SortRank DESC, ranked.Id DESC"
            : "ranked.SortRank ASC, ranked.Id ASC";

        return $"""
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
                        {sortExpression}
                        AS SIGNED
                ) AS SortRank
                FROM recipes r
                WHERE {RecipeCompatibilitySql.Predicate}
                {optionalPredicates}
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
            WHERE {continuationPredicate}
            ORDER BY {orderBy}
            LIMIT @TakePlusOne;
        """;
    }

    /// <summary>Requires every effective term to match either the recipe name or one ingredient name.</summary>
    private static string BuildDiscoverySearchPredicate(int termCount)
    {
        var sql = new StringBuilder();
        for (var index = 0; index < termCount; index++)
        {
            sql.AppendLine($$"""
                AND (
                    r.name LIKE @Term{{index}}Contains ESCAPE '\\'
                    OR EXISTS (
                        SELECT 1
                        FROM recipe_ingredients ri_search
                        INNER JOIN ingredients i_search ON i_search.id = ri_search.ingredient_id
                        WHERE ri_search.recipe_id = r.id
                          AND i_search.name LIKE @Term{{index}}Contains ESCAPE '\\'
                    )
                )
            """);
        }

        return sql.ToString();
    }

    /// <summary>Builds deterministic relevance scoring from parameter placeholders for bounded search terms.</summary>
    private static string BuildDiscoverySearchScore(int termCount)
    {
        var scoreParts = new List<string>
        {
            """
            CASE
                WHEN r.name = @SearchExact THEN 1000
                WHEN r.name LIKE @SearchStarts ESCAPE '\\' THEN 500
                ELSE 0
            END
            """,
        };

        for (var index = 0; index < termCount; index++)
        {
            scoreParts.Add($$"""
                CASE
                    WHEN r.name = @Term{{index}}Exact THEN 120
                    WHEN r.name LIKE @Term{{index}}Starts ESCAPE '\\' THEN 90
                    WHEN r.name LIKE @Term{{index}}Contains ESCAPE '\\' THEN 60
                    ELSE 0
                END
            """);
            scoreParts.Add($$"""
                CASE
                    WHEN EXISTS (
                        SELECT 1
                        FROM recipe_ingredients ri_score_exact
                        INNER JOIN ingredients i_score_exact ON i_score_exact.id = ri_score_exact.ingredient_id
                        WHERE ri_score_exact.recipe_id = r.id
                          AND i_score_exact.name = @Term{{index}}Exact
                    ) THEN 30
                    WHEN EXISTS (
                        SELECT 1
                        FROM recipe_ingredients ri_score_starts
                        INNER JOIN ingredients i_score_starts ON i_score_starts.id = ri_score_starts.ingredient_id
                        WHERE ri_score_starts.recipe_id = r.id
                          AND i_score_starts.name LIKE @Term{{index}}Starts ESCAPE '\\'
                    ) THEN 20
                    WHEN EXISTS (
                        SELECT 1
                        FROM recipe_ingredients ri_score_contains
                        INNER JOIN ingredients i_score_contains ON i_score_contains.id = ri_score_contains.ingredient_id
                        WHERE ri_score_contains.recipe_id = r.id
                          AND i_score_contains.name LIKE @Term{{index}}Contains ESCAPE '\\'
                    ) THEN 10
                    ELSE 0
                END
            """);
        }

        return string.Join($"{Environment.NewLine} + ", scoreParts);
    }

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

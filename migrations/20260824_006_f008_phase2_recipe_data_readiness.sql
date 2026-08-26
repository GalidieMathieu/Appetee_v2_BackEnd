DELIMITER $$

DROP PROCEDURE IF EXISTS migrate_f008_phase2_recipe_data_readiness$$

CREATE PROCEDURE migrate_f008_phase2_recipe_data_readiness()
BEGIN
    ALTER TABLE recipes
        ADD COLUMN description VARCHAR(500) NULL AFTER name,
        ADD COLUMN cook_time_minutes INT NULL AFTER prep_time_minutes,
        ADD COLUMN total_time_minutes INT NULL AFTER cook_time_minutes,
        ADD COLUMN calories_per_serving DECIMAL(10,2) NULL AFTER carbs_total,
        ADD COLUMN protein_per_serving DECIMAL(10,2) NULL AFTER calories_per_serving;

    UPDATE recipes
    SET
        description = name,
        cook_time_minutes = 0,
        total_time_minutes = GREATEST(prep_time_minutes, 1),
        calories_per_serving = ROUND(calories_total / servings, 2),
        protein_per_serving = ROUND(protein_total / servings, 2);

    ALTER TABLE recipe_ingredients
        ADD COLUMN display_order SMALLINT UNSIGNED NULL AFTER unit,
        ADD COLUMN featured_order TINYINT UNSIGNED NULL AFTER display_order;

    CREATE TEMPORARY TABLE f008_recipe_ingredient_order AS
    SELECT
        recipe_id,
        ingredient_id,
        ROW_NUMBER() OVER (
            PARTITION BY recipe_id
            ORDER BY ingredient_id
        ) AS display_order
    FROM recipe_ingredients;

    ALTER TABLE f008_recipe_ingredient_order
        ADD PRIMARY KEY (recipe_id, ingredient_id);

    UPDATE recipe_ingredients ri
    INNER JOIN f008_recipe_ingredient_order ordering
        ON ordering.recipe_id = ri.recipe_id
       AND ordering.ingredient_id = ri.ingredient_id
    SET
        ri.display_order = ordering.display_order,
        ri.featured_order = CASE
            WHEN ordering.display_order <= 3 THEN ordering.display_order
            ELSE NULL
        END;

    DROP TEMPORARY TABLE f008_recipe_ingredient_order;

    IF EXISTS (
        SELECT 1
        FROM recipes
        WHERE CHAR_LENGTH(TRIM(description)) = 0
           OR cook_time_minutes < 0
           OR total_time_minutes <= 0
           OR total_time_minutes < prep_time_minutes
           OR total_time_minutes < cook_time_minutes
           OR calories_per_serving < 0
           OR protein_per_serving < 0
        LIMIT 1
    ) THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'F-008 Phase 2 could not backfill valid recipe readiness fields';
    END IF;

    IF EXISTS (
        SELECT 1
        FROM recipe_ingredients
        GROUP BY recipe_id
        HAVING MIN(display_order) <> 1
           OR MAX(display_order) <> COUNT(*)
           OR COUNT(DISTINCT display_order) <> COUNT(*)
           OR COUNT(featured_order) NOT BETWEEN 1 AND 3
           OR COUNT(DISTINCT featured_order) <> COUNT(featured_order)
        LIMIT 1
    ) THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'F-008 Phase 2 could not backfill valid ingredient order fields';
    END IF;

    ALTER TABLE recipes
        MODIFY description VARCHAR(500) NOT NULL,
        MODIFY cook_time_minutes INT NOT NULL,
        MODIFY total_time_minutes INT NOT NULL,
        MODIFY calories_per_serving DECIMAL(10,2) NOT NULL,
        MODIFY protein_per_serving DECIMAL(10,2) NOT NULL,
        DROP CHECK chk_recipes_prep_time,
        ADD CONSTRAINT chk_recipes_prep_time CHECK (prep_time_minutes >= 0),
        ADD CONSTRAINT chk_recipes_description CHECK (CHAR_LENGTH(TRIM(description)) > 0),
        ADD CONSTRAINT chk_recipes_cook_time CHECK (cook_time_minutes >= 0),
        ADD CONSTRAINT chk_recipes_total_time CHECK (
            total_time_minutes > 0
            AND total_time_minutes >= prep_time_minutes
            AND total_time_minutes >= cook_time_minutes
        ),
        ADD CONSTRAINT chk_recipes_calories_per_serving CHECK (calories_per_serving >= 0),
        ADD CONSTRAINT chk_recipes_protein_per_serving CHECK (protein_per_serving >= 0);

    ALTER TABLE recipe_ingredients
        MODIFY display_order SMALLINT UNSIGNED NOT NULL,
        ADD UNIQUE KEY uq_recipe_ingredients_display_order (recipe_id, display_order),
        ADD UNIQUE KEY uq_recipe_ingredients_featured_order (recipe_id, featured_order),
        ADD CONSTRAINT chk_recipe_ingredients_display_order CHECK (display_order >= 1),
        ADD CONSTRAINT chk_recipe_ingredients_featured_order CHECK (
            featured_order IS NULL OR featured_order BETWEEN 1 AND 3
        );

    ALTER TABLE recipe_badges
        DROP CHECK chk_recipe_badges_badge;

    INSERT IGNORE INTO recipe_badges (recipe_id, badge)
    SELECT recipe_id, 'High Protein'
    FROM recipe_badges
    WHERE badge = 'high-protein';

    INSERT IGNORE INTO recipe_badges (recipe_id, badge)
    SELECT recipe_id, 'Freezer Friendly'
    FROM recipe_badges
    WHERE badge = 'freezer-friendly';

    INSERT IGNORE INTO recipe_badges (recipe_id, badge)
    SELECT recipe_id, 'Budget Friendly'
    FROM recipe_badges
    WHERE badge = 'budget-focused';

    DELETE FROM recipe_badges
    WHERE badge IN ('high-protein', 'freezer-friendly', 'budget-focused');

    ALTER TABLE recipe_badges
        ADD CONSTRAINT chk_recipe_badges_badge CHECK (
            badge IN (
                'High Protein',
                'Low Calorie',
                'Low Carb',
                'High Fiber',
                'Quick Meal',
                'Meal Prep',
                'Freezer Friendly',
                'Budget Friendly',
                'Few Ingredients'
            )
        );
END$$

CALL migrate_f008_phase2_recipe_data_readiness()$$
DROP PROCEDURE migrate_f008_phase2_recipe_data_readiness$$

DELIMITER ;

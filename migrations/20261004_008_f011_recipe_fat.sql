-- Purpose: Adds nullable canonical recipe fat and backfills only complete, valid ingredient inputs.
-- Change reason: Implement F-011 Meal Plan Phase B01 recipe nutrition readiness.
-- Recovery: This migration is forward-only and restartable after MySQL DDL implicit commits.

DELIMITER $$

DROP PROCEDURE IF EXISTS migrate_f011_recipe_fat$$

CREATE PROCEDURE migrate_f011_recipe_fat()
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = DATABASE()
          AND table_name = 'recipes'
          AND column_name = 'fat_total'
    ) THEN
        ALTER TABLE recipes
            ADD COLUMN fat_total DECIMAL(10,2) NULL DEFAULT NULL
            AFTER carbs_total;
    END IF;

    UPDATE recipes r
    INNER JOIN (
        SELECT
            ri.recipe_id,
            ROUND(SUM(n.fat_g * ri.quantity / n.basis), 2) AS fat_total
        FROM recipe_ingredients ri
        INNER JOIN ingredient_nutrition n
            ON n.ingredient_id = ri.ingredient_id
        WHERE n.basis > 0
          AND n.fat_g IS NOT NULL
          AND n.fat_g >= 0
          AND ri.quantity > 0
          AND CHAR_LENGTH(TRIM(n.basis_unit)) > 0
          AND ri.unit = n.basis_unit
        GROUP BY ri.recipe_id
    ) totals
        ON totals.recipe_id = r.id
    SET r.fat_total = totals.fat_total
    WHERE r.fat_total IS NULL
      AND EXISTS (
          SELECT 1
          FROM recipe_ingredients present
          WHERE present.recipe_id = r.id
      )
      AND NOT EXISTS (
          SELECT 1
          FROM recipe_ingredients invalid
          LEFT JOIN ingredient_nutrition invalid_nutrition
            ON invalid_nutrition.ingredient_id = invalid.ingredient_id
          WHERE invalid.recipe_id = r.id
            AND (
                invalid_nutrition.ingredient_id IS NULL
                OR invalid_nutrition.basis IS NULL
                OR invalid_nutrition.basis <= 0
                OR invalid_nutrition.fat_g IS NULL
                OR invalid_nutrition.fat_g < 0
                OR invalid.quantity IS NULL
                OR invalid.quantity <= 0
                OR invalid.unit IS NULL
                OR invalid_nutrition.basis_unit IS NULL
                OR CHAR_LENGTH(TRIM(invalid_nutrition.basis_unit)) = 0
                OR invalid.unit <> invalid_nutrition.basis_unit
            )
      );

    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.table_constraints
        WHERE constraint_schema = DATABASE()
          AND table_name = 'recipes'
          AND constraint_name = 'chk_recipes_fat_total'
          AND constraint_type = 'CHECK'
    ) THEN
        ALTER TABLE recipes
            ADD CONSTRAINT chk_recipes_fat_total
            CHECK (fat_total IS NULL OR fat_total >= 0);
    END IF;
END$$

CALL migrate_f011_recipe_fat()$$
DROP PROCEDURE migrate_f011_recipe_fat$$

DELIMITER ;

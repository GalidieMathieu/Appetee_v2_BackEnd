DELIMITER $$

DROP PROCEDURE IF EXISTS migrate_f007_phase2_mandatory_calculation_data$$

CREATE PROCEDURE migrate_f007_phase2_mandatory_calculation_data()
BEGIN
    IF EXISTS (
        SELECT 1
        FROM ingredient_nutrition
        WHERE calories_kcal IS NULL
           OR protein_g IS NULL
           OR carbs_g IS NULL
        LIMIT 1
    ) THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'F-007 Phase 2 requires null ingredient core data to be corrected or removed before migration';
    END IF;

    IF EXISTS (
        SELECT 1
        FROM recipe_ingredients ri
        LEFT JOIN ingredient_nutrition n ON n.ingredient_id = ri.ingredient_id
        WHERE n.ingredient_id IS NULL
           OR n.basis <= 0
           OR ri.quantity <= 0
           OR BINARY ri.unit <> BINARY n.basis_unit
        LIMIT 1
    ) THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'F-007 Phase 2 requires invalid recipe ingredient calculation relationships to be corrected before migration';
    END IF;

    UPDATE recipes r
    INNER JOIN (
        SELECT
            ri.recipe_id,
            ROUND(SUM(n.calories_kcal * (ri.quantity / n.basis)), 2) AS calories_total,
            ROUND(SUM(n.protein_g * (ri.quantity / n.basis)), 2) AS protein_total,
            ROUND(SUM(n.carbs_g * (ri.quantity / n.basis)), 2) AS carbs_total,
            ROUND(SUM(n.price * (ri.quantity / n.basis)) / r2.servings, 2) AS estimated_cost_per_serving
        FROM recipe_ingredients ri
        INNER JOIN ingredient_nutrition n ON n.ingredient_id = ri.ingredient_id
        INNER JOIN recipes r2 ON r2.id = ri.recipe_id
        GROUP BY ri.recipe_id, r2.servings
    ) totals ON totals.recipe_id = r.id
    SET
        r.calories_total = totals.calories_total,
        r.protein_total = totals.protein_total,
        r.carbs_total = totals.carbs_total,
        r.estimated_cost_per_serving = totals.estimated_cost_per_serving;

    IF EXISTS (
        SELECT 1
        FROM recipes
        WHERE estimated_cost_per_serving IS NULL
        LIMIT 1
    ) THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'F-007 Phase 2 could not calculate a cost for every existing recipe';
    END IF;

    ALTER TABLE ingredient_nutrition
        MODIFY calories_kcal DECIMAL(10,2) NOT NULL,
        MODIFY protein_g DECIMAL(10,2) NOT NULL,
        MODIFY carbs_g DECIMAL(10,2) NOT NULL;

    ALTER TABLE recipes
        MODIFY estimated_cost_per_serving DECIMAL(10,2) NOT NULL;
END$$

CALL migrate_f007_phase2_mandatory_calculation_data()$$
DROP PROCEDURE migrate_f007_phase2_mandatory_calculation_data$$

DELIMITER ;

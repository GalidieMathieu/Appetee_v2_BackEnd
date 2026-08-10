DELIMITER $$

DROP PROCEDURE IF EXISTS migrate_f007_phase4_relationship_cleanup$$

CREATE PROCEDURE migrate_f007_phase4_relationship_cleanup()
BEGIN
    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        RESIGNAL;
    END;

    IF EXISTS (
        SELECT 1
        FROM recipe_ingredients ri
        LEFT JOIN recipes r ON r.id = ri.recipe_id
        LEFT JOIN ingredients i ON i.id = ri.ingredient_id
        LEFT JOIN ingredient_nutrition n ON n.ingredient_id = ri.ingredient_id
        WHERE r.id IS NULL OR i.id IS NULL OR n.ingredient_id IS NULL
        LIMIT 1
    ) THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'F-007 Phase 4 found an invalid recipe ingredient reference';
    END IF;

    IF EXISTS (
        SELECT 1
        FROM diet_recipes dr
        LEFT JOIN recipes r ON r.id = dr.recipe_id
        LEFT JOIN diets d ON d.id = dr.diet_id
        WHERE r.id IS NULL OR d.id IS NULL
        LIMIT 1
    ) THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'F-007 Phase 4 found an invalid recipe diet reference';
    END IF;

    IF EXISTS (
        SELECT 1
        FROM recipe_ingredients
        GROUP BY recipe_id, ingredient_id
        HAVING COUNT(*) > 1
        LIMIT 1
    ) THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'F-007 Phase 4 found a duplicate recipe ingredient relationship';
    END IF;

    IF EXISTS (
        SELECT 1
        FROM recipes r
        LEFT JOIN recipe_ingredients ri ON ri.recipe_id = r.id
        WHERE ri.recipe_id IS NULL
        LIMIT 1
    ) THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'F-007 Phase 4 requires every recipe to have at least one ingredient';
    END IF;

    IF EXISTS (
        SELECT 1
        FROM recipe_ingredients ri
        INNER JOIN ingredient_nutrition n ON n.ingredient_id = ri.ingredient_id
        WHERE LOWER(TRIM(ri.unit)) <> LOWER(TRIM(n.basis_unit))
        LIMIT 1
    ) THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'F-007 Phase 4 requires semantic unit mismatches to be corrected before migration';
    END IF;

    START TRANSACTION;

    UPDATE recipe_ingredients ri
    INNER JOIN ingredient_nutrition n ON n.ingredient_id = ri.ingredient_id
    SET ri.unit = n.basis_unit
    WHERE BINARY ri.unit <> BINARY n.basis_unit;

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

    COMMIT;
END$$

CALL migrate_f007_phase4_relationship_cleanup()$$
DROP PROCEDURE migrate_f007_phase4_relationship_cleanup$$

DELIMITER ;

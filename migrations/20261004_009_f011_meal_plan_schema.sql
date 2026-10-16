-- Purpose: Adds the approved F-011 V1 nutrition profile fields and six-table Meal Plan schema.
-- Change reason: Implement F-011 Meal Plan Phase B02 relational integrity.
-- Recovery: Forward-only and restartable after MySQL DDL implicit commits; migration history prevents reapplication.

DELIMITER $$

DROP PROCEDURE IF EXISTS migrate_f011_meal_plan_schema$$

CREATE PROCEDURE migrate_f011_meal_plan_schema()
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = DATABASE() AND table_name = 'users' AND column_name = 'calculation_sex'
    ) THEN
        ALTER TABLE users
            ADD COLUMN calculation_sex VARCHAR(6) CHARACTER SET ascii COLLATE ascii_bin NULL DEFAULT NULL
            AFTER image_url;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = DATABASE() AND table_name = 'users' AND column_name = 'birth_year'
    ) THEN
        ALTER TABLE users
            ADD COLUMN birth_year SMALLINT NULL DEFAULT NULL
            AFTER calculation_sex;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = DATABASE() AND table_name = 'users' AND column_name = 'height_cm'
    ) THEN
        ALTER TABLE users
            ADD COLUMN height_cm DECIMAL(5,2) NULL DEFAULT NULL
            AFTER birth_year;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = DATABASE() AND table_name = 'users' AND column_name = 'weight_kg'
    ) THEN
        ALTER TABLE users
            ADD COLUMN weight_kg DECIMAL(5,2) NULL DEFAULT NULL
            AFTER height_cm;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.table_constraints
        WHERE constraint_schema = DATABASE()
          AND table_name = 'users'
          AND constraint_name = 'chk_users_nutrition_profile_complete'
          AND constraint_type = 'CHECK'
    ) THEN
        ALTER TABLE users
            ADD CONSTRAINT chk_users_nutrition_profile_complete
            CHECK (
                (calculation_sex IS NULL
                    AND birth_year IS NULL
                    AND height_cm IS NULL
                    AND weight_kg IS NULL)
                OR
                (calculation_sex IS NOT NULL
                    AND calculation_sex IN ('male', 'female')
                    AND birth_year IS NOT NULL
                    AND birth_year BETWEEN 1900 AND 9999
                    AND height_cm IS NOT NULL
                    AND height_cm BETWEEN 120 AND 230
                    AND weight_kg IS NOT NULL
                    AND weight_kg BETWEEN 35 AND 250)
            );
    END IF;

    CREATE TABLE IF NOT EXISTS nutrition_plans (
        id INT NOT NULL PRIMARY KEY AUTO_INCREMENT,
        user_id INT NOT NULL,
        name VARCHAR(80) NOT NULL,
        mode VARCHAR(12) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
        default_meals_per_day TINYINT UNSIGNED NOT NULL,
        goal VARCHAR(24) CHARACTER SET ascii COLLATE ascii_bin NULL DEFAULT NULL,
        activity_level VARCHAR(16) CHARACTER SET ascii COLLATE ascii_bin NULL DEFAULT NULL,
        calculation_sex VARCHAR(6) CHARACTER SET ascii COLLATE ascii_bin NULL DEFAULT NULL,
        birth_year SMALLINT NULL DEFAULT NULL,
        age_years TINYINT UNSIGNED NULL DEFAULT NULL,
        height_cm DECIMAL(5,2) NULL DEFAULT NULL,
        weight_kg DECIMAL(5,2) NULL DEFAULT NULL,
        calculation_policy_version VARCHAR(40) CHARACTER SET ascii COLLATE ascii_bin NULL DEFAULT NULL,
        rmr_kcal DECIMAL(10,2) NULL DEFAULT NULL,
        maintenance_kcal DECIMAL(10,2) NULL DEFAULT NULL,
        target_calories_kcal DECIMAL(10,2) NULL DEFAULT NULL,
        target_protein_g DECIMAL(10,2) NULL DEFAULT NULL,
        target_carbs_g DECIMAL(10,2) NULL DEFAULT NULL,
        target_fat_g DECIMAL(10,2) NULL DEFAULT NULL,
        calculated_at DATETIME(6) NULL DEFAULT NULL,
        created_at DATETIME(6) NOT NULL,
        updated_at DATETIME(6) NOT NULL,

        UNIQUE KEY uq_nutrition_plans_user_name (user_id, name),
        UNIQUE KEY uq_nutrition_plans_user_id (user_id, id),

        CONSTRAINT fk_nutrition_plans_user
            FOREIGN KEY (user_id) REFERENCES users(id)
            ON DELETE RESTRICT ON UPDATE RESTRICT,
        CONSTRAINT chk_nutrition_plans_name
            CHECK (CHAR_LENGTH(TRIM(name)) > 0),
        CONSTRAINT chk_nutrition_plans_mode
            CHECK (mode IN ('Easy', 'Intermediate')),
        CONSTRAINT chk_nutrition_plans_default_meals
            CHECK (default_meals_per_day BETWEEN 1 AND 8),
        CONSTRAINT chk_nutrition_plans_calculation_shape
            CHECK (
                (mode = 'Easy'
                    AND goal IS NULL
                    AND activity_level IS NULL
                    AND calculation_sex IS NULL
                    AND birth_year IS NULL
                    AND age_years IS NULL
                    AND height_cm IS NULL
                    AND weight_kg IS NULL
                    AND calculation_policy_version IS NULL
                    AND rmr_kcal IS NULL
                    AND maintenance_kcal IS NULL
                    AND target_calories_kcal IS NULL
                    AND target_protein_g IS NULL
                    AND target_carbs_g IS NULL
                    AND target_fat_g IS NULL
                    AND calculated_at IS NULL)
                OR
                (mode = 'Intermediate'
                    AND goal IS NOT NULL
                    AND goal IN ('performance', 'maintain', 'fatLoss', 'muscleGain', 'weightGain', 'recomposition')
                    AND activity_level IS NOT NULL
                    AND activity_level IN ('inactive', 'lowActive', 'active', 'veryActive')
                    AND calculation_sex IS NOT NULL
                    AND calculation_sex IN ('male', 'female')
                    AND birth_year IS NOT NULL
                    AND birth_year BETWEEN 1900 AND 9999
                    AND age_years IS NOT NULL
                    AND age_years BETWEEN 18 AND 65
                    AND height_cm IS NOT NULL
                    AND height_cm BETWEEN 120 AND 230
                    AND weight_kg IS NOT NULL
                    AND weight_kg BETWEEN 35 AND 250
                    AND calculation_policy_version IS NOT NULL
                    AND CHAR_LENGTH(TRIM(calculation_policy_version)) > 0
                    AND rmr_kcal IS NOT NULL
                    AND rmr_kcal > 0
                    AND maintenance_kcal IS NOT NULL
                    AND maintenance_kcal > 0
                    AND target_calories_kcal IS NOT NULL
                    AND target_calories_kcal BETWEEN 1500 AND 6000
                    AND target_calories_kcal = FLOOR(target_calories_kcal)
                    AND MOD(target_calories_kcal, 25) = 0
                    AND target_protein_g IS NOT NULL
                    AND target_protein_g > 0
                    AND target_protein_g = FLOOR(target_protein_g)
                    AND target_carbs_g IS NOT NULL
                    AND target_carbs_g > 0
                    AND target_carbs_g = FLOOR(target_carbs_g)
                    AND target_fat_g IS NOT NULL
                    AND target_fat_g > 0
                    AND target_fat_g = FLOOR(target_fat_g)
                    AND calculated_at IS NOT NULL)
            )
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

    CREATE TABLE IF NOT EXISTS meal_plan_user_settings (
        user_id INT NOT NULL PRIMARY KEY,
        current_nutrition_plan_id INT NULL DEFAULT NULL,
        revision BIGINT NOT NULL DEFAULT 0,
        created_at DATETIME(6) NOT NULL,
        updated_at DATETIME(6) NOT NULL,

        KEY idx_meal_plan_settings_current_owner (user_id, current_nutrition_plan_id),

        CONSTRAINT fk_meal_plan_settings_user
            FOREIGN KEY (user_id) REFERENCES users(id)
            ON DELETE RESTRICT ON UPDATE RESTRICT,
        CONSTRAINT fk_meal_plan_settings_current_plan
            FOREIGN KEY (user_id, current_nutrition_plan_id)
            REFERENCES nutrition_plans(user_id, id)
            ON DELETE RESTRICT ON UPDATE RESTRICT,
        CONSTRAINT chk_meal_plan_settings_revision
            CHECK (revision >= 0)
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

    CREATE TABLE IF NOT EXISTS meal_plan_days (
        id INT NOT NULL PRIMARY KEY AUTO_INCREMENT,
        user_id INT NOT NULL,
        plan_date DATE NOT NULL,
        created_at DATETIME(6) NOT NULL,
        updated_at DATETIME(6) NOT NULL,

        UNIQUE KEY uq_meal_plan_days_user_date (user_id, plan_date),

        CONSTRAINT fk_meal_plan_days_user
            FOREIGN KEY (user_id) REFERENCES users(id)
            ON DELETE RESTRICT ON UPDATE RESTRICT
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

    CREATE TABLE IF NOT EXISTS meal_plan_day_meals (
        id INT NOT NULL PRIMARY KEY AUTO_INCREMENT,
        meal_plan_day_id INT NOT NULL,
        name VARCHAR(80) NOT NULL,
        sort_order SMALLINT UNSIGNED NOT NULL,
        created_at DATETIME(6) NOT NULL,
        updated_at DATETIME(6) NOT NULL,

        KEY idx_meal_plan_day_meals_order (meal_plan_day_id, sort_order, id),

        CONSTRAINT fk_meal_plan_day_meals_day
            FOREIGN KEY (meal_plan_day_id) REFERENCES meal_plan_days(id)
            ON DELETE CASCADE ON UPDATE RESTRICT,
        CONSTRAINT chk_meal_plan_day_meals_name
            CHECK (CHAR_LENGTH(TRIM(name)) > 0),
        CONSTRAINT chk_meal_plan_day_meals_sort_order
            CHECK (sort_order BETWEEN 1 AND 12)
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

    CREATE TABLE IF NOT EXISTS meal_plan_meal_items (
        id INT NOT NULL PRIMARY KEY AUTO_INCREMENT,
        meal_plan_day_meal_id INT NOT NULL,
        item_type VARCHAR(8) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
        recipe_id INT NULL DEFAULT NULL,
        quantity INT NULL DEFAULT NULL,
        sort_order SMALLINT UNSIGNED NOT NULL,
        created_at DATETIME(6) NOT NULL,
        updated_at DATETIME(6) NOT NULL,

        KEY idx_meal_plan_meal_items_order (meal_plan_day_meal_id, sort_order, id),
        UNIQUE KEY uq_meal_plan_meal_items_recipe (meal_plan_day_meal_id, recipe_id),
        KEY idx_meal_plan_meal_items_recipe (recipe_id),

        CONSTRAINT fk_meal_plan_meal_items_meal
            FOREIGN KEY (meal_plan_day_meal_id) REFERENCES meal_plan_day_meals(id)
            ON DELETE CASCADE ON UPDATE RESTRICT,
        CONSTRAINT fk_meal_plan_meal_items_recipe
            FOREIGN KEY (recipe_id) REFERENCES recipes(id),
        CONSTRAINT chk_meal_plan_meal_items_shape
            CHECK (
                (item_type = 'Recipe'
                    AND recipe_id IS NOT NULL
                    AND quantity IS NOT NULL
                    AND quantity BETWEEN 1 AND 20)
                OR
                (item_type = 'Manual'
                    AND recipe_id IS NULL
                    AND quantity IS NULL)
            ),
        CONSTRAINT chk_meal_plan_meal_items_sort_order
            CHECK (sort_order BETWEEN 1 AND 20)
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

    CREATE TABLE IF NOT EXISTS meal_plan_manual_items (
        meal_plan_meal_item_id INT NOT NULL PRIMARY KEY,
        name VARCHAR(120) NOT NULL,
        calories_kcal DECIMAL(10,2) NOT NULL,
        protein_g DECIMAL(10,2) NOT NULL,
        carbs_g DECIMAL(10,2) NOT NULL,
        fat_g DECIMAL(10,2) NOT NULL,

        CONSTRAINT fk_meal_plan_manual_items_parent
            FOREIGN KEY (meal_plan_meal_item_id) REFERENCES meal_plan_meal_items(id)
            ON DELETE CASCADE ON UPDATE RESTRICT,
        CONSTRAINT chk_meal_plan_manual_items_name
            CHECK (CHAR_LENGTH(TRIM(name)) > 0),
        CONSTRAINT chk_meal_plan_manual_items_calories
            CHECK (calories_kcal BETWEEN 0.01 AND 20000),
        CONSTRAINT chk_meal_plan_manual_items_protein
            CHECK (protein_g BETWEEN 0.01 AND 5000),
        CONSTRAINT chk_meal_plan_manual_items_carbs
            CHECK (carbs_g BETWEEN 0.01 AND 5000),
        CONSTRAINT chk_meal_plan_manual_items_fat
            CHECK (fat_g BETWEEN 0.01 AND 5000)
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
END$$

CALL migrate_f011_meal_plan_schema()$$
DROP PROCEDURE migrate_f011_meal_plan_schema$$

DELIMITER ;

-- GENERATED FILE. Dataset schema copy generated from repository scriptDatabase.sql.
-- Purpose: Defines the canonical local/test MySQL schema for Appetee.
-- Change reason: Add F-011 Phase B02 nutrition profile fields and six-table Meal Plan schema.
-- Created: Existing file; original timestamp was not recorded.
-- Last updated: 2026-09-11T00:32:56-06:00

	-- ============================================================
	-- Appetee (MySQL) 
	--
	-- Highlights / decisions captured:
	-- - English naming across tables/columns
	-- - recipes: no category tag column; diets use diet_recipes and badges use recipe_badges
	-- - users: public profiles + search => unique username, display_name
	-- - no child accounts => no is_child, no user_children
	-- - sessions: ONE session per user (authentication PK=user_id)
	-- - email recovery: password_reset_tokens
	-- - favorites: saved recipes + user-created collections (Spotify/Instagram style)
	-- - ingredients: no unit on ingredients; unit is per recipe_ingredients row
	-- - nutrition: stored on ingredients and persisted on recipes for faster reads
	-- - user ingredient avoid list: renamed to user_ingredient_restrictions
	-- - character set: utf8mb4 for full Unicode
	-- - removed tables: premium, premium_unlock, data_recommendation_system
	-- ============================================================

	USE appetee;
	SET sql_safe_updates = 0;

	-- Optional for full rebuild
	SET FOREIGN_KEY_CHECKS = 0;

	-- ------------------------------------------------------------
	-- DROP TABLES
	-- ------------------------------------------------------------

	DROP TABLE IF EXISTS meal_plan_manual_items;
	DROP TABLE IF EXISTS account_permissions;
	DROP TABLE IF EXISTS administrative_audit;
	DROP TABLE IF EXISTS meal_plan_meal_items;
	DROP TABLE IF EXISTS meal_plan_day_meals;
	DROP TABLE IF EXISTS meal_plan_days;
	DROP TABLE IF EXISTS meal_plan_user_settings;
	DROP TABLE IF EXISTS nutrition_plans;

	DROP TABLE IF EXISTS user_ingredient_restrictions;
	DROP TABLE IF EXISTS recipe_ingredients;
	DROP TABLE IF EXISTS recipe_badges;
	DROP TABLE IF EXISTS ingredient_nutrition;
	DROP TABLE IF EXISTS ingredients;
	DROP TABLE IF EXISTS ingredient_types;

	DROP TABLE IF EXISTS diet_recipes;
	DROP TABLE IF EXISTS user_diets;
	DROP TABLE IF EXISTS diets;

	DROP TABLE IF EXISTS favorite_collection_recipes;
	DROP TABLE IF EXISTS favorite_collections;
	DROP TABLE IF EXISTS favorite_recipes;

	DROP TABLE IF EXISTS password_reset_tokens;
	DROP TABLE IF EXISTS account_closure_cleanup;

	-- Removed by design:
	DROP TABLE IF EXISTS user_children;

	DROP TABLE IF EXISTS users;
	DROP TABLE IF EXISTS recipes;

	SET FOREIGN_KEY_CHECKS = 1;

	-- ------------------------------------------------------------
	-- CORE TABLES
	-- ------------------------------------------------------------

	CREATE TABLE IF NOT EXISTS recipes (
		id INT NOT NULL PRIMARY KEY AUTO_INCREMENT,
		resource_version BIGINT NOT NULL DEFAULT 1,
		created_by_user_id INT NULL,
		updated_by_user_id INT NULL,
		CONSTRAINT chk_recipes_resource_version CHECK (resource_version > 0),
		name VARCHAR(255) NOT NULL,
		description VARCHAR(500) NOT NULL,
		image_blob_name VARCHAR(500) NULL,
		card_image_blob_name VARCHAR(500) NULL,
		instructions JSON NOT NULL,
		prep_time_minutes INT NOT NULL,
		cook_time_minutes INT NOT NULL,
		total_time_minutes INT NOT NULL,
		servings INT NOT NULL,
		difficulty VARCHAR(50) NOT NULL,
		estimated_cost_per_serving DECIMAL(10,2) NOT NULL,
		calories_total DECIMAL(10,2) NOT NULL,
		protein_total DECIMAL(10,2) NOT NULL,
		carbs_total DECIMAL(10,2) NOT NULL,
		fat_total DECIMAL(10,2) NULL DEFAULT NULL,
		calories_per_serving DECIMAL(10,2) NOT NULL,
		protein_per_serving DECIMAL(10,2) NOT NULL,
		created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
		updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,

		CONSTRAINT chk_recipes_prep_time
			CHECK (prep_time_minutes >= 0),
		CONSTRAINT chk_recipes_description
			CHECK (CHAR_LENGTH(TRIM(description)) > 0),
		CONSTRAINT chk_recipes_cook_time
			CHECK (cook_time_minutes >= 0),
		CONSTRAINT chk_recipes_total_time
			CHECK (total_time_minutes > 0
				AND total_time_minutes >= prep_time_minutes
				AND total_time_minutes >= cook_time_minutes),
		CONSTRAINT chk_recipes_servings
			CHECK (servings > 0),
		CONSTRAINT chk_recipes_difficulty
			CHECK (difficulty IN ('Easy', 'Medium', 'Hard')),
		CONSTRAINT chk_recipes_instructions
			CHECK (JSON_TYPE(instructions) = 'ARRAY' AND JSON_LENGTH(instructions) > 0),
		CONSTRAINT chk_recipes_estimated_cost
			CHECK (estimated_cost_per_serving >= 0),
		CONSTRAINT chk_recipes_calories_total
			CHECK (calories_total >= 0),
		CONSTRAINT chk_recipes_protein_total
			CHECK (protein_total >= 0),
		CONSTRAINT chk_recipes_carbs_total
			CHECK (carbs_total >= 0),
		CONSTRAINT chk_recipes_fat_total
			CHECK (fat_total IS NULL OR fat_total >= 0),
		CONSTRAINT chk_recipes_calories_per_serving
			CHECK (calories_per_serving >= 0),
		CONSTRAINT chk_recipes_protein_per_serving
			CHECK (protein_per_serving >= 0)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

	CREATE TABLE IF NOT EXISTS users (
		id INT NOT NULL PRIMARY KEY AUTO_INCREMENT,
		is_active BOOLEAN NOT NULL DEFAULT TRUE,
		security_version BIGINT NOT NULL DEFAULT 0,
		CONSTRAINT chk_users_security_version CHECK (security_version >= 0),
		username VARCHAR(50) NOT NULL,
		email VARCHAR(255) NOT NULL,
		password_hash VARCHAR(255) NOT NULL,

		image_url VARCHAR(255) NULL,
		calculation_sex VARCHAR(6) CHARACTER SET ascii COLLATE ascii_bin NULL DEFAULT NULL,
		birth_year SMALLINT NULL DEFAULT NULL,
		height_cm DECIMAL(5,2) NULL DEFAULT NULL,
		weight_kg DECIMAL(5,2) NULL DEFAULT NULL,

		created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
		updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,

		UNIQUE KEY uq_users_email (email),

		CONSTRAINT chk_users_nutrition_profile_complete
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
			)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

	-- ------------------------------------------------------------
	-- MEAL PLAN V1
	-- ------------------------------------------------------------

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

	-- Contains no account ID. The media URL is scrubbed as soon as cleanup succeeds.
	CREATE TABLE IF NOT EXISTS account_closure_cleanup (
		id BIGINT NOT NULL PRIMARY KEY AUTO_INCREMENT,
		former_user_id INT NULL,
		profile_image_url VARCHAR(255) NULL,
		status VARCHAR(16) NOT NULL DEFAULT 'pending',
		attempt_count INT UNSIGNED NOT NULL DEFAULT 0,
		next_attempt_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
		last_error_code VARCHAR(100) NULL,
		created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
		updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
		completed_at TIMESTAMP NULL,

		KEY idx_account_closure_cleanup_due (status, next_attempt_at, id),

		CONSTRAINT chk_account_closure_cleanup_status
			CHECK (status IN ('pending', 'completed')),
		CONSTRAINT chk_account_closure_cleanup_attempt_count
			CHECK (attempt_count <= 1000000),
		CONSTRAINT chk_account_closure_cleanup_scrubbing
			CHECK (
				(status = 'pending' AND former_user_id IS NOT NULL)
				OR (status = 'completed' AND former_user_id IS NULL AND profile_image_url IS NULL)
			)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

	CREATE TABLE IF NOT EXISTS password_reset_tokens (
		id BIGINT NOT NULL PRIMARY KEY AUTO_INCREMENT,
		user_id INT NOT NULL,

		token_hash VARCHAR(255) NOT NULL,
		expires_at TIMESTAMP NOT NULL,
		used_at TIMESTAMP NULL,

		created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,

		UNIQUE KEY uq_prt_token_hash (token_hash),
		KEY idx_prt_user_created_at (user_id, created_at),
		KEY idx_prt_expires_at (expires_at),

		CONSTRAINT fk_prt_user
			FOREIGN KEY (user_id) REFERENCES users(id)
			ON DELETE CASCADE
	) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

	-- ------------------------------------------------------------
	-- FAVORITES (saved recipes + collections)
	-- ------------------------------------------------------------

	-- Saved recipes (1 saved per user per recipe)
	CREATE TABLE IF NOT EXISTS favorite_recipes (
		user_id INT NOT NULL,
		recipe_id INT NOT NULL,
		created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,

		PRIMARY KEY (user_id, recipe_id),
		KEY idx_fav_recipe (recipe_id),

		CONSTRAINT fk_fav_user
			FOREIGN KEY (user_id) REFERENCES users(id)
			ON DELETE CASCADE,
		CONSTRAINT fk_fav_recipe
			FOREIGN KEY (recipe_id) REFERENCES recipes(id)
			ON DELETE CASCADE
	) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

	-- User-created collections (folders/categories)
	CREATE TABLE IF NOT EXISTS favorite_collections (
		id INT NOT NULL PRIMARY KEY AUTO_INCREMENT,
		user_id INT NOT NULL,
		name VARCHAR(100) NOT NULL,
		created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,

		UNIQUE KEY uq_collection_name_per_user (user_id, name),
		KEY idx_collections_user (user_id),

		CONSTRAINT fk_collections_user
			FOREIGN KEY (user_id) REFERENCES users(id)
			ON DELETE CASCADE
	) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

	-- Recipes in collections (a recipe can be in multiple collections)
	CREATE TABLE IF NOT EXISTS favorite_collection_recipes (
		collection_id INT NOT NULL,
		recipe_id INT NOT NULL,
		added_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,

		PRIMARY KEY (collection_id, recipe_id),
		KEY idx_fcr_recipe (recipe_id),

		CONSTRAINT fk_fcr_collection
			FOREIGN KEY (collection_id) REFERENCES favorite_collections(id)
			ON DELETE CASCADE,
		CONSTRAINT fk_fcr_recipe
			FOREIGN KEY (recipe_id) REFERENCES recipes(id)
			ON DELETE CASCADE
	) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

	-- ------------------------------------------------------------
	-- DIETS
	-- ------------------------------------------------------------

	CREATE TABLE IF NOT EXISTS diets (
		id INT NOT NULL PRIMARY KEY AUTO_INCREMENT,
		name VARCHAR(255) NOT NULL
	) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

	-- User ↔ diet preferences
	CREATE TABLE IF NOT EXISTS user_diets (
		user_id INT NOT NULL,
		diet_id INT NOT NULL,

		PRIMARY KEY (user_id, diet_id),
		KEY idx_user_diets_diet (diet_id),

		CONSTRAINT fk_user_diets_user
			FOREIGN KEY (user_id) REFERENCES users(id)
			ON DELETE CASCADE,
		CONSTRAINT fk_user_diets_diet
			FOREIGN KEY (diet_id) REFERENCES diets(id)
			ON DELETE CASCADE
	) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

	-- Recipe ↔ diet
	CREATE TABLE IF NOT EXISTS diet_recipes (
		recipe_id INT NOT NULL,
		diet_id INT NOT NULL,

		PRIMARY KEY (recipe_id, diet_id),
		KEY idx_diet_recipes_diet (diet_id),

		CONSTRAINT fk_diet_recipes_recipe
			FOREIGN KEY (recipe_id) REFERENCES recipes(id)
			ON DELETE CASCADE,
		CONSTRAINT fk_diet_recipes_diet
			FOREIGN KEY (diet_id) REFERENCES diets(id)
			ON DELETE CASCADE
	) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

	CREATE TABLE IF NOT EXISTS recipe_badges (
		recipe_id INT NOT NULL,
		badge VARCHAR(50) NOT NULL,

		PRIMARY KEY (recipe_id, badge),
		KEY idx_recipe_badges_badge (badge),

		CONSTRAINT fk_recipe_badges_recipe
			FOREIGN KEY (recipe_id) REFERENCES recipes(id)
			ON DELETE CASCADE,
		CONSTRAINT chk_recipe_badges_badge
			CHECK (badge IN ('High Protein', 'Low Calorie', 'Low Carb', 'High Fiber', 'Quick Meal', 'Meal Prep', 'Freezer Friendly', 'Budget Friendly', 'Few Ingredients'))
	) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

	-- ------------------------------------------------------------
	-- INGREDIENTS
	-- ------------------------------------------------------------
	CREATE TABLE IF NOT EXISTS ingredients (
		id INT NOT NULL PRIMARY KEY AUTO_INCREMENT,
		resource_version BIGINT NOT NULL DEFAULT 1,
		created_by_user_id INT NULL,
		updated_by_user_id INT NULL,
		CONSTRAINT chk_ingredients_resource_version CHECK (resource_version > 0),
		name VARCHAR(255) NOT NULL,
		image_blob_name VARCHAR(500) NULL,
		UNIQUE KEY uk_ing_name (name)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

	CREATE TABLE account_permissions (
        user_id INT NOT NULL,
        permission VARCHAR(40) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
        granted_at DATETIME(6) NOT NULL,
        granted_by_user_id INT NULL,
        PRIMARY KEY (user_id, permission),
        CONSTRAINT fk_account_permissions_user FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE,
        CONSTRAINT chk_account_permissions_code CHECK (permission IN ('Catalog.ReadAdmin','Catalog.Write'))
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

    CREATE TABLE administrative_audit (
        id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
        actor_user_id INT NULL,
        actor_reference VARCHAR(120) NOT NULL,
        operation VARCHAR(40) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
        resource_type VARCHAR(20) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
        resource_id INT NOT NULL,
        resource_version BIGINT NULL,
        permission VARCHAR(40) CHARACTER SET ascii COLLATE ascii_bin NULL,
        occurred_at DATETIME(6) NOT NULL,
        correlation_id VARCHAR(128) NOT NULL,
        outcome VARCHAR(20) CHARACTER SET ascii COLLATE ascii_bin NOT NULL DEFAULT 'succeeded',
        INDEX idx_administrative_audit_resource (resource_type,resource_id,id)
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

	-- Nutrition stored per ingredient and recipe totals persisted on recipes
	CREATE TABLE IF NOT EXISTS ingredient_nutrition (
		ingredient_id INT NOT NULL,
		basis DECIMAL(10,2) NOT NULL,
		basis_unit VARCHAR(10) NOT NULL,
		calories_kcal DECIMAL(10,2) NOT NULL,
		price DECIMAL(10,2) NOT NULL,
		protein_g DECIMAL(10,2) NOT NULL,
		fat_g DECIMAL(10,2) NULL,
		carbs_g DECIMAL(10,2) NOT NULL,
		sugar_g DECIMAL(10,2) NULL,
		fiber_g DECIMAL(10,2) NULL,
		sodium_mg DECIMAL(10,2) NULL,

		vitamin_c_mg DECIMAL(10,2) NULL,
		iron_mg DECIMAL(10,2) NULL,

		PRIMARY KEY (ingredient_id),

		CONSTRAINT fk_ing_nutrition
			FOREIGN KEY (ingredient_id) REFERENCES ingredients(id)
			ON DELETE CASCADE,
		CONSTRAINT chk_ingredient_nutrition_basis
			CHECK (basis > 0),
		CONSTRAINT chk_ingredient_nutrition_basis_unit
			CHECK (basis_unit IN ('g', 'ml')),
		CONSTRAINT chk_ingredient_nutrition_calories
			CHECK (calories_kcal >= 0),
		CONSTRAINT chk_ingredient_nutrition_price
			CHECK (price >= 0),
		CONSTRAINT chk_ingredient_nutrition_protein
			CHECK (protein_g >= 0),
		CONSTRAINT chk_ingredient_nutrition_fat
			CHECK (fat_g IS NULL OR fat_g >= 0),
		CONSTRAINT chk_ingredient_nutrition_carbs
			CHECK (carbs_g >= 0),
		CONSTRAINT chk_ingredient_nutrition_sugar
			CHECK (sugar_g IS NULL OR sugar_g >= 0),
		CONSTRAINT chk_ingredient_nutrition_fiber
			CHECK (fiber_g IS NULL OR fiber_g >= 0),
		CONSTRAINT chk_ingredient_nutrition_sodium
			CHECK (sodium_mg IS NULL OR sodium_mg >= 0),
		CONSTRAINT chk_ingredient_nutrition_vitamin_c
			CHECK (vitamin_c_mg IS NULL OR vitamin_c_mg >= 0),
		CONSTRAINT chk_ingredient_nutrition_iron
			CHECK (iron_mg IS NULL OR iron_mg >= 0)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

	-- Recipe ↔ ingredient with per-recipe unit
	CREATE TABLE IF NOT EXISTS recipe_ingredients (
		recipe_id INT NOT NULL,
		ingredient_id INT NOT NULL,

		quantity DECIMAL(10,3) NOT NULL,
		unit VARCHAR(50) NOT NULL,
		display_order SMALLINT UNSIGNED NOT NULL,
		featured_order TINYINT UNSIGNED NULL,
        
		note VARCHAR(255) NULL,

		PRIMARY KEY (recipe_id, ingredient_id),
		UNIQUE KEY uq_recipe_ingredients_display_order (recipe_id, display_order),
		UNIQUE KEY uq_recipe_ingredients_featured_order (recipe_id, featured_order),
		KEY idx_recipe_ingredients_ingredient (ingredient_id),

		CONSTRAINT fk_recipe_ingredients_recipe
			FOREIGN KEY (recipe_id) REFERENCES recipes(id)
			ON DELETE CASCADE,
		CONSTRAINT fk_recipe_ingredients_ingredient
			FOREIGN KEY (ingredient_id) REFERENCES ingredients(id)
			ON DELETE CASCADE,
		CONSTRAINT chk_recipe_ingredients_quantity
			CHECK (quantity > 0),
		CONSTRAINT chk_recipe_ingredients_unit
			CHECK (CHAR_LENGTH(TRIM(unit)) > 0),
		CONSTRAINT chk_recipe_ingredients_display_order
			CHECK (display_order >= 1),
		CONSTRAINT chk_recipe_ingredients_featured_order
			CHECK (featured_order IS NULL OR featured_order BETWEEN 1 AND 3)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

	-- User ingredient restrictions
	CREATE TABLE IF NOT EXISTS user_ingredient_restrictions (
		user_id INT NOT NULL,
		ingredient_id INT NOT NULL,

		created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,

		PRIMARY KEY (user_id, ingredient_id),
		KEY idx_uir_ingredient (ingredient_id),

		CONSTRAINT fk_uir_user
			FOREIGN KEY (user_id) REFERENCES users(id)
			ON DELETE CASCADE,
		CONSTRAINT fk_uir_ingredient
			FOREIGN KEY (ingredient_id) REFERENCES ingredients(id)
			ON DELETE CASCADE
	) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

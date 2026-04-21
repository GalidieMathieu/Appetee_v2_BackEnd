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
		name VARCHAR(255) NOT NULL,
		image_blob_name VARCHAR(500) NULL,
		instructions TEXT NOT NULL,
		prep_time_minutes INT NOT NULL,
		servings INT NOT NULL,
		difficulty VARCHAR(50) NOT NULL,
		estimated_cost_per_serving DECIMAL(10,2) NULL,
		calories_total DECIMAL(10,2) NOT NULL,
		protein_total DECIMAL(10,2) NOT NULL,
		carbs_total DECIMAL(10,2) NOT NULL,
		created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
		updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,

		CONSTRAINT chk_recipes_prep_time
			CHECK (prep_time_minutes > 0),
		CONSTRAINT chk_recipes_servings
			CHECK (servings > 0),
		CONSTRAINT chk_recipes_difficulty
			CHECK (difficulty IN ('Easy', 'Medium', 'Hard')),
		CONSTRAINT chk_recipes_estimated_cost
			CHECK (estimated_cost_per_serving IS NULL OR estimated_cost_per_serving >= 0),
		CONSTRAINT chk_recipes_calories_total
			CHECK (calories_total >= 0),
		CONSTRAINT chk_recipes_protein_total
			CHECK (protein_total >= 0),
		CONSTRAINT chk_recipes_carbs_total
			CHECK (carbs_total >= 0)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

	CREATE TABLE IF NOT EXISTS users (
		id INT NOT NULL PRIMARY KEY AUTO_INCREMENT,
		username VARCHAR(50) NOT NULL,
		email VARCHAR(255) NOT NULL,
		password_hash VARCHAR(255) NOT NULL,

		image_url VARCHAR(255) NULL,

		created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
		updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,

		UNIQUE KEY uq_users_email (email)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

	CREATE TABLE IF NOT EXISTS password_reset_tokens (
		id BIGINT NOT NULL PRIMARY KEY AUTO_INCREMENT,
		user_id INT NOT NULL,

		token_hash VARCHAR(255) NOT NULL,
		expires_at TIMESTAMP NOT NULL,
		used_at TIMESTAMP NULL,

		created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,

		KEY idx_prt_user_id (user_id),
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

	-- User ↔ diet with has_allergy flag (kept from your earlier model)
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
			CHECK (badge IN ('freezer-friendly', 'budget-focused', 'high-protein'))
	) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

	-- ------------------------------------------------------------
	-- INGREDIENTS
	-- ------------------------------------------------------------
	CREATE TABLE IF NOT EXISTS ingredients (
		id INT NOT NULL PRIMARY KEY AUTO_INCREMENT,
		name VARCHAR(255) NOT NULL,
		image_blob_name VARCHAR(500) NULL,
		UNIQUE KEY uk_ing_name (name)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

	-- Nutrition stored per ingredient and recipe totals persisted on recipes
	CREATE TABLE IF NOT EXISTS ingredient_nutrition (
		ingredient_id INT NOT NULL,
		basis DECIMAL(10,2) NOT NULL,  -- this is always in g
		calories_kcal DECIMAL(10,2),
		price DECIMAL(10,2) NOT NULL,
		protein_g DECIMAL(10,2) NULL,
		fat_g DECIMAL(10,2) NULL,
		carbs_g DECIMAL(10,2) NULL,
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
		CONSTRAINT chk_ingredient_nutrition_calories
			CHECK (calories_kcal IS NULL OR calories_kcal >= 0),
		CONSTRAINT chk_ingredient_nutrition_price
			CHECK (price >= 0),
		CONSTRAINT chk_ingredient_nutrition_protein
			CHECK (protein_g IS NULL OR protein_g >= 0),
		CONSTRAINT chk_ingredient_nutrition_fat
			CHECK (fat_g IS NULL OR fat_g >= 0),
		CONSTRAINT chk_ingredient_nutrition_carbs
			CHECK (carbs_g IS NULL OR carbs_g >= 0),
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
        
		note VARCHAR(255) NULL,

		PRIMARY KEY (recipe_id, ingredient_id),
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
			CHECK (CHAR_LENGTH(TRIM(unit)) > 0)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

	-- User avoids ingredients (allergy or taste/dislike)
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

-- Seed data for local and automated testing.
-- Run this after scriptDatabase.sql to patch the current schema shape
-- and load deterministic reference data used by the endpoint tests.

USE appetee;
SET sql_safe_updates = 0;


DELETE FROM recipe_ingredients;
DELETE FROM recipe_badges;
DELETE FROM diet_recipes;
DELETE FROM user_ingredient_restrictions;
DELETE FROM user_diets;
DELETE FROM ingredient_nutrition;
DELETE FROM ingredients;
DELETE FROM users;
DELETE FROM recipes;
DELETE FROM diets;

INSERT INTO diets (id, name)
VALUES
    (1, 'Vegetarian'),
    (2, 'Gluten Free'),
    (3, 'High Protein');

INSERT INTO ingredients (id, name, image_blob_name)
VALUES
    (1, 'Chicken Breast', 'ingredients/chicken-breast-seed.avif'),
    (2, 'Brown Rice', 'ingredients/brown-rice-seed.avif'),
    (3, 'Broccoli', 'ingredients/broccoli-seed.avif'),
    (4, 'Greek Yogurt', 'ingredients/greek-yogurt-seed.avif');

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
)
VALUES
    (1, 100.00, 'g', 165.00, 2.40, 31.00, 3.60, 0.00, 0.00, 0.00, 74.00, 0.00, 0.90),
    (2, 100.00, 'g', 123.00, 0.65, 2.70, 1.00, 25.60, 0.40, 1.80, 4.00, 0.00, 0.40),
    (3, 100.00, 'g', 34.00, 0.90, 2.80, 0.40, 6.60, 1.70, 2.60, 33.00, 89.20, 0.70),
    (4, 100.00, 'g', 97.00, 1.30, 10.00, 5.00, 3.60, 3.20, 0.00, 36.00, 0.50, 0.10);

INSERT INTO users (
    id,
    username,
    email,
    password_hash,
    image_url,
    created_at,
    updated_at
)
VALUES
    (
        1,
        'ava_seed',
        'ava.seed@appetee.test',
        'seed-user-placeholder-hash',
        'https://cdn.test/users/ava.png',
        UTC_TIMESTAMP(),
        UTC_TIMESTAMP()
    ),
    (
        2,
        'noah_seed',
        'noah.seed@appetee.test',
        'seed-user-placeholder-hash',
        'https://cdn.test/users/noah.png',
        UTC_TIMESTAMP(),
        UTC_TIMESTAMP()
    );

INSERT INTO user_diets (user_id, diet_id)
VALUES
    (1, 1),
    (1, 2),
    (2, 3);

INSERT INTO user_ingredient_restrictions (user_id, ingredient_id)
VALUES
    (1, 4),
    (2, 3);

INSERT INTO recipes (
    id,
    name,
    description,
    image_blob_name,
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
    protein_per_serving,
    created_at,
    updated_at
)
VALUES
    (
        1,
        'Chicken Rice Bowl',
        'A simple chicken, rice, and broccoli bowl.',
        'recipes/chicken-rice-bowl-seed.avif',
        JSON_ARRAY(
            JSON_OBJECT(
                'title', 'Season the chicken',
                'instruction', 'Season and sear the chicken.'
            ),
            JSON_OBJECT(
                'title', 'Cook the sides',
                'instruction', 'Cook the rice and steam the broccoli.'
            ),
            JSON_OBJECT(
                'title', 'Assemble the bowl',
                'instruction', 'Slice the chicken and serve everything together.'
            )
        ),
        10,
        15,
        25,
        2,
        'Medium',
        4.13,
        674.70,
        85.72,
        54.00,
        337.35,
        42.86,
        UTC_TIMESTAMP(),
        UTC_TIMESTAMP()
    );

INSERT INTO diet_recipes (recipe_id, diet_id)
VALUES
    (1, 2),
    (1, 3);

INSERT INTO recipe_badges (recipe_id, badge)
VALUES
    (1, 'Budget Friendly'),
    (1, 'High Protein');

INSERT INTO recipe_ingredients (recipe_id, ingredient_id, quantity, unit, display_order, featured_order, note)
VALUES
    (1, 1, 250.000, 'g', 1, 1, 'thinly sliced'),
    (1, 2, 180.000, 'g', 2, 2, NULL),
    (1, 3, 120.000, 'g', 3, 3, 'steamed');

-- GENERATED FILE. Source of truth: data/ingredients/*/ingredient.json
-- The image path is the known-valid development seed placeholder from initObjectDatabase.sql.
USE appetee;
SET NAMES utf8mb4;

-- ING-0001 Boneless Skinless Chicken Breast
INSERT INTO ingredients (name, image_blob_name) VALUES ('Boneless Skinless Chicken Breast', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Boneless Skinless Chicken Breast' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 106.00, 0.65, 22.50, 1.93, 0.00, NULL, NULL, 65.80, NULL, 0.35)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0002 Jasmine Rice
INSERT INTO ingredients (name, image_blob_name) VALUES ('Jasmine Rice', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Jasmine Rice' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 359.00, 0.32, 7.04, 1.03, 80.30, NULL, 0.15, 0.46, NULL, 0.14)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0003 Canned Black Beans
INSERT INTO ingredients (name, image_blob_name) VALUES ('Canned Black Beans', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Canned Black Beans' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 115.00, 0.20, 6.91, 1.27, 19.80, NULL, NULL, 218.00, NULL, 1.69)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0004 Frozen Corn
INSERT INTO ingredients (name, image_blob_name) VALUES ('Frozen Corn', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Frozen Corn' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 81.00, 0.40, 2.55, 0.67, 19.30, 3.07, 2.40, 1.00, 3.50, 0.47)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0005 Chunky Salsa
INSERT INTO ingredients (name, image_blob_name) VALUES ('Chunky Salsa', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Chunky Salsa' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 29.00, 0.48, 1.44, 0.19, 6.74, NULL, 1.80, 656.00, NULL, 0.42)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0006 Ground Turkey 87% Lean
INSERT INTO ingredients (name, image_blob_name) VALUES ('Ground Turkey 87% Lean', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Ground Turkey 87% Lean' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 180.00, 0.44, 16.90, 12.50, 0.00, 0.00, 0.00, 54.00, 0.00, 1.32)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0007 Sweet Potatoes
INSERT INTO ingredients (name, image_blob_name) VALUES ('Sweet Potatoes', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Sweet Potatoes' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 79.00, 0.24, 1.58, 0.38, 17.30, NULL, NULL, 0.00, 14.80, 0.40)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0008 Canned Diced Tomatoes
INSERT INTO ingredients (name, image_blob_name) VALUES ('Canned Diced Tomatoes', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Canned Diced Tomatoes' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 18.00, 0.22, 0.84, 0.50, 3.32, NULL, NULL, 125.00, NULL, 0.57)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0009 Yellow Onion
INSERT INTO ingredients (name, image_blob_name) VALUES ('Yellow Onion', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Yellow Onion' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 38.00, 0.27, 0.83, 0.05, 8.61, NULL, 1.90, 1.00, 8.20, 0.28)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0010 Chili Powder
INSERT INTO ingredients (name, image_blob_name) VALUES ('Chili Powder', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Chili Powder' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 282.00, 1.27, 13.50, 14.30, 49.70, 7.19, 34.80, 2870.00, 0.70, 17.30)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0011 Pink Salmon Fillet
INSERT INTO ingredients (name, image_blob_name) VALUES ('Pink Salmon Fillet', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Pink Salmon Fillet' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 127.00, 1.26, 20.50, 4.40, 0.00, 0.00, 0.00, 75.00, 0.00, 0.38)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0012 White Quinoa
INSERT INTO ingredients (name, image_blob_name) VALUES ('White Quinoa', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'White Quinoa' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 368.00, 0.69, 14.10, 6.07, 64.20, NULL, 7.00, 5.00, NULL, 4.57)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0013 Frozen Broccoli Florets
INSERT INTO ingredients (name, image_blob_name) VALUES ('Frozen Broccoli Florets', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Frozen Broccoli Florets' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 26.00, 0.32, 2.81, 0.29, 4.78, 1.35, 3.00, 24.00, 56.40, 0.81)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0014 Extra Virgin Olive Oil
INSERT INTO ingredients (name, image_blob_name) VALUES ('Extra Virgin Olive Oil', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Extra Virgin Olive Oil' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'ml', 804.44, 1.22, 0.00, 91.00, 0.00, 0.00, 0.00, 1.82, 0.00, 0.51)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0015 Brown Lentils
INSERT INTO ingredients (name, image_blob_name) VALUES ('Brown Lentils', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Brown Lentils' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 352.00, 0.42, 24.60, 1.06, 63.40, 2.03, 10.70, 6.00, 4.50, 6.51)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0016 Frozen Chopped Spinach
INSERT INTO ingredients (name, image_blob_name) VALUES ('Frozen Chopped Spinach', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Frozen Chopped Spinach' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 29.00, 0.37, 3.63, 0.57, 4.21, 0.65, 2.90, 74.00, 5.50, 1.89)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0017 Curry Powder
INSERT INTO ingredients (name, image_blob_name) VALUES ('Curry Powder', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Curry Powder' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 325.00, 3.67, 14.30, 14.00, 55.80, 2.76, 53.20, 52.00, 0.70, 19.10)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0018 Extra-Firm Tofu
INSERT INTO ingredients (name, image_blob_name) VALUES ('Extra-Firm Tofu', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Extra-Firm Tofu' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 78.00, 0.74, 9.04, 4.17, 2.85, 0.60, 0.90, 12.00, 0.20, 1.61)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0019 Frozen Peas and Carrots
INSERT INTO ingredients (name, image_blob_name) VALUES ('Frozen Peas and Carrots', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Frozen Peas and Carrots' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 53.00, 0.29, 3.40, 0.47, 11.20, NULL, 3.40, 79.00, 11.20, 1.09)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0020 Soy Sauce
INSERT INTO ingredients (name, image_blob_name) VALUES ('Soy Sauce', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Soy Sauce' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'ml', 61.48, 0.40, 9.44, 0.66, 5.72, 0.46, 0.93, 6368.40, 0.00, 1.68)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0021 Ground Beef 85% Lean
INSERT INTO ingredients (name, image_blob_name) VALUES ('Ground Beef 85% Lean', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Ground Beef 85% Lean' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 215.00, 1.64, 18.60, 15.00, 0.00, 0.00, 0.00, 66.00, 0.00, 2.09)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0022 Canned Chickpeas
INSERT INTO ingredients (name, image_blob_name) VALUES ('Canned Chickpeas', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Canned Chickpeas' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 139.00, 0.20, 7.05, 2.77, 22.50, 4.01, 6.40, 246.00, 0.10, 1.07)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0023 Roma Tomatoes
INSERT INTO ingredients (name, image_blob_name) VALUES ('Roma Tomatoes', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Roma Tomatoes' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 18.00, 0.25, 0.88, 0.20, 3.89, 2.63, 1.20, 5.00, 13.70, 0.27)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0024 Apple Cider Vinegar
INSERT INTO ingredients (name, image_blob_name) VALUES ('Apple Cider Vinegar', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Apple Cider Vinegar' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'ml', 21.21, 0.26, 0.00, 0.00, 0.94, 0.40, 0.00, 5.05, 0.00, 0.20)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0025 Canned Chunk Light Tuna
INSERT INTO ingredients (name, image_blob_name) VALUES ('Canned Chunk Light Tuna', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Canned Chunk Light Tuna' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 116.00, 0.68, 25.50, 0.82, 0.00, 0.00, 0.00, 50.00, 0.00, 1.53)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0026 Canned Cannellini Beans
INSERT INTO ingredients (name, image_blob_name) VALUES ('Canned Cannellini Beans', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Canned Cannellini Beans' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 114.00, 0.20, 7.26, 0.29, 21.20, 0.29, 4.80, 340.00, 0.00, 2.99)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0027 Whole-Wheat Flour Tortillas
INSERT INTO ingredients (name, image_blob_name) VALUES ('Whole-Wheat Flour Tortillas', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Whole-Wheat Flour Tortillas' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 310.00, 0.61, 9.76, 9.76, 45.90, 2.44, 9.80, 617.00, 0.00, 2.63)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0028 Large Eggs
INSERT INTO ingredients (name, image_blob_name) VALUES ('Large Eggs', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Large Eggs' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 143.00, 0.24, 12.60, 9.51, 0.72, 0.37, 0.00, 142.00, 0.00, 1.75)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0029 Quick Oats
INSERT INTO ingredients (name, image_blob_name) VALUES ('Quick Oats', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Quick Oats' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 379.00, 0.37, 13.20, 6.52, 67.70, 0.99, 10.10, 6.00, 0.00, 4.25)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0030 2% Milk
INSERT INTO ingredients (name, image_blob_name) VALUES ('2% Milk', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = '2% Milk' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'ml', 51.50, 0.12, 3.40, 2.04, 4.94, 5.21, 0.00, 48.41, 0.21, 0.02)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0031 Plain Nonfat Greek Yogurt
INSERT INTO ingredients (name, image_blob_name) VALUES ('Plain Nonfat Greek Yogurt', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Plain Nonfat Greek Yogurt' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 59.00, 0.36, 10.20, 0.39, 3.60, 3.24, 0.00, 36.00, 0.00, 0.07)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0032 Crunchy Peanut Butter
INSERT INTO ingredients (name, image_blob_name) VALUES ('Crunchy Peanut Butter', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Crunchy Peanut Butter' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 589.00, 0.33, 24.10, 49.90, 21.60, 8.41, 8.00, 486.00, 0.00, 1.90)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0033 Bananas
INSERT INTO ingredients (name, image_blob_name) VALUES ('Bananas', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Bananas' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 89.00, 0.11, 1.09, 0.33, 22.80, 12.20, 2.60, 1.00, 8.70, 0.26)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0034 Frozen Mixed Berries
INSERT INTO ingredients (name, image_blob_name) VALUES ('Frozen Mixed Berries', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Frozen Mixed Berries' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 35.00, 0.59, 0.43, 0.11, 9.13, 4.56, 2.10, 2.00, 41.20, 0.75)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0035 Low-Fat Cottage Cheese
INSERT INTO ingredients (name, image_blob_name) VALUES ('Low-Fat Cottage Cheese', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Low-Fat Cottage Cheese' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 72.00, 0.42, 12.40, 1.02, 2.72, 2.72, 0.00, 406.00, 0.00, 0.14)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0036 Boneless Skinless Chicken Thighs
INSERT INTO ingredients (name, image_blob_name) VALUES ('Boneless Skinless Chicken Thighs', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Boneless Skinless Chicken Thighs' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 110.00, 0.73, 19.10, 3.69, 0.00, 0.00, 0.00, 156.00, 0.00, 0.60)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0037 Frozen Cut Green Beans
INSERT INTO ingredients (name, image_blob_name) VALUES ('Frozen Cut Green Beans', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Frozen Cut Green Beans' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 33.00, 0.29, 1.79, 0.21, 7.54, 2.21, 2.60, 3.00, 12.90, 0.85)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0038 Raw Peeled Shrimp
INSERT INTO ingredients (name, image_blob_name) VALUES ('Raw Peeled Shrimp', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Raw Peeled Shrimp' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 71.00, 1.99, 13.60, 1.01, 0.91, 0.00, 0.00, 566.00, 0.00, 0.21)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0039 Dry Penne Pasta
INSERT INTO ingredients (name, image_blob_name) VALUES ('Dry Penne Pasta', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Dry Penne Pasta' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 371.00, 0.27, 13.00, 1.51, 74.70, 2.67, 3.20, 6.00, 0.00, 3.30)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0040 Canned Coconut Milk
INSERT INTO ingredients (name, image_blob_name) VALUES ('Canned Coconut Milk', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Canned Coconut Milk' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'ml', 197.00, 0.56, 2.02, 21.30, 2.81, NULL, NULL, 13.00, 1.00, 3.30)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0041 Minced Garlic
INSERT INTO ingredients (name, image_blob_name) VALUES ('Minced Garlic', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Minced Garlic' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 149.00, 0.66, 6.36, 0.50, 33.10, 1.00, 2.10, 17.00, 31.20, 1.70)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0042 Red Lentils
INSERT INTO ingredients (name, image_blob_name) VALUES ('Red Lentils', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Red Lentils' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 352.00, 0.41, 24.60, 1.06, 63.40, 2.03, 10.70, 6.00, 4.50, 6.51)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0043 Berbere Spice Blend
INSERT INTO ingredients (name, image_blob_name) VALUES ('Berbere Spice Blend', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Berbere Spice Blend' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 282.00, 17.61, 14.10, 12.90, 54.00, 10.30, 34.90, 68.00, 0.90, 21.10)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0044 Cod Fillets
INSERT INTO ingredients (name, image_blob_name) VALUES ('Cod Fillets', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Cod Fillets' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 69.00, 2.05, 15.30, 0.41, 0.00, 0.00, 0.00, 303.00, 0.00, 0.16)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0045 Russet Potatoes
INSERT INTO ingredients (name, image_blob_name) VALUES ('Russet Potatoes', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Russet Potatoes' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 79.00, 0.14, 2.14, 0.08, 18.10, 0.62, 1.30, 5.00, 5.70, 0.86)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0046 Ground Cumin
INSERT INTO ingredients (name, image_blob_name) VALUES ('Ground Cumin', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Ground Cumin' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 375.00, 2.09, 17.80, 22.30, 44.20, 2.25, 10.50, 168.00, 7.70, 66.40)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0047 Ground Cinnamon
INSERT INTO ingredients (name, image_blob_name) VALUES ('Ground Cinnamon', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Ground Cinnamon' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 247.00, 3.54, 3.99, 1.24, 80.60, 2.17, 53.10, 10.00, 3.80, 8.32)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0048 Ground Pork
INSERT INTO ingredients (name, image_blob_name) VALUES ('Ground Pork', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Ground Pork' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 263.00, 0.71, 16.90, 21.20, 0.00, NULL, 0.00, 56.00, 0.70, 0.88)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0049 Five-Grain Tempeh
INSERT INTO ingredients (name, image_blob_name) VALUES ('Five-Grain Tempeh', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Five-Grain Tempeh' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 192.00, 2.97, 20.30, 10.80, 7.64, NULL, NULL, 9.00, 0.00, 2.70)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0050 Frozen Edamame
INSERT INTO ingredients (name, image_blob_name) VALUES ('Frozen Edamame', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Frozen Edamame' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 109.00, 0.56, 11.20, 4.73, 7.61, 2.48, 4.80, 6.00, 9.70, 2.11)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0051 Whole-Wheat Bread
INSERT INTO ingredients (name, image_blob_name) VALUES ('Whole-Wheat Bread', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Whole-Wheat Bread' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 252.00, 0.33, 12.40, 3.50, 42.70, 4.34, 6.00, 455.00, 0.00, 2.47)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0052 Granulated Sugar
INSERT INTO ingredients (name, image_blob_name) VALUES ('Granulated Sugar', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Granulated Sugar' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 387.00, 0.18, 0.00, 0.00, 100.00, 99.80, 0.00, 1.00, 0.00, 0.05)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0053 Ground Paprika
INSERT INTO ingredients (name, image_blob_name) VALUES ('Ground Paprika', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Ground Paprika' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 282.00, 1.52, 14.10, 12.90, 54.00, 10.30, 34.90, 68.00, 0.90, 21.10)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0054 Tomato Paste
INSERT INTO ingredients (name, image_blob_name) VALUES ('Tomato Paste', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Tomato Paste' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 82.00, 0.69, 4.32, 0.47, 18.90, 12.20, 4.10, 59.00, 21.90, 2.98)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0055 Chicken Broth
INSERT INTO ingredients (name, image_blob_name) VALUES ('Chicken Broth', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Chicken Broth' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'ml', 7.00, 0.16, 1.36, 0.00, 0.38, 0.22, 0.00, 231.00, 0.40, 0.26)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0056 Unsalted Butter
INSERT INTO ingredients (name, image_blob_name) VALUES ('Unsalted Butter', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Unsalted Butter' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 717.00, 0.67, 0.85, 81.10, 0.06, 0.06, 0.00, 11.00, 0.00, 0.02)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0057 Pure Vanilla Extract
INSERT INTO ingredients (name, image_blob_name) VALUES ('Pure Vanilla Extract', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Pure Vanilla Extract' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'ml', 253.15, 12.14, 0.05, 0.05, 11.07, 11.07, 0.00, 7.91, 0.00, 0.10)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0058 Dried Parsley
INSERT INTO ingredients (name, image_blob_name) VALUES ('Dried Parsley', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Dried Parsley' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 292.00, 9.52, 26.60, 5.48, 50.60, 7.27, 26.70, 452.00, 125.00, 22.00)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0059 Fresh Lemons
INSERT INTO ingredients (name, image_blob_name) VALUES ('Fresh Lemons', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Fresh Lemons' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 29.00, 0.43, 1.10, 0.30, 9.32, 2.50, 2.80, 2.00, 53.00, 0.60)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0060 Red Bell Pepper
INSERT INTO ingredients (name, image_blob_name) VALUES ('Red Bell Pepper', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Red Bell Pepper' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 26.00, 1.24, 0.99, 0.30, 6.03, 4.20, 2.10, 4.00, 128.00, 0.43)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0061 Green Bell Pepper
INSERT INTO ingredients (name, image_blob_name) VALUES ('Green Bell Pepper', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Green Bell Pepper' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 20.00, 0.69, 0.86, 0.17, 4.64, 2.40, 1.70, 3.00, 80.40, 0.34)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0062 Green Onions
INSERT INTO ingredients (name, image_blob_name) VALUES ('Green Onions', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Green Onions' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 32.00, 2.62, 1.83, 0.19, 7.34, 2.33, 2.60, 16.00, 18.80, 1.48)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0063 Fresh Cilantro
INSERT INTO ingredients (name, image_blob_name) VALUES ('Fresh Cilantro', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Fresh Cilantro' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 23.00, 1.21, 2.13, 0.52, 3.67, 0.87, 2.80, 46.00, 27.00, 1.77)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0064 Eggplant
INSERT INTO ingredients (name, image_blob_name) VALUES ('Eggplant', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Eggplant' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 25.00, 0.40, 0.98, 0.18, 5.88, 3.53, 3.00, 2.00, 2.20, 0.23)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0065 Bay Leaves
INSERT INTO ingredients (name, image_blob_name) VALUES ('Bay Leaves', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Bay Leaves' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 313.00, 87.60, 7.61, 8.36, 75.00, NULL, 26.30, 23.00, 46.50, 43.00)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0066 Cornstarch
INSERT INTO ingredients (name, image_blob_name) VALUES ('Cornstarch', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Cornstarch' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 381.00, 0.42, 0.26, 0.05, 91.30, 0.00, 0.90, 9.00, 0.00, 0.47)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0067 All-Purpose Flour
INSERT INTO ingredients (name, image_blob_name) VALUES ('All-Purpose Flour', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'All-Purpose Flour' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 364.00, 0.10, 10.30, 0.98, 76.30, 0.27, 2.70, 2.00, 0.00, 4.64)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0068 Top Sirloin Steak
INSERT INTO ingredients (name, image_blob_name) VALUES ('Top Sirloin Steak', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Top Sirloin Steak' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 127.00, 3.07, 22.30, 3.54, 0.00, 0.00, 0.00, 56.00, 0.00, 1.61)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0069 Pork Tenderloin
INSERT INTO ingredients (name, image_blob_name) VALUES ('Pork Tenderloin', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Pork Tenderloin' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 109.00, 0.92, 21.00, 2.17, 0.00, 0.00, 0.00, 53.00, 0.00, 0.98)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0070 Fresh Ginger
INSERT INTO ingredients (name, image_blob_name) VALUES ('Fresh Ginger', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Fresh Ginger' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 80.00, 1.99, 1.82, 0.75, 17.80, 1.70, 2.00, 13.00, 5.00, 0.60)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0071 Toasted Sesame Oil
INSERT INTO ingredients (name, image_blob_name) VALUES ('Toasted Sesame Oil', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Toasted Sesame Oil' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'ml', 813.28, 2.19, 0.00, 92.00, 0.00, 0.00, 0.00, 0.00, 0.00, 0.00)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0072 Sesame Seeds
INSERT INTO ingredients (name, image_blob_name) VALUES ('Sesame Seeds', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Sesame Seeds' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 631.00, 3.50, 20.40, 61.20, 11.70, 0.48, 11.60, 47.00, 0.00, 6.36)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0073 Gochujang
INSERT INTO ingredients (name, image_blob_name) VALUES ('Gochujang', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Gochujang' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 21.00, 2.06, 0.90, 0.60, 3.90, 2.55, 0.70, 25.00, 30.00, 0.50)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0074 White Mushrooms
INSERT INTO ingredients (name, image_blob_name) VALUES ('White Mushrooms', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'White Mushrooms' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 22.00, 0.86, 3.09, 0.34, 3.26, 1.98, 1.00, 5.00, 2.10, 0.50)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0075 Dried Fava Beans
INSERT INTO ingredients (name, image_blob_name) VALUES ('Dried Fava Beans', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Dried Fava Beans' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 341.00, 1.24, 26.10, 1.53, 58.30, 5.70, 25.00, 13.00, 1.40, 6.70)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0076 Dry Black Beans
INSERT INTO ingredients (name, image_blob_name) VALUES ('Dry Black Beans', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Dry Black Beans' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 341.00, 0.33, 21.60, 1.42, 62.40, 2.12, 15.50, 5.00, 0.00, 5.02)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0077 Garam Masala
INSERT INTO ingredients (name, image_blob_name) VALUES ('Garam Masala', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Garam Masala' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 325.00, 4.03, 14.30, 14.00, 55.80, 2.76, 53.20, 52.00, 0.70, 19.10)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0078 Canned Kidney Beans
INSERT INTO ingredients (name, image_blob_name) VALUES ('Canned Kidney Beans', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Canned Kidney Beans' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 81.00, 0.20, 5.22, 0.36, 14.80, 1.85, 5.30, 117.00, 0.80, 1.25)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0079 Ground Nutmeg
INSERT INTO ingredients (name, image_blob_name) VALUES ('Ground Nutmeg', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Ground Nutmeg' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 525.00, 5.27, 5.84, 36.30, 49.30, 2.99, 20.80, 16.00, 3.00, 3.04)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0080 Grated Parmesan Cheese
INSERT INTO ingredients (name, image_blob_name) VALUES ('Grated Parmesan Cheese', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Grated Parmesan Cheese' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 420.00, 1.31, 28.40, 27.80, 13.90, 0.07, 0.00, 1800.00, 0.00, 0.49)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0081 Creamed Corn
INSERT INTO ingredients (name, image_blob_name) VALUES ('Creamed Corn', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Creamed Corn' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 72.00, 0.17, 1.74, 0.42, 18.10, 3.23, 1.20, 261.00, 4.60, 0.38)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0082 Thai Red Curry Paste
INSERT INTO ingredients (name, image_blob_name) VALUES ('Thai Red Curry Paste', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Thai Red Curry Paste' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 325.00, 1.59, 14.30, 14.00, 55.80, 2.76, 53.20, 52.00, 0.70, 19.10)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0083 Zucchini
INSERT INTO ingredients (name, image_blob_name) VALUES ('Zucchini', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Zucchini' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 17.00, 0.34, 1.21, 0.32, 3.11, 2.50, 1.00, 8.00, 17.90, 0.37)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0084 Green Olives
INSERT INTO ingredients (name, image_blob_name) VALUES ('Green Olives', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Green Olives' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 116.00, 2.53, 0.84, 10.90, 6.04, 0.00, 1.60, 735.00, 0.90, 6.28)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0085 Pork Chorizo
INSERT INTO ingredients (name, image_blob_name) VALUES ('Pork Chorizo', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Pork Chorizo' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 296.00, 0.59, 13.60, 25.10, 3.78, 0.00, 0.00, 788.00, 0.00, 1.41)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0086 Fresh Rosemary
INSERT INTO ingredients (name, image_blob_name) VALUES ('Fresh Rosemary', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Fresh Rosemary' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 131.00, 12.56, 3.31, 5.86, 20.70, NULL, 14.10, 26.00, 21.80, 6.65)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0087 Dried Thyme
INSERT INTO ingredients (name, image_blob_name) VALUES ('Dried Thyme', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Dried Thyme' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 276.00, 7.00, 9.11, 7.43, 63.90, 1.71, 37.00, 55.00, 50.00, 124.00)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0088 Harissa Paste
INSERT INTO ingredients (name, image_blob_name) VALUES ('Harissa Paste', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Harissa Paste' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 93.00, 3.05, 1.93, 0.93, 19.20, 15.10, 2.20, 2120.00, 26.90, 1.64)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0089 Red Cooking Wine
INSERT INTO ingredients (name, image_blob_name) VALUES ('Red Cooking Wine', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Red Cooking Wine' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'ml', 84.15, 0.91, 0.07, 0.00, 2.58, 0.61, 0.00, 3.96, 0.00, 0.46)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0090 Dijon Mustard
INSERT INTO ingredients (name, image_blob_name) VALUES ('Dijon Mustard', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Dijon Mustard' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 60.00, 0.51, 3.74, 3.34, 5.83, 0.92, 4.00, 1100.00, 0.30, 1.61)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0091 Ground Turmeric
INSERT INTO ingredients (name, image_blob_name) VALUES ('Ground Turmeric', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Ground Turmeric' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 312.00, 3.47, 9.68, 3.25, 67.10, 3.21, 22.70, 27.00, 0.70, 55.00)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0092 Honey
INSERT INTO ingredients (name, image_blob_name) VALUES ('Honey', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Honey' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 304.00, 0.92, 0.30, 0.00, 82.40, 82.10, 0.20, 4.00, 0.50, 0.42)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0093 Balsamic Vinegar
INSERT INTO ingredients (name, image_blob_name) VALUES ('Balsamic Vinegar', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Balsamic Vinegar' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'ml', 92.40, 1.65, 0.51, 0.00, 17.85, 15.75, NULL, 24.15, 0.00, 0.76)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0094 Fresh Carrots
INSERT INTO ingredients (name, image_blob_name) VALUES ('Fresh Carrots', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Fresh Carrots' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 41.00, 0.25, 0.93, 0.24, 9.58, 4.74, 2.80, 69.00, 5.90, 0.30)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0095 Seedless Raisins
INSERT INTO ingredients (name, image_blob_name) VALUES ('Seedless Raisins', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Seedless Raisins' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 299.00, 0.84, 3.30, 0.25, 79.30, 65.20, 4.50, 26.00, 2.30, 1.79)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0096 Whole Natural Almonds
INSERT INTO ingredients (name, image_blob_name) VALUES ('Whole Natural Almonds', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Whole Natural Almonds' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 579.00, 1.71, 21.20, 49.90, 21.60, 4.35, 12.50, 1.00, 0.00, 3.71)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0097 Fresh Green Kale
INSERT INTO ingredients (name, image_blob_name) VALUES ('Fresh Green Kale', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Fresh Green Kale' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 35.00, 0.77, 2.92, 1.49, 4.42, 0.99, 4.10, 53.00, 93.40, 1.60)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0098 Dry Orzo Pasta
INSERT INTO ingredients (name, image_blob_name) VALUES ('Dry Orzo Pasta', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Dry Orzo Pasta' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 371.00, 0.27, 13.00, 1.51, 74.70, 2.67, 3.20, 6.00, 0.00, 3.30)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0099 Fresh Basil
INSERT INTO ingredients (name, image_blob_name) VALUES ('Fresh Basil', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Fresh Basil' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 23.00, 12.56, 3.15, 0.64, 2.65, 0.30, 1.60, 4.00, 18.00, 3.17)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0100 Fresh Green Cabbage
INSERT INTO ingredients (name, image_blob_name) VALUES ('Fresh Green Cabbage', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Fresh Green Cabbage' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 25.00, 0.19, 1.28, 0.10, 5.80, 3.20, 2.50, 18.00, 36.60, 0.47)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0101 Fresh Cucumber
INSERT INTO ingredients (name, image_blob_name) VALUES ('Fresh Cucumber', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Fresh Cucumber' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 15.00, 0.25, 0.65, 0.11, 3.63, 1.67, 0.50, 2.00, 2.80, 0.28)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0102 Frozen Cauliflower
INSERT INTO ingredients (name, image_blob_name) VALUES ('Frozen Cauliflower', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Frozen Cauliflower' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 24.00, 0.34, 2.01, 0.27, 4.68, 2.22, 2.30, 24.00, 48.80, 0.54)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0103 Dry Egg Noodles
INSERT INTO ingredients (name, image_blob_name) VALUES ('Dry Egg Noodles', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Dry Egg Noodles' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 384.00, 0.38, 14.20, 4.44, 71.30, 1.88, 3.30, 21.00, 0.00, 4.01)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0104 Fresh Apples
INSERT INTO ingredients (name, image_blob_name) VALUES ('Fresh Apples', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Fresh Apples' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 63.00, 0.39, 0.20, 0.18, 15.20, 11.70, 2.10, 1.00, NULL, 0.10)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0105 Unsweetened Cocoa Powder
INSERT INTO ingredients (name, image_blob_name) VALUES ('Unsweetened Cocoa Powder', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Unsweetened Cocoa Powder' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 228.00, 2.15, 19.60, 13.70, 57.90, 1.75, 37.00, 21.00, 0.00, 13.90)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0106 Whole-Wheat Pita Bread
INSERT INTO ingredients (name, image_blob_name) VALUES ('Whole-Wheat Pita Bread', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Whole-Wheat Pita Bread' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 262.00, 2.36, 9.80, 1.71, 55.90, 2.87, 6.10, 421.00, 0.00, 3.06)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0107 Frozen Mango Chunks
INSERT INTO ingredients (name, image_blob_name) VALUES ('Frozen Mango Chunks', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Frozen Mango Chunks' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 60.00, 0.75, 0.82, 0.38, 15.00, 13.70, 1.60, 1.00, 36.40, 0.16)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0108 Frozen Sliced Peaches
INSERT INTO ingredients (name, image_blob_name) VALUES ('Frozen Sliced Peaches', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Frozen Sliced Peaches' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 39.00, 0.63, 0.91, 0.25, 9.54, 8.39, 1.50, 0.00, 6.60, 0.25)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0109 Fresh Asparagus
INSERT INTO ingredients (name, image_blob_name) VALUES ('Fresh Asparagus', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Fresh Asparagus' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 20.00, 0.78, 2.20, 0.12, 3.88, 1.88, 2.10, 2.00, 5.60, 2.14)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0110 Dry Couscous
INSERT INTO ingredients (name, image_blob_name) VALUES ('Dry Couscous', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Dry Couscous' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 376.00, 0.90, 12.80, 0.64, 77.40, NULL, 5.00, 10.00, 0.00, 1.08)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0111 Frozen Dark Sweet Cherries
INSERT INTO ingredients (name, image_blob_name) VALUES ('Frozen Dark Sweet Cherries', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Frozen Dark Sweet Cherries' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 63.00, 0.84, 1.06, 0.20, 16.00, 12.80, 2.10, 0.00, 7.00, 0.36)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0112 Canned Pumpkin Puree
INSERT INTO ingredients (name, image_blob_name) VALUES ('Canned Pumpkin Puree', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Canned Pumpkin Puree' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 34.00, 0.46, 1.10, 0.28, 8.09, 3.30, 2.90, 5.00, 4.20, 1.39)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0113 Frozen Pineapple Chunks
INSERT INTO ingredients (name, image_blob_name) VALUES ('Frozen Pineapple Chunks', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Frozen Pineapple Chunks' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 50.00, 0.62, 0.54, 0.12, 13.10, 9.85, 1.40, 1.00, 47.80, 0.29)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0114 Fresh Avocado
INSERT INTO ingredients (name, image_blob_name) VALUES ('Fresh Avocado', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Fresh Avocado' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 160.00, 0.73, 2.00, 14.70, 8.53, 0.66, 6.70, 7.00, 10.00, 0.55)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0115 Pitted Deglet Noor Dates
INSERT INTO ingredients (name, image_blob_name) VALUES ('Pitted Deglet Noor Dates', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Pitted Deglet Noor Dates' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 282.00, 1.31, 2.45, 0.39, 75.00, 63.40, 8.00, 2.00, 0.40, 1.02)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0116 Pure Maple Syrup
INSERT INTO ingredients (name, image_blob_name) VALUES ('Pure Maple Syrup', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Pure Maple Syrup' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'ml', 345.80, 2.16, 0.05, 0.08, 89.11, 80.47, 0.00, 15.96, 0.00, 0.15)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0117 Plain Low-Fat Kefir
INSERT INTO ingredients (name, image_blob_name) VALUES ('Plain Low-Fat Kefir', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Plain Low-Fat Kefir' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'ml', 44.29, 0.41, 3.90, 1.05, 4.91, 4.75, 0.00, 41.20, 0.21, 0.04)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0118 Fresh Bartlett Pears
INSERT INTO ingredients (name, image_blob_name) VALUES ('Fresh Bartlett Pears', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Fresh Bartlett Pears' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 63.00, 0.44, 0.39, 0.16, 15.00, 9.69, 3.10, 1.00, 4.40, 0.19)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0119 Dried Chia Seeds
INSERT INTO ingredients (name, image_blob_name) VALUES ('Dried Chia Seeds', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Dried Chia Seeds' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 486.00, 3.35, 16.50, 30.70, 42.10, NULL, 34.40, 16.00, 1.60, 7.72)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0120 Semolina Flour
INSERT INTO ingredients (name, image_blob_name) VALUES ('Semolina Flour', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Semolina Flour' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 360.00, 0.54, 12.70, 1.05, 72.80, NULL, 3.90, 1.00, 0.00, 4.36)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);

-- ING-0121 Tahini
INSERT INTO ingredients (name, image_blob_name) VALUES ('Tahini', 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);
SET @ingredient_id := (SELECT id FROM ingredients WHERE name = 'Tahini' LIMIT 1);
INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)
VALUES (@ingredient_id, 100.00, 'g', 595.00, 3.30, 17.00, 53.80, 21.20, 0.49, 9.30, 115.00, 0.00, 8.95)
ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);


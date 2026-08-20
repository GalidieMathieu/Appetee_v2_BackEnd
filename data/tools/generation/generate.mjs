import { mkdir, readdir, readFile, writeFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { runValidation } from "../validation/validate.mjs";
import { syncImageQueues } from "../image-processing/sync-image-queues.mjs";

const toolDir = path.dirname(fileURLToPath(import.meta.url));
const dataDir = path.resolve(toolDir, "..", "..");
const repoDir = path.resolve(dataDir, "..");
const json = (value) => `${JSON.stringify(value, null, 2)}\n`;
const sqlString = (value) => `'${String(value).replaceAll("\\", "\\\\").replaceAll("'", "''")}'`;
const sqlNumber = (value) => value === undefined || value === null ? "NULL" : Number(value).toFixed(2);

async function records(root, jsonName) {
  const entries = (await readdir(root, { withFileTypes: true })).filter((entry) => entry.isDirectory()).sort((a, b) => a.name.localeCompare(b.name));
  const output = [];
  for (const entry of entries) {
    const file = path.join(root, entry.name, jsonName);
    try { output.push({ directory: entry.name, data: JSON.parse(await readFile(file, "utf8")) }); } catch {}
  }
  return output;
}

function ingredientSql(items) {
  const lines = [
    "-- GENERATED FILE. Source of truth: data/ingredients/*/ingredient.json",
    "-- The image path is the known-valid development seed placeholder from initObjectDatabase.sql.",
    "USE appetee;",
    "SET NAMES utf8mb4;",
    "",
  ];
  for (const { data: item } of items) {
    const n = item.normalizedNutrition.values;
    lines.push(`-- ${item.seedId} ${item.name}`);
    lines.push(`INSERT INTO ingredients (name, image_blob_name) VALUES (${sqlString(item.name)}, 'ingredients/chicken-breast-seed.avif') ON DUPLICATE KEY UPDATE image_blob_name = VALUES(image_blob_name);`);
    lines.push(`SET @ingredient_id := (SELECT id FROM ingredients WHERE name = ${sqlString(item.name)} LIMIT 1);`);
    lines.push("INSERT INTO ingredient_nutrition (ingredient_id, basis, basis_unit, calories_kcal, price, protein_g, fat_g, carbs_g, sugar_g, fiber_g, sodium_mg, vitamin_c_mg, iron_mg)");
    lines.push(`VALUES (@ingredient_id, 100.00, ${sqlString(item.normalizedNutrition.basisUnit)}, ${sqlNumber(n.calories)}, ${sqlNumber(item.normalizedPrice.usdPerBasis)}, ${sqlNumber(n.proteinG)}, ${sqlNumber(n.fatG)}, ${sqlNumber(n.carbohydratesG)}, ${sqlNumber(n.sugarsG)}, ${sqlNumber(n.fiberG)}, ${sqlNumber(n.sodiumMg)}, ${sqlNumber(n.vitaminCMg)}, ${sqlNumber(n.ironMg)})`);
    lines.push("ON DUPLICATE KEY UPDATE basis=VALUES(basis), basis_unit=VALUES(basis_unit), calories_kcal=VALUES(calories_kcal), price=VALUES(price), protein_g=VALUES(protein_g), fat_g=VALUES(fat_g), carbs_g=VALUES(carbs_g), sugar_g=VALUES(sugar_g), fiber_g=VALUES(fiber_g), sodium_mg=VALUES(sodium_mg), vitamin_c_mg=VALUES(vitamin_c_mg), iron_mg=VALUES(iron_mg);");
    lines.push("");
  }
  return `${lines.join("\n")}\n`;
}

function referenceSql() {
  return `-- GENERATED FILE. Source of truth: data/DATASET_SPEC.md and data/tools/shared/diet-compatibility.mjs
INSERT INTO diets (id, name) VALUES
  (1, 'Vegetarian'),
  (2, 'Vegan'),
  (3, 'Pescatarian'),
  (4, 'Keto'),
  (5, 'Paleo'),
  (6, 'Flexitarian'),
  (7, 'Gluten Free'),
  (8, 'Lactose Free')
ON DUPLICATE KEY UPDATE name = VALUES(name);
`;
}

function instructionTitle(instruction) {
  const firstSentence = String(instruction).trim().split(/(?<=[.!?])\s+/u)[0];
  return /[.!?]$/u.test(firstSentence) ? firstSentence : `${firstSentence}.`;
}

function recipeSql(items, ingredientMap) {
  const lines = [
    "-- GENERATED FILE. Source of truth: data/recipes/*/recipe.json",
    "-- prep_time_minutes stores mandatory preparation + cooking time because the current schema has one time column.",
    "-- The image path is the requested stable recipe seed blob name.",
    "USE appetee;",
    "SET NAMES utf8mb4;",
    "",
  ];
  for (const { data: item } of items) {
    const instructions = item.instructions.map((instruction) => ({ title: instructionTitle(instruction), instruction }));
    lines.push(`-- ${item.seedId} ${item.name}`);
    lines.push("INSERT INTO recipes (name, image_blob_name, instructions, prep_time_minutes, servings, difficulty, estimated_cost_per_serving, calories_total, protein_total, carbs_total, created_at, updated_at)");
    lines.push(`VALUES (${sqlString(item.name)}, 'recipes/96ef8a25a7f4433e936a40e6aa6e33c0.avif', CAST(${sqlString(JSON.stringify(instructions))} AS JSON), ${item.times.totalMinutes}, ${item.servings}, ${sqlString(item.difficulty)}, ${sqlNumber(item.calculatedCost.perServingUsd)}, ${sqlNumber(item.calculatedNutrition.total.calories)}, ${sqlNumber(item.calculatedNutrition.total.proteinG)}, ${sqlNumber(item.calculatedNutrition.total.carbohydratesG)}, ${sqlString(item.createdAt.slice(0, 19).replace("T", " "))}, ${sqlString(item.createdAt.slice(0, 19).replace("T", " "))});`);
    lines.push("SET @recipe_id := LAST_INSERT_ID();");
    for (const usage of item.ingredients) {
      const ingredient = ingredientMap.get(usage.ingredientSeedId);
      lines.push(`INSERT INTO recipe_ingredients (recipe_id, ingredient_id, quantity, unit, note) VALUES (@recipe_id, (SELECT id FROM ingredients WHERE name = ${sqlString(ingredient.name)} LIMIT 1), ${Number(usage.normalizedQuantity).toFixed(3)}, ${sqlString(usage.normalizedUnit)}, ${sqlString(usage.sourceDisplay)});`);
    }
    for (const diet of item.diets) lines.push(`INSERT INTO diet_recipes (recipe_id, diet_id) VALUES (@recipe_id, (SELECT id FROM diets WHERE name = ${sqlString(diet)} LIMIT 1));`);
    for (const badge of item.badges) lines.push(`INSERT INTO recipe_badges (recipe_id, badge) VALUES (@recipe_id, ${sqlString(badge)});`);
    lines.push("");
  }
  return `${lines.join("\n")}\n`;
}

function distributionReport(validation) {
  return {
    generatedAt: validation.generatedAt,
    recipeCount: validation.counts.recipes,
    ingredientCount: validation.counts.ingredients,
    progressiveTargets: validation.diversityTargets,
    distributions: validation.distributions,
    nextBatchPriorityGaps: [
      "Add air-fryer, slow-cooker, grill, microwave, and additional no-cook recipes to reduce stovetop concentration.",
      "Continue non-chicken athlete proteins including pork, tempeh, beans, and additional fish.",
      "Add familiar Korean and other underrepresented cuisines while preserving source-domain diversity.",
      "Classify meal roles truthfully and track drift against the candidate plan's approximately 85% Main Meal and 15% combined other categories.",
      "Maintain Gluten Free and Lactose Free coverage using ingredient-level compatibility evidence.",
      "Keep new images pending during bulk growth and maintain both research/image owner handoff queues.",
    ],
  };
}

async function main() {
  const validation = await runValidation({ writeReport: true });
  if (!validation.valid) throw new Error(`SQL generation refused: validation has ${validation.errors.length} critical errors`);
  const ingredientItems = await records(path.join(dataDir, "ingredients"), "ingredient.json");
  const recipeItems = await records(path.join(dataDir, "recipes"), "recipe.json");
  const ingredientMap = new Map(ingredientItems.map((item) => [item.data.seedId, item.data]));
  const sqlDir = path.join(dataDir, "generated", "sql");
  const reportsDir = path.join(dataDir, "generated", "reports");
  await mkdir(sqlDir, { recursive: true });
  await mkdir(reportsDir, { recursive: true });

  const schemaSource = await readFile(path.join(repoDir, "scriptDatabase.sql"), "utf8");
  const badgeConstraint = "CHECK (badge IN ('High Protein', 'Low Calorie', 'Low Carb', 'High Fiber', 'Quick Meal', 'Meal Prep', 'Freezer Friendly', 'Budget Friendly', 'Few Ingredients'))";
  const schema = schemaSource.replace("CHECK (badge IN ('freezer-friendly', 'budget-focused', 'high-protein'))", badgeConstraint);
  if (schema === schemaSource) throw new Error("Could not locate the backend badge constraint while generating the dataset schema copy");
  await writeFile(path.join(sqlDir, "01-schema.sql"), `-- GENERATED FILE. Dataset schema copy generated from repository scriptDatabase.sql.\n-- Dataset badge constraint expanded to the canonical DATASET_SPEC.md badge vocabulary.\n${schema}`);
  await writeFile(path.join(sqlDir, "02-reference.sql"), referenceSql());
  await writeFile(path.join(sqlDir, "03-ingredients.sql"), ingredientSql(ingredientItems));
  await writeFile(path.join(sqlDir, "04-recipes.sql"), recipeSql(recipeItems, ingredientMap));

  const ingredientIndex = ingredientItems.map(({ directory, data: item }) => ({ seedId: item.seedId, name: item.name, normalizedName: item.name.trim().toLowerCase(), measurementType: item.measurementType, basisUnit: item.normalizedNutrition.basisUnit, normalizedPriceUsd: item.normalizedPrice.usdPerBasis, walmartProductId: item.market.productId, path: `ingredients/${directory}/ingredient.json` }));
  const recipeIndex = recipeItems.map(({ directory, data: item }) => ({ seedId: item.seedId, name: item.name, normalizedName: item.name.trim().toLowerCase(), candidateSequence: item.candidate?.sequence ?? null, candidateSeedId: item.candidate?.seedId ?? null, ingredientSeedIds: item.ingredients.map((usage) => usage.ingredientSeedId).sort(), countryOfOrigin: item.countryOfOrigin, sourceDomain: item.source.domain, discovery: item.discovery, studentAthleteTarget: item.studentAthleteTarget, totalMinutes: item.times.totalMinutes, mealType: item.mealType, mealCategory: item.mealCategory, cookingMethod: item.cookingMethod, primaryProtein: item.primaryProtein, carbohydrateBase: item.carbohydrateBase, diets: item.diets, badges: item.badges, costPerServingUsd: item.calculatedCost.perServingUsd, path: `recipes/${directory}/recipe.json` }));
  await writeFile(path.join(dataDir, "ingredients", "index.json"), json(ingredientIndex));
  await writeFile(path.join(dataDir, "recipes", "index.json"), json(recipeIndex));
  await writeFile(path.join(reportsDir, "distribution.json"), json(distributionReport(validation)));
  await syncImageQueues();
  process.stdout.write(`Generated schema, SQL, indexes, and distributions for ${recipeItems.length} recipes.\n`);
}

main().catch((error) => { process.stderr.write(`${error.stack ?? error}\n`); process.exitCode = 1; });

import { readdir, readFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";
import {
  getIngredientImageBlobName,
  getRecipeCardImageBlobName,
  getRecipeMainImageBlobName,
} from "../shared/blob-names.mjs";
import { backendRoot, loadDatabaseConfig, redactDatabaseTarget } from "../shared/tool-config.mjs";
import { createLocalDatabaseConnection, parseAndAssertSafeLocalTarget } from "./mysql-local.mjs";

const defaultDataDir = path.join(backendRoot, "data");

async function readCanonicalRecords(root, fileName) {
  const entries = (await readdir(root, { withFileTypes: true }))
    .filter((entry) => entry.isDirectory())
    .sort((left, right) => left.name.localeCompare(right.name));
  const records = [];
  for (const entry of entries) {
    const recordPath = path.join(root, entry.name, fileName);
    try {
      const text = (await readFile(recordPath, "utf8")).replace(/^\uFEFF/u, "");
      records.push(JSON.parse(text));
    } catch (error) {
      if (error?.code !== "ENOENT") throw error;
    }
  }
  return records;
}

function uniqueNameMap(records, entityType) {
  const result = new Map();
  for (const record of records) {
    if (result.has(record.name)) {
      throw new Error(`Canonical ${entityType} name is ambiguous: ${record.name}.`);
    }
    result.set(record.name, record);
  }
  return result;
}

export async function loadCanonicalExpectations({ dataDir = defaultDataDir } = {}) {
  const ingredients = await readCanonicalRecords(path.join(dataDir, "ingredients"), "ingredient.json");
  const recipes = await readCanonicalRecords(path.join(dataDir, "recipes"), "recipe.json");
  const ingredientByName = uniqueNameMap(ingredients, "ingredient");
  const recipeByName = uniqueNameMap(recipes, "recipe");

  return {
    counts: {
      ingredients: ingredients.length,
      ingredientNutrition: ingredients.length,
      recipes: recipes.length,
      recipeIngredients: recipes.reduce((total, recipe) => total + recipe.ingredients.length, 0),
      dietRecipes: recipes.reduce((total, recipe) => total + recipe.diets.length, 0),
      recipeBadges: recipes.reduce((total, recipe) => total + recipe.badges.length, 0),
    },
    ingredientImages: new Map([...ingredientByName].map(([name, ingredient]) => [
      name,
      getIngredientImageBlobName(ingredient.seedId),
    ])),
    recipeImages: new Map([...recipeByName].map(([name, recipe]) => [name, {
      main: getRecipeMainImageBlobName(recipe.seedId),
      card: getRecipeCardImageBlobName(recipe.seedId),
    }])),
  };
}

async function scalar(connection, sql) {
  const [rows] = await connection.query(sql);
  return Number(rows[0].Value);
}

function verifyImageRows(expected, actualRows, describeExpected) {
  const issues = [];
  const actualByName = new Map();
  for (const row of actualRows) {
    if (actualByName.has(row.Name)) issues.push(`duplicate database row for ${row.Name}`);
    actualByName.set(row.Name, row);
  }
  for (const [name, expectedValue] of expected) {
    const actual = actualByName.get(name);
    if (!actual) issues.push(`missing database row for ${name}`);
    else issues.push(...describeExpected(name, expectedValue, actual));
  }
  for (const name of actualByName.keys()) {
    if (!expected.has(name)) issues.push(`unexpected database row for ${name}`);
  }
  return issues;
}

export async function verifyLocalDatabase(
  target,
  {
    expectations,
    dataDir = defaultDataDir,
    openConnection = createLocalDatabaseConnection,
  } = {},
) {
  const expected = expectations ?? await loadCanonicalExpectations({ dataDir });
  const connection = await openConnection(target);
  try {
    const actualCounts = {
      ingredients: await scalar(connection, "SELECT COUNT(*) AS Value FROM ingredients"),
      ingredientNutrition: await scalar(connection, "SELECT COUNT(*) AS Value FROM ingredient_nutrition"),
      recipes: await scalar(connection, "SELECT COUNT(*) AS Value FROM recipes"),
      recipeIngredients: await scalar(connection, "SELECT COUNT(*) AS Value FROM recipe_ingredients"),
      dietRecipes: await scalar(connection, "SELECT COUNT(*) AS Value FROM diet_recipes"),
      recipeBadges: await scalar(connection, "SELECT COUNT(*) AS Value FROM recipe_badges"),
    };

    const countIssues = Object.entries(expected.counts)
      .filter(([key, count]) => actualCounts[key] !== count)
      .map(([key, count]) => `${key}: expected ${count}, found ${actualCounts[key]}`);

    const orphanCount = await scalar(connection, `
      SELECT COUNT(*) AS Value FROM (
        SELECT n.ingredient_id FROM ingredient_nutrition n LEFT JOIN ingredients i ON i.id = n.ingredient_id WHERE i.id IS NULL
        UNION ALL
        SELECT ri.recipe_id FROM recipe_ingredients ri LEFT JOIN recipes r ON r.id = ri.recipe_id LEFT JOIN ingredients i ON i.id = ri.ingredient_id WHERE r.id IS NULL OR i.id IS NULL
        UNION ALL
        SELECT dr.recipe_id FROM diet_recipes dr LEFT JOIN recipes r ON r.id = dr.recipe_id LEFT JOIN diets d ON d.id = dr.diet_id WHERE r.id IS NULL OR d.id IS NULL
        UNION ALL
        SELECT rb.recipe_id FROM recipe_badges rb LEFT JOIN recipes r ON r.id = rb.recipe_id WHERE r.id IS NULL
      ) orphan_rows
    `);

    const [ingredientRows] = await connection.query(
      "SELECT name AS Name, image_blob_name AS ImageBlobName FROM ingredients ORDER BY id",
    );
    const [recipeRows] = await connection.query(
      "SELECT name AS Name, image_blob_name AS MainImageBlobName, card_image_blob_name AS CardImageBlobName FROM recipes ORDER BY id",
    );
    const imageIssues = [
      ...verifyImageRows(expected.ingredientImages, ingredientRows, (name, blobName, actual) =>
        actual.ImageBlobName === blobName ? [] : [`ingredient image mismatch for ${name}`]),
      ...verifyImageRows(expected.recipeImages, recipeRows, (name, blobNames, actual) => {
        const issues = [];
        if (actual.MainImageBlobName !== blobNames.main) issues.push(`recipe main image mismatch for ${name}`);
        if (actual.CardImageBlobName !== blobNames.card) issues.push(`recipe card image mismatch for ${name}`);
        return issues;
      }),
    ];

    const legacyImageCount = await scalar(connection, `
      SELECT (
        SELECT COUNT(*) FROM ingredients WHERE image_blob_name = 'ingredients/chicken-breast-seed.avif'
      ) + (
        SELECT COUNT(*) FROM recipes WHERE image_blob_name = 'recipes/96ef8a25a7f4433e936a40e6aa6e33c0.avif'
      ) AS Value
    `);

    const invalidRecipeReadinessCount = await scalar(connection, `
      SELECT COUNT(*) AS Value
      FROM recipes
      WHERE CHAR_LENGTH(TRIM(description)) = 0
         OR prep_time_minutes < 0
         OR cook_time_minutes < 0
         OR total_time_minutes <= 0
         OR total_time_minutes < prep_time_minutes
         OR total_time_minutes < cook_time_minutes
         OR calories_per_serving < 0
         OR protein_per_serving < 0
    `);
    const invalidDisplayOrderCount = await scalar(connection, `
      SELECT COUNT(*) AS Value
      FROM (
        SELECT recipe_id
        FROM recipe_ingredients
        GROUP BY recipe_id
        HAVING MIN(display_order) <> 1
           OR MAX(display_order) <> COUNT(*)
           OR COUNT(DISTINCT display_order) <> COUNT(*)
      ) invalid_display_order
    `);
    const invalidFeaturedOrderCount = await scalar(connection, `
      SELECT COUNT(*) AS Value
      FROM (
        SELECT recipe_id
        FROM recipe_ingredients
        GROUP BY recipe_id
        HAVING COUNT(featured_order) NOT BETWEEN 1 AND 3
           OR COUNT(DISTINCT featured_order) <> COUNT(featured_order)
           OR MIN(featured_order) < 1
           OR MAX(featured_order) > 3
      ) invalid_featured_order
    `);
    const legacyBadgeCount = await scalar(connection, `
      SELECT COUNT(*) AS Value
      FROM recipe_badges
      WHERE badge IN ('freezer-friendly', 'budget-focused', 'high-protein')
    `);

    const issues = [...countIssues];
    if (orphanCount !== 0) issues.push(`orphan relationships: ${orphanCount}`);
    if (legacyImageCount !== 0) issues.push(`legacy image Blob names: ${legacyImageCount}`);
    if (invalidRecipeReadinessCount !== 0) issues.push(`recipes missing F-008 readiness data: ${invalidRecipeReadinessCount}`);
    if (invalidDisplayOrderCount !== 0) issues.push(`recipes with invalid ingredient display order: ${invalidDisplayOrderCount}`);
    if (invalidFeaturedOrderCount !== 0) issues.push(`recipes with invalid featured ingredient order: ${invalidFeaturedOrderCount}`);
    if (legacyBadgeCount !== 0) issues.push(`legacy recipe badge values: ${legacyBadgeCount}`);
    issues.push(...imageIssues);
    if (issues.length > 0) {
      throw new Error(`Local database verification failed:\n- ${issues.slice(0, 20).join("\n- ")}${issues.length > 20 ? `\n- ...and ${issues.length - 20} more` : ""}`);
    }

    return {
      counts: actualCounts,
      orphanCount,
      legacyImageCount,
      invalidRecipeReadinessCount,
      invalidDisplayOrderCount,
      invalidFeaturedOrderCount,
      legacyBadgeCount,
      imageMappingsPassed: true,
    };
  } finally {
    await connection.end();
  }
}

export function printVerification(result, write = console.log) {
  write(`  Ingredients: ${result.counts.ingredients}`);
  write(`  Recipes: ${result.counts.recipes}`);
  write(`  Recipe ingredients: ${result.counts.recipeIngredients}`);
  write(`  Diet relationships: ${result.counts.dietRecipes}`);
  write(`  Badge relationships: ${result.counts.recipeBadges}`);
  write("  Relationships: passed");
  write("  Image mappings: passed");
  write("  F-008 recipe readiness: passed");
}

export async function main() {
  console.log("Appetee local database verification\n");
  const { connectionString } = await loadDatabaseConfig();
  const target = parseAndAssertSafeLocalTarget(connectionString);
  console.log(`Target: ${redactDatabaseTarget(target)}`);
  const result = await verifyLocalDatabase(target);
  printVerification(result);
  console.log("\nLocal database verification passed.");
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  main().catch((error) => {
    console.error(error?.message ?? String(error));
    process.exitCode = 1;
  });
}

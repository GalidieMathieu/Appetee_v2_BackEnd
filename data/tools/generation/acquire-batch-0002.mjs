import { mkdir, readFile, readdir, writeFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { applyDerivedDiets, dietCompatibilityForIngredient } from "../diet-compatibility.mjs";
import { canonicalMealCategory } from "../meal-categories.mjs";

const toolDir = path.dirname(fileURLToPath(import.meta.url));
const dataDir = path.resolve(toolDir, "..", "..");
const batchModule = process.argv[2] ?? "./batch-0002.mjs";
const { ingredients: ingredientInputs, recipes: recipeInputs, candidateEvents = null } = await import(batchModule);
const round = (value, places = 3) => Number(Number(value).toFixed(places));
const slug = (value) => value.replace(/[^a-z0-9]+/gi, "_").replace(/^_|_$/g, "");
const json = (value) => `${JSON.stringify(value, null, 2)}\n`;
const srArchiveUrl = "https://fdc.nal.usda.gov/fdc-datasets/FoodData_Central_sr_legacy_food_json_2018-04.zip";
const srJsonPath = path.join(dataDir, "research", "cache", "sr-legacy", "FoodData_Central_sr_legacy_food_json_2018-04.json");

const nutrientSpecs = {
  calories: { ids: [1008, 2047, 2048], names: ["Energy"] },
  proteinG: { ids: [1003], names: ["Protein"] },
  carbohydratesG: { ids: [1005], names: ["Carbohydrate, by difference"] },
  fatG: { ids: [1004], names: ["Total lipid (fat)"] },
  fiberG: { ids: [1079], names: ["Fiber, total dietary"] },
  sugarsG: { ids: [2000], names: ["Sugars, total including NLEA", "Sugars, total"] },
  sodiumMg: { ids: [1093], names: ["Sodium, Na"] },
  potassiumMg: { ids: [1092], names: ["Potassium, K"] },
  calciumMg: { ids: [1087], names: ["Calcium, Ca"] },
  ironMg: { ids: [1089], names: ["Iron, Fe"] },
  magnesiumMg: { ids: [1090], names: ["Magnesium, Mg"] },
  vitaminCMg: { ids: [1162], names: ["Vitamin C, total ascorbic acid"] },
};

function extractNutrition(food, input) {
  const values = {};
  for (const [field, spec] of Object.entries(nutrientSpecs)) {
    let item = food.foodNutrients.find((entry) => spec.ids.includes(entry.nutrient.id));
    if (!item) item = food.foodNutrients.find((entry) => spec.names.includes(entry.nutrient.name));
    if (field === "calories" && item?.nutrient.unitName?.toUpperCase() !== "KCAL") {
      item = food.foodNutrients.find((entry) => spec.ids.includes(entry.nutrient.id) && entry.nutrient.unitName?.toUpperCase() === "KCAL");
    }
    if (item?.amount !== undefined && Number.isFinite(Number(item.amount))) values[field] = round(Number(item.amount));
  }
  if (!Number.isFinite(values.calories) || !Number.isFinite(values.proteinG)) throw new Error(`${input.seedId} USDA record lacks mandatory nutrition`);
  return values;
}

async function loadExistingIngredients() {
  const records = [];
  const entries = (await readdir(path.join(dataDir, "ingredients"), { withFileTypes: true })).filter((entry) => entry.isDirectory());
  for (const entry of entries) {
    try { records.push(JSON.parse(await readFile(path.join(dataDir, "ingredients", entry.name, "ingredient.json"), "utf8"))); } catch {}
  }
  return records;
}

async function makeIngredientRecords() {
  const payload = JSON.parse(await readFile(srJsonPath, "utf8"));
  return ingredientInputs.map((input) => {
    const food = payload.SRLegacyFoods.find((item) => item.fdcId === input.preferredFdcId);
    if (!food) throw new Error(`Missing preferred USDA record ${input.preferredFdcId} for ${input.seedId}`);
    const densityGPerMl = input.densityGPerMl ?? null;
    const basisUnit = input.nutritionBasisUnit ?? "g";
    const nutrition = extractNutrition(food, input);
    if (basisUnit === "ml") {
      if (!Number.isFinite(densityGPerMl) || densityGPerMl <= 0) throw new Error(`${input.seedId} liquid nutrition requires densityGPerMl`);
      for (const key of Object.keys(nutrition)) nutrition[key] = round(nutrition[key] * densityGPerMl);
    }
    const normalizedPrice = round(input.market.packagePriceUsd / input.market.packageQuantity * 100, 4);
    return {
      seedId: input.seedId,
      name: input.name,
      canonicalName: input.name,
      description: input.description,
      measurementType: input.measurementType,
      dietCompatibility: dietCompatibilityForIngredient(input.seedId, input.dietCompatibility),
      market: input.market,
      originalNutritionLabel: null,
      normalizedNutrition: { basisQuantity: 100, basisUnit, values: nutrition },
      normalizedPrice: { basisQuantity: 100, basisUnit, usdPerBasis: normalizedPrice },
      nutritionSources: [{
        authority: "USDA FoodData Central",
        fdcId: food.fdcId,
        dataType: food.dataType,
        description: food.description,
        url: `https://fdc.nal.usda.gov/fdc-app.html#/food-details/${food.fdcId}/nutrients`,
        searchQuery: input.fdcSearchQuery,
        accessedAt: "2026-08-10T12:00:00-06:00",
        note: input.conversionNotes,
      }],
      fallbackSourceUrls: [srArchiveUrl],
      conversion: { densityGPerMl, notes: input.conversionNotes },
      image: {
        path: null,
        imageType: "external-real-product-photograph",
        aiGenerated: false,
        provenance: "Real product image pending.",
        needsGeneratedImage: true,
        replacementReason: "Listed in research/image/ingredients.json; AI generation is prohibited.",
      },
      futureApiResponseExample: {
        id: input.seedId,
        name: input.name,
        description: input.description,
        measurement: { type: input.measurementType, basisQuantity: 100, basisUnit },
        nutrition,
        price: { usdPerBasis: normalizedPrice, basisQuantity: 100, basisUnit },
        image: null,
      },
    };
  });
}

function calculateRecipe(recipe, ingredientsById) {
  const mealCategory = canonicalMealCategory(recipe.seedId, recipe.mealCategory);
  const totalNutrition = {};
  let totalCostUsd = 0;
  for (const usage of recipe.ingredients) {
    const ingredient = ingredientsById.get(usage.ingredientSeedId);
    if (!ingredient) throw new Error(`${recipe.seedId} references missing ${usage.ingredientSeedId}`);
    if (usage.normalizedUnit !== ingredient.normalizedNutrition.basisUnit) throw new Error(`${recipe.seedId}/${usage.ingredientSeedId} basis-unit mismatch`);
    const factor = usage.normalizedQuantity / ingredient.normalizedNutrition.basisQuantity;
    for (const [key, value] of Object.entries(ingredient.normalizedNutrition.values)) totalNutrition[key] = (totalNutrition[key] ?? 0) + value * factor;
    totalCostUsd += ingredient.normalizedPrice.usdPerBasis * factor;
  }
  for (const key of Object.keys(totalNutrition)) totalNutrition[key] = round(totalNutrition[key]);
  const perServingNutrition = Object.fromEntries(Object.entries(totalNutrition).map(([key, value]) => [key, round(value / recipe.servings)]));
  totalCostUsd = round(totalCostUsd, 2);
  const costPerServingUsd = round(totalCostUsd / recipe.servings, 2);
  const badges = [];
  if (perServingNutrition.proteinG >= 30) badges.push("High Protein");
  if ((totalNutrition.calories / recipe.estimatedFinishedWeightG) * 100 <= 120) badges.push("Low Calorie");
  if (perServingNutrition.carbohydratesG !== undefined && perServingNutrition.carbohydratesG <= 30) badges.push("Low Carb");
  if (perServingNutrition.fiberG !== undefined && perServingNutrition.fiberG >= 5.6) badges.push("High Fiber");
  if (recipe.times.totalMinutes <= 30) badges.push("Quick Meal");
  if (recipe.badgeJudgements.mealPrep) badges.push("Meal Prep");
  if (recipe.badgeJudgements.freezerFriendly) badges.push("Freezer Friendly");
  if (costPerServingUsd <= 4) badges.push("Budget Friendly");
  if (recipe.ingredients.length <= 5) badges.push("Few Ingredients");
  const createdAt = new Date(Date.UTC(2024, 0, 3, 17, 0, 0) + (Number(recipe.seedId.slice(4)) - 1) * 37 * 60 * 60 * 1000).toISOString();
  const diets = applyDerivedDiets(recipe, ingredientsById);
  return {
    ...recipe,
    mealCategory,
    diets,
    calculatedNutrition: {
      total: totalNutrition,
      perServing: perServingNutrition,
      calculationMethod: "Sum of canonical ingredient values multiplied by normalized quantity / 100; no intermediate rounding; displayed values rounded to 0.001.",
    },
    calculatedCost: {
      totalUsd: totalCostUsd,
      perServingUsd: costPerServingUsd,
      calculationMethod: "Sum of canonical normalized ingredient prices multiplied by normalized quantity / 100.",
    },
    badges,
    createdAt,
    image: {
      mainPath: null,
      cardPath: null,
      imageType: "external-real-photograph",
      aiGenerated: false,
      provenance: "Real dish photograph pending.",
      needsGeneratedImage: true,
      replacementReason: "Listed in research/image/recipes.json; AI generation is prohibited.",
    },
    futureApiResponseExample: {
      id: recipe.seedId,
      name: recipe.name,
      description: recipe.description,
      mealType: recipe.mealType,
      mealCategory,
      image: null,
      totalTimeMinutes: recipe.times.totalMinutes,
      servings: recipe.servings,
      nutritionPerServing: perServingNutrition,
      estimatedCostPerServingUsd: costPerServingUsd,
      diets,
      badges,
    },
  };
}

async function main() {
  const batchIngredientIds = new Set(ingredientInputs.map((item) => item.seedId));
  const existingIngredients = (await loadExistingIngredients()).filter((item) => !batchIngredientIds.has(item.seedId));
  const newIngredients = await makeIngredientRecords();
  const allIngredients = [...existingIngredients, ...newIngredients];
  const ingredientsById = new Map(allIngredients.map((record) => [record.seedId, record]));
  if (ingredientsById.size !== allIngredients.length) throw new Error("Duplicate ingredient seed ID detected before write");
  const recipeRecords = recipeInputs.map((input) => calculateRecipe(input, ingredientsById));
  if (new Set(recipeRecords.map((item) => item.seedId)).size !== recipeRecords.length) throw new Error("Duplicate recipe seed ID detected before write");

  for (const record of newIngredients) {
    const directory = `${record.seedId.slice(4)}_${slug(record.name)}`;
    const target = path.join(dataDir, "ingredients", directory);
    await mkdir(target, { recursive: true });
    await writeFile(path.join(target, "ingredient.json"), json(record));
  }
  for (const record of recipeRecords) {
    const directory = `${record.seedId.slice(4)}_${slug(record.name)}`;
    const target = path.join(dataDir, "recipes", directory);
    await mkdir(target, { recursive: true });
    await writeFile(path.join(target, "recipe.json"), json(record));
  }
  if (candidateEvents) {
    const progressPath = path.join(dataDir, "progress.json");
    const progress = JSON.parse(await readFile(progressPath, "utf8"));
    const acquisition = progress.candidateAcquisition ?? {};
    const mergeEvents = (previous = [], incoming = []) => {
      const bySequence = new Map(previous.map((entry) => [entry.sequence, entry]));
      for (const entry of incoming) bySequence.set(entry.sequence, entry);
      return [...bySequence.values()].sort((a, b) => a.sequence - b.sequence);
    };
    acquisition.skippedCandidates = mergeEvents(acquisition.skippedCandidates, candidateEvents.skippedCandidates);
    acquisition.unresolvedCandidates = mergeEvents(acquisition.unresolvedCandidates, candidateEvents.unresolvedCandidates);
    progress.candidateAcquisition = acquisition;
    await writeFile(progressPath, json(progress));
  }
  process.stdout.write(`Acquired ${newIngredients.length} new ingredients and ${recipeRecords.length} recipes.\n`);
}

main().catch((error) => {
  process.stderr.write(`${error.stack ?? error}\n`);
  process.exitCode = 1;
});

import { mkdir, readFile, writeFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { ingredients as ingredientInputs, recipes as recipeInputs } from "./batch-0001.mjs";
import { applyDerivedDiets, dietCompatibilityForIngredient } from "../diet-compatibility.mjs";
import { canonicalMealCategory } from "../meal-categories.mjs";

const toolDir = path.dirname(fileURLToPath(import.meta.url));
const dataDir = path.resolve(toolDir, "..", "..");
const round = (value, places = 3) => Number(Number(value).toFixed(places));
const slug = (value) => value.replace(/[^a-z0-9]+/gi, "_").replace(/^_|_$/g, "");
const json = (value) => `${JSON.stringify(value, null, 2)}\n`;
const srArchiveUrl = "https://fdc.nal.usda.gov/fdc-datasets/FoodData_Central_sr_legacy_food_json_2018-04.zip";
const srJsonPath = path.join(dataDir, "research", "cache", "sr-legacy", "FoodData_Central_sr_legacy_food_json_2018-04.json");
let srFoods;

const nutrientSpecs = {
  calories: { ids: [1008, 2047, 2048], names: ["Energy"], unit: "KCAL" },
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

const typeScore = { Foundation: 70, "SR Legacy": 60, "Survey (FNDDS)": 35, Branded: 5 };

function selectFood(foods, input) {
  if (input.preferredFdcId) {
    const preferred = foods.find((food) => food.fdcId === input.preferredFdcId);
    if (preferred) return preferred;
  }
  const queryTokens = input.fdcSearchQuery.toLowerCase().split(/\W+/).filter((token) => token.length > 2);
  const ranked = foods.map((food) => {
    const description = food.description.toLowerCase();
    let score = typeScore[food.dataType] ?? 0;
    score += queryTokens.filter((token) => description.includes(token)).length * 6;
    score += input.fdcDescriptionIncludes.filter((token) => description.includes(token.toLowerCase())).length * 20;
    if (queryTokens.includes("raw") && description.includes("raw")) score += 25;
    if (queryTokens.includes("frozen") && description.includes("frozen")) score += 15;
    if (queryTokens.includes("canned") && description.includes("canned")) score += 15;
    if (!queryTokens.includes("cooked") && description.includes("cooked")) score -= 12;
    if (!queryTokens.includes("prepared") && description.includes("prepared")) score -= 8;
    return { food, score };
  }).sort((a, b) => b.score - a.score || a.food.fdcId - b.food.fdcId);
  return ranked[0]?.food;
}

function extractNutrition(food, input) {
  const values = {};
  for (const [field, spec] of Object.entries(nutrientSpecs)) {
    let item = food.foodNutrients.find((n) => spec.ids.includes(n.nutrientId));
    if (!item) item = food.foodNutrients.find((n) => spec.names.includes(n.nutrientName));
    if (field === "calories" && item && item.unitName?.toUpperCase() !== "KCAL") {
      item = food.foodNutrients.find((n) => spec.ids.includes(n.nutrientId) && n.unitName?.toUpperCase() === "KCAL");
    }
    if (item?.value !== undefined && item.value !== null && Number.isFinite(Number(item.value))) {
      const densityFactor = input.measurementType === "liquid" ? input.densityGPerMl : 1;
      values[field] = round(Number(item.value) * densityFactor);
    }
  }
  if (!Number.isFinite(values.calories) || !Number.isFinite(values.proteinG)) {
    throw new Error(`${input.seedId} selected USDA food ${food.fdcId} without mandatory calories/protein`);
  }
  return values;
}

async function fetchNutrition(input) {
  const url = new URL("https://api.nal.usda.gov/fdc/v1/foods/search");
  url.searchParams.set("api_key", process.env.USDA_API_KEY || "DEMO_KEY");
  url.searchParams.set("query", input.fdcSearchQuery);
  url.searchParams.set("pageSize", "25");
  let response;
  for (let attempt = 1; attempt <= 6; attempt += 1) {
    response = await fetch(url, { headers: { "User-Agent": "Appetee-Dataset-Research/0.2" } });
    if (response.status !== 429) break;
    await new Promise((resolve) => setTimeout(resolve, attempt * 5000));
  }
  if (!response?.ok) throw new Error(`USDA search failed for ${input.name}: ${response?.status ?? "no response"}`);
  const payload = await response.json();
  const selected = selectFood(payload.foods ?? [], input);
  if (!selected) throw new Error(`No USDA candidate for ${input.seedId} ${input.name}`);
  return { selected, nutrition: extractNutrition(selected, input), searchUrl: url.toString().replace(/api_key=[^&]+/, "api_key=REDACTED") };
}

async function localNutrition(input) {
  if (!srFoods) {
    const payload = JSON.parse(await readFile(srJsonPath, "utf8"));
    srFoods = payload.SRLegacyFoods.map((food) => ({
      fdcId: food.fdcId,
      description: food.description,
      dataType: food.dataType,
      foodNutrients: food.foodNutrients.map((item) => ({
        nutrientId: item.nutrient.id,
        nutrientName: item.nutrient.name,
        unitName: item.nutrient.unitName,
        value: item.amount,
      })),
    }));
  }
  const selected = selectFood(srFoods, input);
  if (!selected) throw new Error(`No local USDA SR Legacy candidate for ${input.seedId} ${input.name}`);
  return { selected, nutrition: extractNutrition(selected, input), searchUrl: srArchiveUrl, sourceKind: "USDA SR Legacy April 2018 JSON archive" };
}

function ingredientDirectory(record) {
  return `${record.seedId.slice(4)}_${slug(record.name)}`;
}

function recipeDirectory(record) {
  return `${record.seedId.slice(4)}_${slug(record.name)}`;
}

function calculateRecipe(recipe, ingredientsById) {
  const mealCategory = canonicalMealCategory(recipe.seedId, recipe.mealCategory);
  const totalNutrition = {};
  let totalCostUsd = 0;
  for (const usage of recipe.ingredients) {
    const ingredient = ingredientsById.get(usage.ingredientSeedId);
    if (!ingredient) throw new Error(`${recipe.seedId} references missing ${usage.ingredientSeedId}`);
    if (usage.normalizedUnit !== ingredient.normalizedNutrition.basisUnit) {
      throw new Error(`${recipe.seedId}/${usage.ingredientSeedId} unit ${usage.normalizedUnit} does not match ${ingredient.normalizedNutrition.basisUnit}`);
    }
    const factor = usage.normalizedQuantity / ingredient.normalizedNutrition.basisQuantity;
    for (const [key, value] of Object.entries(ingredient.normalizedNutrition.values)) {
      totalNutrition[key] = (totalNutrition[key] ?? 0) + value * factor;
    }
    totalCostUsd += ingredient.normalizedPrice.usdPerBasis * factor;
  }
  for (const key of Object.keys(totalNutrition)) totalNutrition[key] = round(totalNutrition[key]);
  const perServingNutrition = Object.fromEntries(Object.entries(totalNutrition).map(([key, value]) => [key, round(value / recipe.servings)]));
  const costPerServingUsd = round(totalCostUsd / recipe.servings, 2);
  totalCostUsd = round(totalCostUsd, 2);
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
      provenance: "Real product image pending; AI generation is prohibited.",
      needsGeneratedImage: true,
      replacementReason: "Listed in research/image/recipes.json; do not generate through Codex.",
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
  await mkdir(path.join(dataDir, "ingredients"), { recursive: true });
  await mkdir(path.join(dataDir, "recipes"), { recursive: true });
  await mkdir(path.join(dataDir, "research", "cache"), { recursive: true });

  const cachePath = path.join(dataDir, "research", "cache", "usda-batch-0001.json");
  let cache = {};
  try { cache = JSON.parse(await readFile(cachePath, "utf8")); } catch {}
  const ingredientRecords = [];

  for (const input of ingredientInputs) {
    let result = cache[input.seedId];
    if (input.preferredFdcId && result?.selected?.fdcId !== input.preferredFdcId) result = undefined;
    if (!result) {
      try {
        result = await localNutrition(input);
      } catch (localError) {
        await new Promise((resolve) => setTimeout(resolve, 1200));
        result = await fetchNutrition(input);
      }
      cache[input.seedId] = result;
      await writeFile(cachePath, json(cache));
    }
    const selected = result.selected;
    const basisUnit = input.measurementType === "liquid" ? "ml" : "g";
    const normalizedPrice = round(input.market.packagePriceUsd / input.market.packageQuantity * 100, 4);
    const record = {
      seedId: input.seedId,
      name: input.name,
      canonicalName: input.name,
      description: input.description,
      measurementType: input.measurementType,
      dietCompatibility: dietCompatibilityForIngredient(input.seedId, input.dietCompatibility),
      market: input.market,
      originalNutritionLabel: null,
      normalizedNutrition: {
        basisQuantity: 100,
        basisUnit,
        values: result.nutrition,
      },
      normalizedPrice: {
        basisQuantity: 100,
        basisUnit,
        usdPerBasis: normalizedPrice,
      },
      nutritionSources: [{
        authority: "USDA FoodData Central",
        fdcId: selected.fdcId,
        dataType: selected.dataType,
        description: selected.description,
        url: `https://fdc.nal.usda.gov/fdc-app.html#/food-details/${selected.fdcId}/nutrients`,
        searchQuery: input.fdcSearchQuery,
        accessedAt: "2026-08-10T12:00:00-06:00",
        note: input.seedId === "ING-0043" ? "Nutrition proxy uses USDA paprika because no generic berbere record was available; product and price are not proxied." : null,
      }],
      fallbackSourceUrls: [result.searchUrl],
      conversion: { densityGPerMl: input.densityGPerMl ?? null, notes: input.conversionNotes },
      image: {
        path: null,
        provenance: "Real dish photograph pending; AI generation is prohibited.",
        needsGeneratedImage: true,
        replacementReason: "Listed in research/image/ingredients.json; do not generate through Codex.",
      },
      futureApiResponseExample: {
        id: input.seedId,
        name: input.name,
        description: input.description,
        measurement: { type: input.measurementType, basisQuantity: 100, basisUnit },
        nutrition: result.nutrition,
        price: { usdPerBasis: normalizedPrice, basisQuantity: 100, basisUnit },
        image: null,
      },
    };
    const dir = path.join(dataDir, "ingredients", ingredientDirectory(record));
    await mkdir(dir, { recursive: true });
    await writeFile(path.join(dir, "ingredient.json"), json(record));
    ingredientRecords.push(record);
  }

  const ingredientsById = new Map(ingredientRecords.map((record) => [record.seedId, record]));
  const recipeRecords = [];
  for (const input of recipeInputs) {
    const record = calculateRecipe(input, ingredientsById);
    const dir = path.join(dataDir, "recipes", recipeDirectory(record));
    await mkdir(dir, { recursive: true });
    await writeFile(path.join(dir, "recipe.json"), json(record));
    recipeRecords.push(record);
  }
  await writeFile(cachePath, json(cache));
  process.stdout.write(`Acquired ${ingredientRecords.length} ingredients and ${recipeRecords.length} recipes.\n`);
}

main().catch((error) => {
  process.stderr.write(`${error.stack ?? error}\n`);
  process.exitCode = 1;
});

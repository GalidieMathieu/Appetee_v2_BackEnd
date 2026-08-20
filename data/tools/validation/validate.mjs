import { createHash } from "node:crypto";
import { readdir, readFile, stat, writeFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import sharp from "sharp";
import { allowedDietNames, deriveRestrictionDiets, derivedDietNames } from "../shared/diet-compatibility.mjs";
import { allowedMealCategoryNames, mainMealTargetPercentage, nonMainMealTargetPercentage } from "../shared/meal-categories.mjs";
import { loadCandidates } from "../generation/candidate-progress.mjs";

const toolDir = path.dirname(fileURLToPath(import.meta.url));
const dataDir = path.resolve(toolDir, "..", "..");
const allowedDiets = new Set(allowedDietNames);
const allowedBadges = new Set(["High Protein", "Low Calorie", "Low Carb", "High Fiber", "Quick Meal", "Meal Prep", "Freezer Friendly", "Budget Friendly", "Few Ingredients"]);
const allowedMealTypes = new Set(["Breakfast", "Lunch", "Dinner"]);
const allowedMealCategories = new Set(allowedMealCategoryNames);
const round = (value, places = 3) => Number(Number(value).toFixed(places));

async function loadRecords(root, jsonName) {
  const entries = (await readdir(root, { withFileTypes: true })).filter((entry) => entry.isDirectory()).sort((a, b) => a.name.localeCompare(b.name));
  const records = [];
  for (const entry of entries) {
    const file = path.join(root, entry.name, jsonName);
    try {
      records.push({ dirName: entry.name, dir: path.dirname(file), file, data: JSON.parse(await readFile(file, "utf8")) });
    } catch (error) {
      records.push({ dirName: entry.name, dir: path.dirname(file), file, parseError: error.message });
    }
  }
  return records;
}

function increment(map, key) { map[key ?? "(missing)"] = (map[key ?? "(missing)"] ?? 0) + 1; }
function error(report, code, message, record = null) { report.errors.push({ code, message, record }); }
function warning(report, code, message, record = null) { report.warnings.push({ code, message, record }); }
function near(a, b, tolerance = 0.011) { return Number.isFinite(a) && Number.isFinite(b) && Math.abs(a - b) <= tolerance; }

async function validateImage(report, recordId, file, width, height, maxBytes, allowMissing = false) {
  try {
    const buffer = await readFile(file);
    const info = await stat(file);
    const metadata = await sharp(buffer).metadata();
    if (metadata.format !== "heif" || metadata.compression !== "av1") error(report, "IMAGE_FORMAT", `${file} is not AVIF`, recordId);
    if (metadata.width !== width || metadata.height !== height) error(report, "IMAGE_DIMENSIONS", `${file} is ${metadata.width}x${metadata.height}; expected ${width}x${height}`, recordId);
    if (info.size > maxBytes) error(report, "IMAGE_SIZE", `${file} is ${info.size} bytes; maximum ${maxBytes}`, recordId);
    return createHash("sha256").update(buffer).digest("hex");
  } catch (cause) {
    if (allowMissing && cause.code === "ENOENT") return null;
    error(report, "IMAGE_MISSING_OR_INVALID", `${file}: ${cause.message}`, recordId);
    return null;
  }
}

function expectedBadges(recipe) {
  const nutrition = recipe.calculatedNutrition.perServing;
  const total = recipe.calculatedNutrition.total;
  const badges = [];
  if (nutrition.proteinG >= 30) badges.push("High Protein");
  if ((total.calories / recipe.estimatedFinishedWeightG) * 100 <= 120) badges.push("Low Calorie");
  if (nutrition.carbohydratesG !== undefined && nutrition.carbohydratesG <= 30) badges.push("Low Carb");
  if (nutrition.fiberG !== undefined && nutrition.fiberG >= 5.6) badges.push("High Fiber");
  if (recipe.times.totalMinutes <= 30) badges.push("Quick Meal");
  if (recipe.badgeJudgements.mealPrep) badges.push("Meal Prep");
  if (recipe.badgeJudgements.freezerFriendly) badges.push("Freezer Friendly");
  if (recipe.calculatedCost.perServingUsd <= 4) badges.push("Budget Friendly");
  if (recipe.ingredients.length <= 5) badges.push("Few Ingredients");
  return badges;
}

function recalculate(recipe, ingredientMap) {
  const total = {};
  let cost = 0;
  for (const usage of recipe.ingredients) {
    const ingredient = ingredientMap.get(usage.ingredientSeedId);
    if (!ingredient) continue;
    const factor = usage.normalizedQuantity / 100;
    for (const [key, value] of Object.entries(ingredient.normalizedNutrition.values)) total[key] = (total[key] ?? 0) + value * factor;
    cost += ingredient.normalizedPrice.usdPerBasis * factor;
  }
  return {
    total: Object.fromEntries(Object.entries(total).map(([key, value]) => [key, round(value)])),
    perServing: Object.fromEntries(Object.entries(total).map(([key, value]) => [key, round(value / recipe.servings)])),
    totalCost: round(cost, 2),
    servingCost: round(cost / recipe.servings, 2),
  };
}

export async function runValidation({ writeReport = true } = {}) {
  const report = {
    contract: "data/DATASET_SPEC.md",
    generatedAt: "2026-08-10T12:00:00-06:00",
    valid: false,
    errors: [], warnings: [],
    counts: { ingredients: 0, recipes: 0, candidateRecipes: 0, pendingIngredientImages: 0, pendingRecipeImages: 0 },
    distributions: { diets: {}, badges: {}, countries: {}, sourceDomains: {}, athleteTarget: {}, discovery: {}, primaryProteins: {}, carbohydrateBases: {}, mealTypes: {}, mealCategories: {}, cookingMethods: {}, timeBuckets: {}, costBuckets: {} },
    diversityTargets: {},
    pendingImageGeneration: { ingredients: [], recipes: [] },
  };
  const ingredientFiles = await loadRecords(path.join(dataDir, "ingredients"), "ingredient.json");
  const recipeFiles = await loadRecords(path.join(dataDir, "recipes"), "recipe.json");
  const candidates = await loadCandidates();
  const candidateBySequence = new Map(candidates.map((candidate) => [candidate.sequence, candidate]));
  const completedCandidateSequences = new Set();
  const ingredientMap = new Map();
  const ingredientImageHashes = new Map();
  const names = new Set();

  for (const item of ingredientFiles) {
    if (item.parseError) { error(report, "INGREDIENT_JSON", item.parseError, item.dirName); continue; }
    const value = item.data;
    report.counts.ingredients += 1;
    const expectedPrefix = value.seedId?.replace("ING-", "");
    if (!/^ING-\d{4}$/.test(value.seedId ?? "") || !item.dirName.startsWith(`${expectedPrefix}_`)) error(report, "INGREDIENT_IDENTITY", `Folder ${item.dirName} does not match seed ID ${value.seedId}`, value.seedId);
    if (ingredientMap.has(value.seedId)) error(report, "DUPLICATE_INGREDIENT_ID", value.seedId, value.seedId);
    ingredientMap.set(value.seedId, value);
    const canonical = value.canonicalName?.trim().toLowerCase();
    if (!canonical || names.has(canonical)) error(report, "DUPLICATE_OR_MISSING_CANONICAL", value.canonicalName, value.seedId);
    names.add(canonical);
    if (!["solid", "liquid"].includes(value.measurementType)) error(report, "MEASUREMENT_TYPE", value.measurementType, value.seedId);
    if (typeof value.dietCompatibility?.glutenFree !== "boolean" || typeof value.dietCompatibility?.lactoseFree !== "boolean" || !value.dietCompatibility?.classificationBasis) {
      error(report, "DIET_COMPATIBILITY", "Ingredient must explicitly classify glutenFree and lactoseFree with a classification basis", value.seedId);
    }
    const basisUnit = value.measurementType === "liquid" ? "ml" : "g";
    if (value.normalizedNutrition?.basisQuantity !== 100 || value.normalizedNutrition?.basisUnit !== basisUnit) error(report, "NUTRITION_BASIS", "Expected per-100 g/ml nutrition", value.seedId);
    if (value.normalizedPrice?.basisQuantity !== 100 || value.normalizedPrice?.basisUnit !== basisUnit || !(value.normalizedPrice?.usdPerBasis > 0)) error(report, "PRICE_BASIS", "Missing or invalid normalized price", value.seedId);
    if (!Number.isFinite(value.normalizedNutrition?.values?.calories) || !Number.isFinite(value.normalizedNutrition?.values?.proteinG)) error(report, "MANDATORY_NUTRITION", "Calories/protein missing", value.seedId);
    if (!value.market?.productUrl?.startsWith("https://www.walmart.com/") || !value.market?.productId || !(value.market?.packagePriceUsd > 0) || !(value.market?.packageQuantity > 0) || !value.market?.checkedAt) error(report, "PRODUCT_PROVENANCE", "Incomplete Walmart product snapshot", value.seedId);
    if (!value.nutritionSources?.some((source) => source.url?.startsWith("https://fdc.nal.usda.gov/"))) error(report, "NUTRITION_PROVENANCE", "Missing USDA nutrition URL", value.seedId);
    if (value.image?.needsGeneratedImage) { report.counts.pendingIngredientImages += 1; report.pendingImageGeneration.ingredients.push(value.seedId); }
    const imageHash = await validateImage(report, value.seedId, path.join(item.dir, "assets", "image.avif"), 256, 256, 40 * 1024, Boolean(value.image?.needsGeneratedImage));
    if (imageHash && ingredientImageHashes.has(imageHash)) error(report, "DUPLICATE_INGREDIENT_IMAGE", `Image content is identical to ${ingredientImageHashes.get(imageHash)}`, value.seedId);
    else if (imageHash) ingredientImageHashes.set(imageHash, value.seedId);
  }

  const recipeIds = new Set();
  const recipeNames = new Set();
  const recipeImageHashes = new Map();
  for (const item of recipeFiles) {
    if (item.parseError) { error(report, "RECIPE_JSON", item.parseError, item.dirName); continue; }
    const value = item.data;
    report.counts.recipes += 1;
    const expectedPrefix = value.seedId?.replace("REC-", "");
    if (!/^REC-\d{4}$/.test(value.seedId ?? "") || !item.dirName.startsWith(`${expectedPrefix}_`)) error(report, "RECIPE_IDENTITY", `Folder ${item.dirName} does not match seed ID ${value.seedId}`, value.seedId);
    if (recipeIds.has(value.seedId)) error(report, "DUPLICATE_RECIPE_ID", value.seedId, value.seedId);
    recipeIds.add(value.seedId);
    const normalizedName = value.name?.trim().toLowerCase();
    if (!normalizedName || recipeNames.has(normalizedName)) error(report, "DUPLICATE_RECIPE_NAME", value.name, value.seedId);
    recipeNames.add(normalizedName);
    const recipeSequence = Number(value.seedId?.slice(4));
    if (recipeSequence >= 117 || value.candidate) {
      const sequence = value.candidate?.sequence;
      const candidate = candidateBySequence.get(sequence);
      if (!Number.isInteger(sequence) || !candidate) error(report, "RECIPE_CANDIDATE", "Missing or unknown candidate sequence", value.seedId);
      else {
        report.counts.candidateRecipes += 1;
        if (completedCandidateSequences.has(sequence)) error(report, "DUPLICATE_CANDIDATE_RECIPE", `Candidate ${sequence} is used by multiple recipes`, value.seedId);
        completedCandidateSequences.add(sequence);
        if (value.candidate.seedId !== candidate.seedId || value.candidate.name !== candidate.name || value.name !== candidate.name) error(report, "RECIPE_CANDIDATE_MISMATCH", `Expected ${candidate.seedId} / ${candidate.name}`, value.seedId);
        if (value.mealCategory !== candidate.mealCategory) warning(report, "CANDIDATE_CATEGORY_CORRECTED", `Candidate ${sequence} intended ${candidate.mealCategory}; researched recipe uses ${value.mealCategory}`, value.seedId);
      }
    }
    if (!Array.isArray(value.ingredients) || value.ingredients.length === 0) error(report, "ZERO_INGREDIENTS", "Recipe has no ingredients", value.seedId);
    if (!Array.isArray(value.instructions) || value.instructions.length === 0) error(report, "ZERO_INSTRUCTIONS", "Recipe has no instructions", value.seedId);
    if (!(value.servings > 0) || !(value.times?.prepMinutes >= 0) || !(value.times?.cookMinutes >= 0) || value.times?.totalMinutes !== value.times?.prepMinutes + value.times?.cookMinutes) error(report, "RECIPE_QUANTITIES", "Invalid servings or times", value.seedId);
    if (!allowedMealTypes.has(value.mealType)) error(report, "INVALID_MEAL_TYPE", `Expected Breakfast, Lunch, or Dinner; received ${value.mealType}`, value.seedId);
    if (!allowedMealCategories.has(value.mealCategory)) error(report, "INVALID_MEAL_CATEGORY", `Expected one of ${allowedMealCategoryNames.join(", ")}; received ${value.mealCategory}`, value.seedId);
    if (!value.source?.url || !value.source?.domain) error(report, "RECIPE_SOURCE", "Missing source", value.seedId);
    else { try { const parsed = new URL(value.source.url); if (parsed.protocol !== "https:") throw new Error("HTTPS required"); } catch { error(report, "RECIPE_SOURCE_URL", value.source.url, value.seedId); } }
    const usageIds = new Set();
    for (const usage of value.ingredients ?? []) {
      const ingredientValue = ingredientMap.get(usage.ingredientSeedId);
      if (!ingredientValue) error(report, "MISSING_INGREDIENT_REFERENCE", usage.ingredientSeedId, value.seedId);
      if (usageIds.has(usage.ingredientSeedId)) error(report, "DUPLICATE_RECIPE_INGREDIENT", usage.ingredientSeedId, value.seedId);
      usageIds.add(usage.ingredientSeedId);
      if (!(usage.sourceQuantity > 0) || !usage.sourceUnit || !usage.sourceDisplay || !(usage.normalizedQuantity > 0) || !["g", "ml"].includes(usage.normalizedUnit)) error(report, "INGREDIENT_MEASUREMENT", JSON.stringify(usage), value.seedId);
      if (ingredientValue && usage.normalizedUnit !== ingredientValue.normalizedNutrition.basisUnit) error(report, "INGREDIENT_UNIT_MISMATCH", usage.ingredientSeedId, value.seedId);
      if (!(usage.cookingYieldFactor > 0)) error(report, "COOKING_YIELD", `Missing cooking-yield factor for ${usage.ingredientSeedId}`, value.seedId);
    }
    const expectedFinishedWeight = round((value.ingredients ?? []).reduce((sum, usage) => sum + usage.normalizedQuantity * usage.cookingYieldFactor, 0));
    if (!near(value.estimatedFinishedWeightG, expectedFinishedWeight)) error(report, "FINISHED_WEIGHT_MISMATCH", `Stored ${value.estimatedFinishedWeightG}; expected ${expectedFinishedWeight}`, value.seedId);
    if (new Set(value.diets ?? []).size !== (value.diets ?? []).length) error(report, "DUPLICATE_DIET", "Recipe contains duplicate diet values", value.seedId);
    for (const diet of value.diets ?? []) if (!allowedDiets.has(diet)) error(report, "INVALID_DIET", diet, value.seedId); else increment(report.distributions.diets, diet);
    const expectedRestrictionDiets = new Set(deriveRestrictionDiets(value, ingredientMap));
    for (const diet of derivedDietNames) {
      const expectedDiet = expectedRestrictionDiets.has(diet);
      const actualDiet = (value.diets ?? []).includes(diet);
      if (expectedDiet !== actualDiet) error(report, "RESTRICTION_DIET_MISMATCH", `${diet}: stored ${actualDiet}; expected ${expectedDiet} from ingredient compatibility`, value.seedId);
    }
    for (const badge of value.badges ?? []) if (!allowedBadges.has(badge)) error(report, "INVALID_BADGE", badge, value.seedId); else increment(report.distributions.badges, badge);
    const recalculated = recalculate(value, ingredientMap);
    for (const [key, expected] of Object.entries(recalculated.total)) if (!near(value.calculatedNutrition?.total?.[key], expected)) error(report, "TOTAL_NUTRITION_MISMATCH", `${key}: stored ${value.calculatedNutrition?.total?.[key]}, expected ${expected}`, value.seedId);
    for (const [key, expected] of Object.entries(recalculated.perServing)) if (!near(value.calculatedNutrition?.perServing?.[key], expected)) error(report, "SERVING_NUTRITION_MISMATCH", `${key}: stored ${value.calculatedNutrition?.perServing?.[key]}, expected ${expected}`, value.seedId);
    if (!near(value.calculatedCost?.totalUsd, recalculated.totalCost) || !near(value.calculatedCost?.perServingUsd, recalculated.servingCost)) error(report, "COST_MISMATCH", `Stored ${value.calculatedCost?.totalUsd}/${value.calculatedCost?.perServingUsd}; expected ${recalculated.totalCost}/${recalculated.servingCost}`, value.seedId);
    const expected = expectedBadges(value).sort();
    const actual = [...(value.badges ?? [])].sort();
    if (JSON.stringify(expected) !== JSON.stringify(actual)) error(report, "BADGE_RULE_MISMATCH", `Stored ${actual.join(", ")}; expected ${expected.join(", ")}`, value.seedId);
    increment(report.distributions.countries, value.countryOfOrigin);
    increment(report.distributions.sourceDomains, value.source?.domain);
    increment(report.distributions.athleteTarget, String(Boolean(value.studentAthleteTarget)));
    increment(report.distributions.discovery, String(Boolean(value.discovery)));
    increment(report.distributions.primaryProteins, value.primaryProtein);
    increment(report.distributions.carbohydrateBases, value.carbohydrateBase);
    increment(report.distributions.mealTypes, value.mealType);
    increment(report.distributions.mealCategories, value.mealCategory);
    increment(report.distributions.cookingMethods, value.cookingMethod);
    increment(report.distributions.timeBuckets, value.times.totalMinutes <= 20 ? "0-20" : value.times.totalMinutes <= 40 ? "21-40" : value.times.totalMinutes <= 60 ? "41-60" : "61+");
    increment(report.distributions.costBuckets, value.calculatedCost.perServingUsd <= 1.5 ? "very-inexpensive" : value.calculatedCost.perServingUsd <= 3 ? "moderate" : value.calculatedCost.perServingUsd <= 4 ? "upper-budget" : "premium");
    if (value.image?.needsGeneratedImage) { report.counts.pendingRecipeImages += 1; report.pendingImageGeneration.recipes.push(value.seedId); }
    const imageHash = await validateImage(report, value.seedId, path.join(item.dir, "assets", "main.avif"), 1200, 800, 200 * 1024, Boolean(value.image?.needsGeneratedImage));
    if (imageHash && recipeImageHashes.has(imageHash)) error(report, "DUPLICATE_RECIPE_IMAGE", `Image content is identical to ${recipeImageHashes.get(imageHash)}`, value.seedId);
    else if (imageHash) recipeImageHashes.set(imageHash, value.seedId);
    await validateImage(report, value.seedId, path.join(item.dir, "assets", "card.avif"), 480, 320, 80 * 1024, Boolean(value.image?.needsGeneratedImage));
  }

  for (let i = 0; i < recipeFiles.length; i += 1) {
    const a = recipeFiles[i].data; if (!a) continue;
    for (let j = i + 1; j < recipeFiles.length; j += 1) {
      const b = recipeFiles[j].data; if (!b) continue;
      const setA = new Set(a.ingredients.map((item) => item.ingredientSeedId));
      const setB = new Set(b.ingredients.map((item) => item.ingredientSeedId));
      const overlap = [...setA].filter((id) => setB.has(id)).length;
      const union = new Set([...setA, ...setB]).size;
      if (overlap / union >= 0.8 && a.cookingMethod === b.cookingMethod) warning(report, "POSSIBLE_RECIPE_DUPLICATE", `${a.seedId} and ${b.seedId} share ${round(overlap / union, 2)} ingredient Jaccard similarity`);
    }
  }

  const recipes = report.counts.recipes || 1;
  report.diversityTargets = {
    studentAthlete: { count: report.distributions.athleteTarget.true ?? 0, percentage: round(((report.distributions.athleteTarget.true ?? 0) / recipes) * 100, 1), targetApproximatePercentage: 30 },
    mealPrep: { count: report.distributions.badges["Meal Prep"] ?? 0, percentage: round(((report.distributions.badges["Meal Prep"] ?? 0) / recipes) * 100, 1), targetMinimumPercentage: 25 },
    discovery: { count: report.distributions.discovery.true ?? 0, percentage: round(((report.distributions.discovery.true ?? 0) / recipes) * 100, 1), targetApproximatePercentage: 10 },
    familiarToUS: { count: recipes - (report.distributions.discovery.true ?? 0), percentage: round(((recipes - (report.distributions.discovery.true ?? 0)) / recipes) * 100, 1), targetApproximatePercentage: 90 },
    mainMeal: { count: report.distributions.mealCategories["Main Meal"] ?? 0, percentage: round(((report.distributions.mealCategories["Main Meal"] ?? 0) / recipes) * 100, 1), targetApproximatePercentage: mainMealTargetPercentage },
    otherMealCategories: { count: recipes - (report.distributions.mealCategories["Main Meal"] ?? 0), percentage: round(((recipes - (report.distributions.mealCategories["Main Meal"] ?? 0)) / recipes) * 100, 1), targetApproximatePercentage: nonMainMealTargetPercentage },
  };
  if (report.diversityTargets.studentAthlete.percentage < 25) warning(report, "ATHLETE_TARGET_LOW", "Student-athlete share is below the progressive range");
  if (report.diversityTargets.mealPrep.percentage < 25) error(report, "MEAL_PREP_TARGET_LOW", "Meal Prep share is below 25%");
  if (report.diversityTargets.mealPrep.percentage > 40) warning(report, "MEAL_PREP_TARGET_HIGH", "Meal Prep share is materially above the approximate progressive target");
  if (report.diversityTargets.discovery.percentage < 8) warning(report, "DISCOVERY_TARGET_LOW", "Discovery share is below the progressive range");
  if (report.diversityTargets.mainMeal.percentage < 80 || report.diversityTargets.mainMeal.percentage > 90) warning(report, "MAIN_MEAL_TARGET_DRIFT", "Main Meal share is outside the progressive 80%-90% range around the candidate plan's 85% target");
  for (const diet of allowedDiets) if (!report.distributions.diets[diet]) warning(report, "DIET_NOT_YET_REPRESENTED", diet);
  report.valid = report.errors.length === 0;
  if (writeReport) await writeFile(path.join(dataDir, "generated", "reports", "validation.json"), `${JSON.stringify(report, null, 2)}\n`);
  return report;
}

const isCli = process.argv[1] && import.meta.url === pathToFileURL(path.resolve(process.argv[1])).href;
if (isCli) {
  runValidation().then((report) => {
    process.stdout.write(`Validation ${report.valid ? "passed" : "failed"}: ${report.counts.ingredients} ingredients, ${report.counts.recipes} recipes, ${report.errors.length} errors, ${report.warnings.length} warnings.\n`);
    if (!report.valid) process.exitCode = 1;
  }).catch((cause) => { process.stderr.write(`${cause.stack ?? cause}\n`); process.exitCode = 1; });
}

import { readdir, readFile, stat, writeFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { runValidation } from "../validation/validate.mjs";
import { deriveCandidateAcquisition } from "./candidate-progress.mjs";

const toolDir = path.dirname(fileURLToPath(import.meta.url));
const dataDir = path.resolve(toolDir, "..", "..");
const json = (value) => `${JSON.stringify(value, null, 2)}\n`;

async function loadRecipes() {
  const entries = (await readdir(path.join(dataDir, "recipes"), { withFileTypes: true })).filter((entry) => entry.isDirectory()).sort((a, b) => a.name.localeCompare(b.name));
  const values = [];
  for (const entry of entries) {
    try { values.push(JSON.parse(await readFile(path.join(dataDir, "recipes", entry.name, "recipe.json"), "utf8"))); } catch {}
  }
  return values;
}

function bucket(values, selector, ranges) {
  const result = Object.fromEntries(ranges.map(([label]) => [label, 0]));
  for (const value of values) {
    const number = selector(value);
    const match = ranges.find(([, max]) => number <= max) ?? ranges.at(-1);
    result[match[0]] += 1;
  }
  return result;
}

async function main() {
  const validation = await runValidation({ writeReport: true });
  if (!validation.valid) throw new Error(`Checkpoint refused: ${validation.errors.length} validation errors`);
  for (const required of ["schema/appeteeInitDatabase.sql", "reference/dataInitDatabase.sql", "ingredients/IngredientInit.sql", "recipes/RecipeInit.sql", "ingredients/index.json", "recipes/index.json", "distribution-report.json", "image-manifest.json", "research/image/ingredients.json", "research/image/recipes.json"]) await stat(path.join(dataDir, required));
  const ingredientImageQueue = JSON.parse(await readFile(path.join(dataDir, "research", "image", "ingredients.json"), "utf8"));
  const recipeImageQueue = JSON.parse(await readFile(path.join(dataDir, "research", "image", "recipes.json"), "utf8"));
  if (ingredientImageQueue.length !== validation.counts.pendingIngredientImages || recipeImageQueue.length !== validation.counts.pendingRecipeImages) throw new Error("Checkpoint refused: research/image queues do not match pending image counts; run npm run build first");
  const recipes = await loadRecipes();
  const previousProgress = JSON.parse(await readFile(path.join(dataDir, "progress.json"), "utf8"));
  const previousVersion = JSON.parse(await readFile(path.join(dataDir, "version.json"), "utf8"));
  const candidateAcquisition = await deriveCandidateAcquisition(recipes, previousProgress.candidateAcquisition);
  const now = new Date().toISOString();
  const nextRecipeSequence = validation.counts.recipes + 1;
  const nextIngredientSequence = validation.counts.ingredients + 1;
  const requestedBatchSize = Number(process.env.APPETEE_BATCH_SIZE ?? 50);
  if (!Number.isInteger(requestedBatchSize) || requestedBatchSize < 1) throw new Error("APPETEE_BATCH_SIZE must be a positive integer");
  const datasetVersion = process.env.APPETEE_DATASET_VERSION ?? previousVersion.datasetVersion;
  const snapshotName = `appetee-dataset-v${datasetVersion}-${validation.counts.recipes}-recipes.zip`;
  const pendingImageCount = validation.counts.pendingIngredientImages + validation.counts.pendingRecipeImages;
  const progress = {
    status: "in-progress-valid-checkpoint",
    datasetVersion,
    completedRecipes: validation.counts.recipes,
    completedIngredients: validation.counts.ingredients,
    lastCompletedRecipeSequence: validation.counts.recipes,
    nextRecipeSequence,
    lastCompletedIngredientSequence: validation.counts.ingredients,
    nextIngredientSequence,
    currentBatchStart: nextRecipeSequence,
    currentBatchTarget: nextRecipeSequence + requestedBatchSize - 1,
    candidateAcquisition,
    studentAthleteRecipeCount: validation.diversityTargets.studentAthlete.count,
    mealPrepRecipeCount: validation.diversityTargets.mealPrep.count,
    discoveryRecipeCount: validation.diversityTargets.discovery.count,
    mainMealRecipeCount: validation.diversityTargets.mainMeal.count,
    otherMealCategoryRecipeCount: validation.diversityTargets.otherMealCategories.count,
    sourceDomainCounts: validation.distributions.sourceDomains,
    failedRecipeCandidates: previousProgress.failedRecipeCandidates ?? [],
    skippedIngredients: previousProgress.skippedIngredients ?? [],
    pendingImageGeneration: [
      ...validation.pendingImageGeneration.ingredients.map((seedId) => ({ type: "ingredient", seedId })),
      ...validation.pendingImageGeneration.recipes.map((seedId) => ({ type: "recipe", seedId })),
    ],
    lastValidationAt: now,
    lastCheckpointAt: now,
    checkpointSnapshot: snapshotName,
    nextActions: [
      "Re-run npm run validate before acquiring the next batch.",
      `Begin with candidate ${candidateAcquisition.nextCandidateSequence} in recipe_name_candidates.json and process names strictly in order until approximately ${requestedBatchSize} new valid recipes are complete or a safe partial checkpoint is required.`,
      "Research missing canonical ingredients before writing any recipe that references them; search ingredients/index.json first.",
      ...(pendingImageCount ? ["Acquire pending images in order: exact source/product page, then distinct related real-food search for recipes, then AI only as the documented final fallback; keep unresolved records in both research/image queues."] : ["Preserve current image assets and keep the handoff queues synchronized."]),
    ],
    proteinSourceDistribution: validation.distributions.primaryProteins,
    mealTypeDistribution: validation.distributions.mealTypes,
    mealCategoryDistribution: validation.distributions.mealCategories,
    cookingMethodDistribution: validation.distributions.cookingMethods,
    prepTimeBuckets: validation.distributions.timeBuckets,
    costPerServingBuckets: validation.distributions.costBuckets,
    caloriePerServingBuckets: bucket(recipes, (item) => item.calculatedNutrition.perServing.calories, [["<=300", 300], ["301-500", 500], ["501-700", 700], [">700", Infinity]]),
    proteinPerServingBuckets: bucket(recipes, (item) => item.calculatedNutrition.perServing.proteinG, [["<15", 14.999], ["15-29.9", 29.999], ["30-44.9", 44.999], [">=45", Infinity]]),
    currentDiversityGaps: [
      "Stovetop recipes remain the dominant cooking method; add air-fryer, slow-cooker, grill, microwave, and more no-cook coverage.",
      "Egg and lentil proteins are comparatively concentrated; add more pork, tempeh, fish, beans, and other non-chicken athlete proteins.",
      "Korean cuisine remains underrepresented, and source-domain diversity should continue to expand.",
      "Track actual meal-category drift against the candidate plan's approximate 85/15 Main Meal/other target without reordering candidates.",
      ...(pendingImageCount ? [`${pendingImageCount} records remain in the image handoff queues for exact/related real-photo acquisition and optional last-resort AI fallback.`] : []),
    ],
    nextBatchDiversityPriorities: [
      "Air-fryer, slow-cooker, grill, microwave, and no-cook methods",
      "Pork, tempeh, beans, and additional fish in the athlete subset",
      "Korean and other underrepresented familiar-to-US cuisines",
      "Process candidate names strictly in order and classify actual meal roles truthfully against the approximate 85/15 candidate plan",
      "Maintain meaningful Gluten Free and Lactose Free coverage using ingredient-level compatibility evidence",
      "Keep athlete near 30%, Meal Prep at or above 25%, and discovery near 10%",
    ],
  };
  const version = {
    datasetVersion,
    status: "in-progress-valid-checkpoint",
    generatedAt: now,
    recipeCount: validation.counts.recipes,
    ingredientCount: validation.counts.ingredients,
    dietCount: 8,
    badgeCount: 9,
    walmartStoreNumber: 3789,
    walmartStoreAddress: "1959 Wall Ave, Ogden, UT 84401",
    priceSnapshotStartedAt: "2026-08-10T12:00:00-06:00",
    schemaVersion: "current-backend-development plus canonical dataset badge constraint",
    validationStatus: "valid",
    validationErrors: 0,
    checkpointSnapshot: snapshotName,
  };
  const plan = `# Dataset Execution Plan\n\n## Status\n\nValid checkpoint at ${validation.counts.recipes} recipes and ${validation.counts.ingredients} canonical ingredients. Dataset growth remains in progress toward >= 2,000 recipes.\n\n## Workstreams\n\n- [x] Build deterministic validation, SQL, index, image-audit, and checkpoint tooling.\n- [x] Adopt \`recipe_name_candidates.json\` as the immutable ordered recipe-name source.\n- [x] Persist completed, skipped, unresolved, and next candidate sequences.\n- [ ] Continue ordered acquisition at approximately ${requestedBatchSize} new valid recipes per checkpoint.\n- [ ] Stop at the largest fully valid partial checkpoint whenever integrity or system limits make the full target unsafe.\n- [ ] Maintain and report student-athlete, Meal Prep, Discovery, meal-category, cuisine, method, nutrition, cost, diet, badge, and source distributions.\n- [ ] Reach >= 2,000 validated recipes and create the final versioned snapshot.\n\nOperational details and exact recipe/ingredient/candidate sequences are in \`progress.json\` and \`RESUME.md\`.\n`;
  const finalResume = `# Dataset Resume State\n\nCheckpoint v${datasetVersion} is valid and resumable. The corpus is intentionally partial.\n\n## Current state\n\n- Completed recipes: ${validation.counts.recipes}\n- Completed ingredients: ${validation.counts.ingredients}\n- Last completed recipe: REC-${String(validation.counts.recipes).padStart(4, "0")}\n- Next recipe sequence: ${nextRecipeSequence}\n- Next ingredient sequence: ${nextIngredientSequence}\n- Last candidate processed: ${candidateAcquisition.lastCandidateSequenceProcessed}\n- Last candidate completed: ${candidateAcquisition.lastCandidateSequenceCompleted}\n- Next candidate sequence: ${candidateAcquisition.nextCandidateSequence}\n- Completed candidate recipes: ${candidateAcquisition.completedCandidateCount}\n- Skipped candidates: ${candidateAcquisition.skippedCandidates.length}\n- Unresolved candidates: ${candidateAcquisition.unresolvedCandidates.length}\n- Validation: valid, ${validation.errors.length} errors, ${validation.warnings.length} warnings\n- Images pending in owner handoff queues: ${pendingImageCount}\n- Checkpoint ZIP: \`${snapshotName}\`\n\n## Resume procedure\n\n1. Read \`data/AGENTS.md\`, \`data/START_CODEX_TASK.md\`, and \`data/DATASET_SPEC.md\`.\n2. Run \`npm run validate\` from \`data/tools\`; do not continue if it fails.\n3. Inspect \`data/progress.json\`, \`data/distribution-report.json\`, and both lightweight indexes.\n4. Begin at candidate ${candidateAcquisition.nextCandidateSequence} and process \`data/recipe_name_candidates.json\` strictly in sequence.\n5. Write the next recipe as REC-${String(nextRecipeSequence).padStart(4, "0")} and any new ingredient as ING-${String(nextIngredientSequence).padStart(4, "0")}; never leave a partial record.\n6. Research a real matching recipe, resolve ingredients, calculate/classify, and persist candidate metadata. Record duplicates as skipped and unsourceable names as unresolved.\n7. Do not search for, download, or generate images through Codex; keep pending images in both owner handoff queues.\n8. Run image queue audit, build, validation, checkpoint, and create a ZIP named with the actual count.\n\n## Operating targets\n\n- Approximately ${requestedBatchSize} new valid recipes per checkpoint, or the largest safe partial checkpoint.\n- Candidate order controls names; distribution analysis reports drift and never authorizes reordering.\n- Candidate plan: approximately 85% Main Meal / 15% other, 30% athlete, 25% Meal Prep, and 10% Discovery.\n- Derive Gluten Free and Lactose Free only from ingredient compatibility.\n`;
  const updatedFinalResume = finalResume.replace(
    "7. Do not search for, download, or generate images through Codex; keep pending images in both owner handoff queues.",
    "7. Acquire images in order: exact source/product page, then distinct related real-food search for recipes, then AI only as the final fallback; keep unresolved images in both handoff queues.",
  );
  await writeFile(path.join(dataDir, "progress.json"), json(progress));
  await writeFile(path.join(dataDir, "version.json"), json(version));
  await writeFile(path.join(dataDir, "PLAN.md"), plan);
  await writeFile(path.join(dataDir, "RESUME.md"), updatedFinalResume);
  process.stdout.write(`Checkpoint metadata updated for ${validation.counts.recipes} valid recipes.\n`);
}

main().catch((error) => { process.stderr.write(`${error.stack ?? error}\n`); process.exitCode = 1; });

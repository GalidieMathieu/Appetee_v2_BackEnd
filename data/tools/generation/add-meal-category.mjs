import { readdir, readFile, writeFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { canonicalMealCategory } from "../shared/meal-categories.mjs";

const toolDir = path.dirname(fileURLToPath(import.meta.url));
const dataDir = path.resolve(toolDir, "..", "..");

function insertAfter(object, afterKey, key, value) {
  const output = {};
  for (const [currentKey, currentValue] of Object.entries(object)) {
    if (currentKey === key) continue;
    output[currentKey] = currentValue;
    if (currentKey === afterKey) output[key] = value;
  }
  if (!(key in output)) output[key] = value;
  return output;
}

async function main() {
  const root = path.join(dataDir, "recipes");
  const entries = (await readdir(root, { withFileTypes: true })).filter((entry) => entry.isDirectory()).sort((a, b) => a.name.localeCompare(b.name));
  let updated = 0;
  for (const entry of entries) {
    const file = path.join(root, entry.name, "recipe.json");
    let recipe;
    try { recipe = JSON.parse(await readFile(file, "utf8")); } catch { continue; }
    const mealCategory = canonicalMealCategory(recipe.seedId, recipe.mealCategory);
    recipe = insertAfter(recipe, "mealType", "mealCategory", mealCategory);
    if (recipe.futureApiResponseExample) recipe.futureApiResponseExample = insertAfter(recipe.futureApiResponseExample, "description", "mealType", recipe.mealType);
    if (recipe.futureApiResponseExample) recipe.futureApiResponseExample = insertAfter(recipe.futureApiResponseExample, "mealType", "mealCategory", recipe.mealCategory);
    await writeFile(file, `${JSON.stringify(recipe, null, 2)}\n`);
    updated += 1;
  }
  process.stdout.write(`Migrated ${updated} recipe records to the canonical meal-category vocabulary.\n`);
}

main().catch((error) => { process.stderr.write(`${error.stack ?? error}\n`); process.exitCode = 1; });

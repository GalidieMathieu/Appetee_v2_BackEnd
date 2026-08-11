import { readdir, readFile, writeFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { applyDerivedDiets, dietCompatibilityForIngredient } from "../diet-compatibility.mjs";

const toolDir = path.dirname(fileURLToPath(import.meta.url));
const dataDir = path.resolve(toolDir, "..", "..");
const json = (value) => `${JSON.stringify(value, null, 2)}\n`;

async function records(root, jsonName) {
  const entries = (await readdir(root, { withFileTypes: true })).filter((entry) => entry.isDirectory()).sort((a, b) => a.name.localeCompare(b.name));
  const output = [];
  for (const entry of entries) {
    const file = path.join(root, entry.name, jsonName);
    output.push({ file, data: JSON.parse(await readFile(file, "utf8")) });
  }
  return output;
}

const ingredientRecords = await records(path.join(dataDir, "ingredients"), "ingredient.json");
const ingredientMap = new Map();
for (const record of ingredientRecords) {
  record.data.dietCompatibility = dietCompatibilityForIngredient(record.data.seedId, record.data.dietCompatibility);
  ingredientMap.set(record.data.seedId, record.data);
}

const recipeRecords = await records(path.join(dataDir, "recipes"), "recipe.json");
for (const record of recipeRecords) {
  record.data.diets = applyDerivedDiets(record.data, ingredientMap);
  if (record.data.futureApiResponseExample) record.data.futureApiResponseExample.diets = record.data.diets;
}

for (const record of ingredientRecords) await writeFile(record.file, json(record.data));
for (const record of recipeRecords) await writeFile(record.file, json(record.data));

const glutenFreeCount = recipeRecords.filter((record) => record.data.diets.includes("Gluten Free")).length;
const lactoseFreeCount = recipeRecords.filter((record) => record.data.diets.includes("Lactose Free")).length;
process.stdout.write(`Classified ${ingredientRecords.length} ingredients and ${recipeRecords.length} recipes: ${glutenFreeCount} Gluten Free, ${lactoseFreeCount} Lactose Free.\n`);

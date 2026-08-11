import { mkdir, readdir, readFile, writeFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

const toolDir = path.dirname(fileURLToPath(import.meta.url));
const dataDir = path.resolve(toolDir, "..", "..");

async function pendingRecords(group, jsonName) {
  const root = path.join(dataDir, group);
  const entries = (await readdir(root, { withFileTypes: true })).filter((entry) => entry.isDirectory()).sort((a, b) => a.name.localeCompare(b.name));
  const records = [];
  for (const entry of entries) {
    const file = path.join(root, entry.name, jsonName);
    try {
      const data = JSON.parse(await readFile(file, "utf8"));
      if (data.image?.needsGeneratedImage) records.push(data);
    } catch {}
  }
  return records;
}

export async function syncImageQueues() {
  const ingredients = (await pendingRecords("ingredients", "ingredient.json")).map((item) => {
    if (!item.name || !item.market?.productUrl) throw new Error(`${item.seedId ?? "Ingredient"} cannot enter the image queue without name and Walmart product URL`);
    return { name: item.name, url: item.market.productUrl };
  });
  const recipes = (await pendingRecords("recipes", "recipe.json")).map((item) => {
    if (!item.name || !item.source?.url) throw new Error(`${item.seedId ?? "Recipe"} cannot enter the image queue without name and recipe source URL`);
    return { name: item.name, url: item.source.url };
  });
  const outputDir = path.join(dataDir, "research", "image");
  await mkdir(outputDir, { recursive: true });
  await writeFile(path.join(outputDir, "ingredients.json"), `${JSON.stringify(ingredients, null, 2)}\n`);
  await writeFile(path.join(outputDir, "recipes.json"), `${JSON.stringify(recipes, null, 2)}\n`);
  return { ingredients: ingredients.length, recipes: recipes.length };
}

const isCli = process.argv[1] && import.meta.url === pathToFileURL(path.resolve(process.argv[1])).href;
if (isCli) syncImageQueues()
  .then((counts) => process.stdout.write(`Synchronized pending real-image queues: ${counts.ingredients} ingredients, ${counts.recipes} recipes.\n`))
  .catch((error) => { process.stderr.write(`${error.stack ?? error}\n`); process.exitCode = 1; });

import { mkdir, writeFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

const toolDir = path.dirname(fileURLToPath(import.meta.url));
const dataDir = path.resolve(toolDir, "..", "..");
const outputFile = path.join(dataDir, "research", "cache", "themealdb-catalog.json");

function compactMeal(meal) {
  const ingredients = [];
  for (let index = 1; index <= 20; index += 1) {
    const name = meal[`strIngredient${index}`]?.trim();
    const measure = meal[`strMeasure${index}`]?.trim();
    if (name) ingredients.push({ name, measure: measure || null });
  }
  return {
    id: meal.idMeal,
    name: meal.strMeal,
    area: meal.strArea || null,
    category: meal.strCategory || null,
    sourceUrl: meal.strSource || `https://www.themealdb.com/meal/${meal.idMeal}`,
    youtubeUrl: meal.strYoutube || null,
    ingredients,
    instructions: meal.strInstructions || null,
  };
}

async function main() {
  const meals = new Map();
  for (const letter of "abcdefghijklmnopqrstuvwxyz") {
    const url = `https://www.themealdb.com/api/json/v1/1/search.php?f=${letter}`;
    const response = await fetch(url, { headers: { "User-Agent": "Appetee-Dataset-Research/0.3" } });
    if (!response.ok) throw new Error(`${url} returned ${response.status}`);
    const payload = await response.json();
    for (const meal of payload.meals ?? []) meals.set(meal.idMeal, compactMeal(meal));
  }
  const records = [...meals.values()].sort((a, b) => a.name.localeCompare(b.name));
  await mkdir(path.dirname(outputFile), { recursive: true });
  await writeFile(outputFile, `${JSON.stringify({ acquiredAt: new Date().toISOString(), source: "https://www.themealdb.com/api.php", count: records.length, records }, null, 2)}\n`);
  process.stdout.write(`Cached ${records.length} public recipe records from TheMealDB.\n`);
}

main().catch((error) => { process.stderr.write(`${error.stack ?? error}\n`); process.exitCode = 1; });

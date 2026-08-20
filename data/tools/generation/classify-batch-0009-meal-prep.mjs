import { readFile, readdir, writeFile } from "node:fs/promises";
import path from "node:path";

const dataDir = path.resolve(import.meta.dirname, "..", "..");
const suitable = /meal prep|overnight oats|chili|soup|stew|casserole|grain bowl|rice bowl|quinoa bowl|noodle bowl|baked potatoes|tray bake|sheet pan|pilaf|meatballs|patties/iu;
let reviewed = 0;
let mealPrep = 0;
for (const entry of await readdir(path.join(dataDir, "recipes"), { withFileTypes: true })) {
  if (!entry.isDirectory()) continue;
  const file = path.join(dataDir, "recipes", entry.name, "recipe.json");
  let recipe;
  try { recipe = JSON.parse(await readFile(file, "utf8")); } catch { continue; }
  if ((recipe.candidate?.sequence ?? 0) < 1965) continue;
  reviewed += 1;
  const qualifies = Boolean(recipe.badgeJudgements?.mealPrep) || suitable.test(recipe.name);
  recipe.badgeJudgements = { ...recipe.badgeJudgements, mealPrep: qualifies };
  recipe.badges = recipe.badges.filter((badge) => badge !== "Meal Prep");
  if (qualifies) { recipe.badges.push("Meal Prep"); mealPrep += 1; }
  recipe.futureApiResponseExample.badges = [...recipe.badges];
  await writeFile(file, `${JSON.stringify(recipe, null, 2)}\n`);
}
process.stdout.write(`Reviewed ${reviewed} batch-0009 recipes; classified ${mealPrep} as Meal Prep.\n`);

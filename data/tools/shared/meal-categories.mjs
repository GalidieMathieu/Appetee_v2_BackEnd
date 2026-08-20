export const allowedMealCategoryNames = [
  "Main Meal",
  "Small Meal",
  "Snack",
  "Side",
  "Meal Component",
  "Dessert",
  "Drink",
];

export const mainMealTargetPercentage = 85;
export const nonMainMealTargetPercentage = 15;

// Semantic classifications for current records that are not main meals.
// Every other recipe through REC-0075 is a Main Meal. These assignments are
// based on the recipe's intended role, not on forcing the corpus to 85/15.
const currentNonMainMealCategories = new Map([
  ["REC-0011", "Small Meal"],
  ["REC-0025", "Drink"],
  ["REC-0026", "Snack"],
  ["REC-0027", "Dessert"],
  ["REC-0028", "Small Meal"],
  ["REC-0029", "Small Meal"],
  ["REC-0030", "Snack"],
  ["REC-0031", "Snack"],
  ["REC-0032", "Meal Component"],
  ["REC-0033", "Meal Component"],
  ["REC-0034", "Snack"],
  ["REC-0038", "Small Meal"],
  ["REC-0051", "Small Meal"],
]);

export function canonicalMealCategory(seedId, value) {
  const currentClassification = currentNonMainMealCategories.get(seedId);
  if (currentClassification) return currentClassification;

  const sequence = Number(String(seedId).replace("REC-", ""));
  if (value === "Full Meal" && Number.isInteger(sequence) && sequence >= 1 && sequence <= 75) return "Main Meal";
  if (allowedMealCategoryNames.includes(value)) return value;

  throw new Error(`${seedId} must provide one canonical mealCategory: ${allowedMealCategoryNames.join(", ")}`);
}

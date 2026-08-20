export const allowedDietNames = [
  "Vegetarian",
  "Vegan",
  "Pescatarian",
  "Keto",
  "Paleo",
  "Flexitarian",
  "Gluten Free",
  "Lactose Free",
];

export const derivedDietNames = ["Gluten Free", "Lactose Free"];

// Conservative classifications for the canonical ingredients in checkpoints
// through ING-0051. Oats are not treated as gluten free unless the exact retail
// product is certified; the five-grain tempeh contains a gluten-bearing grain.
const glutenIncompatibleIngredientIds = new Set([
  "ING-0020", // soy sauce made with wheat
  "ING-0027", // whole-wheat tortillas
  "ING-0029", // oats not documented as certified gluten free
  "ING-0039", // wheat penne
  "ING-0049", // five-grain tempeh containing barley
  "ING-0051", // whole-wheat bread
]);

const lactoseIncompatibleIngredientIds = new Set([
  "ING-0030", // dairy milk
  "ING-0031", // Greek yogurt
  "ING-0035", // cottage cheese
]);

const classificationBasis = "Canonical product identity and documented ingredient/allergen information; cross-contact is not inferred, and uncertified oats are classified conservatively.";

export function dietCompatibilityForIngredient(seedId, explicit = null) {
  if (explicit && typeof explicit.glutenFree === "boolean" && typeof explicit.lactoseFree === "boolean") {
    return {
      glutenFree: explicit.glutenFree,
      lactoseFree: explicit.lactoseFree,
      classificationBasis: explicit.classificationBasis ?? classificationBasis,
      notes: explicit.notes ?? null,
    };
  }

  const sequence = Number(String(seedId).replace("ING-", ""));
  if (!Number.isInteger(sequence) || sequence < 1 || sequence > 51) {
    throw new Error(`${seedId} must provide explicit glutenFree and lactoseFree ingredient compatibility`);
  }

  return {
    glutenFree: !glutenIncompatibleIngredientIds.has(seedId),
    lactoseFree: !lactoseIncompatibleIngredientIds.has(seedId),
    classificationBasis,
    notes: seedId === "ING-0029"
      ? "Not labeled Gluten Free because the selected oat product is not documented as certified gluten free."
      : seedId === "ING-0049"
        ? "Not labeled Gluten Free because the selected five-grain tempeh includes a gluten-bearing grain."
        : null,
  };
}

export function deriveRestrictionDiets(recipe, ingredientMap) {
  const ingredients = recipe.ingredients.map((usage) => ingredientMap.get(usage.ingredientSeedId));
  if (ingredients.some((ingredient) => !ingredient)) return [];
  const diets = [];
  if (ingredients.every((ingredient) => ingredient.dietCompatibility?.glutenFree === true)) diets.push("Gluten Free");
  if (ingredients.every((ingredient) => ingredient.dietCompatibility?.lactoseFree === true)) diets.push("Lactose Free");
  return diets;
}

export function applyDerivedDiets(recipe, ingredientMap) {
  const retained = (recipe.diets ?? []).filter((diet) => !derivedDietNames.includes(diet));
  return [...retained, ...deriveRestrictionDiets(recipe, ingredientMap)];
}

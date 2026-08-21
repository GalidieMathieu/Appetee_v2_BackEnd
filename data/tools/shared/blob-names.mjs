const INGREDIENT_SEED_ID = /^ING-\d{4}$/u;
const RECIPE_SEED_ID = /^REC-\d{4}$/u;

function assertSeedId(seedId, pattern, entityType) {
  if (typeof seedId !== "string" || !pattern.test(seedId)) {
    throw new TypeError(`Invalid ${entityType} seed ID: ${String(seedId)}`);
  }

  return seedId;
}

export function getIngredientImageBlobName(seedId) {
  return `dataset/ingredients/${assertSeedId(seedId, INGREDIENT_SEED_ID, "ingredient")}/image.avif`;
}

export function getRecipeMainImageBlobName(seedId) {
  return `dataset/recipes/${assertSeedId(seedId, RECIPE_SEED_ID, "recipe")}/main.avif`;
}

export function getRecipeCardImageBlobName(seedId) {
  return `dataset/recipes/${assertSeedId(seedId, RECIPE_SEED_ID, "recipe")}/card.avif`;
}

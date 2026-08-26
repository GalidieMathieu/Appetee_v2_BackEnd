import assert from "node:assert/strict";
import test from "node:test";
import { ingredientSql, recipeSql } from "../generation/generate.mjs";
import { getFeaturedOrderValidationError } from "../validation/validate.mjs";

test("ingredient SQL uses the deterministic dataset Blob name", () => {
  const sql = ingredientSql([{ data: {
    seedId: "ING-0001",
    name: "Chicken Breast",
    normalizedNutrition: {
      basisUnit: "g",
      values: { calories: 100, proteinG: 20, fatG: 2, carbohydratesG: 0 },
    },
    normalizedPrice: { usdPerBasis: 1.25 },
  } }]);

  assert.match(sql, /dataset\/ingredients\/ING-0001\/image\.avif/u);
  assert.doesNotMatch(sql, /chicken-breast-seed/u);
});

test("recipe SQL populates deterministic main and card Blob names", () => {
  const sql = recipeSql([{ data: {
    seedId: "REC-0001",
    name: "Test Recipe",
    description: "A test recipe.",
    instructions: ["Cook the recipe."],
    times: { prepMinutes: 5, cookMinutes: 10, totalMinutes: 20 },
    servings: 2,
    difficulty: "Easy",
    calculatedCost: { perServingUsd: 2.5 },
    calculatedNutrition: {
      total: { calories: 500, proteinG: 30, carbohydratesG: 40 },
      perServing: { calories: 250, proteinG: 15 },
    },
    createdAt: "2026-08-20T00:00:00Z",
    ingredients: [],
    diets: [],
    badges: [],
  } }], new Map());

  assert.match(sql, /image_blob_name, card_image_blob_name/u);
  assert.match(sql, /dataset\/recipes\/REC-0001\/main\.avif/u);
  assert.match(sql, /dataset\/recipes\/REC-0001\/card\.avif/u);
  assert.doesNotMatch(sql, /96ef8a25a7f4433e936a40e6aa6e33c0/u);
  assert.match(sql, /description, image_blob_name/u);
  assert.match(sql, /prep_time_minutes, cook_time_minutes, total_time_minutes/u);
  assert.match(sql, /calories_per_serving, protein_per_serving/u);
});

test("recipe SQL persists source display and featured ingredient order", () => {
  const sql = recipeSql([{ data: {
    seedId: "REC-0001",
    name: "Test Recipe",
    description: "A test recipe.",
    instructions: ["Cook the recipe."],
    times: { prepMinutes: 5, cookMinutes: 10, totalMinutes: 20 },
    servings: 2,
    difficulty: "Easy",
    calculatedCost: { perServingUsd: 2.5 },
    calculatedNutrition: {
      total: { calories: 500, proteinG: 30, carbohydratesG: 40 },
      perServing: { calories: 250, proteinG: 15 },
    },
    createdAt: "2026-08-20T00:00:00Z",
    ingredients: [
      { ingredientSeedId: "ING-0001", normalizedQuantity: 100, normalizedUnit: "g", sourceDisplay: "first", featuredOrder: 1 },
      { ingredientSeedId: "ING-0002", normalizedQuantity: 50, normalizedUnit: "g", sourceDisplay: "second" },
    ],
    diets: [],
    badges: [],
  } }], new Map([
    ["ING-0001", { name: "First" }],
    ["ING-0002", { name: "Second" }],
  ]));

  assert.match(sql, /display_order, featured_order/u);
  assert.match(sql, /100\.000, 'g', 1, 1, 'first'/u);
  assert.match(sql, /50\.000, 'g', 2, NULL, 'second'/u);
});

test("featured ingredient validation rejects duplicate and invalid orders", () => {
  assert.equal(
    getFeaturedOrderValidationError([{ featuredOrder: 1 }, { featuredOrder: 1 }]),
    "Featured orders must be unique within a recipe",
  );
  assert.equal(
    getFeaturedOrderValidationError([{ featuredOrder: 4 }]),
    "Featured orders must be integers from 1 to 3",
  );
  assert.equal(
    getFeaturedOrderValidationError([{}, {}]),
    "Recipe must explicitly feature 1-3 ingredients",
  );
  assert.equal(
    getFeaturedOrderValidationError([{ featuredOrder: 1 }, { featuredOrder: 3 }]),
    null,
  );
});

import assert from "node:assert/strict";
import test from "node:test";
import { ingredientSql, recipeSql } from "../generation/generate.mjs";

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
    instructions: ["Cook the recipe."],
    times: { totalMinutes: 20 },
    servings: 2,
    difficulty: "Easy",
    calculatedCost: { perServingUsd: 2.5 },
    calculatedNutrition: { total: { calories: 500, proteinG: 30, carbohydratesG: 40 } },
    createdAt: "2026-08-20T00:00:00Z",
    ingredients: [],
    diets: [],
    badges: [],
  } }], new Map());

  assert.match(sql, /image_blob_name, card_image_blob_name/u);
  assert.match(sql, /dataset\/recipes\/REC-0001\/main\.avif/u);
  assert.match(sql, /dataset\/recipes\/REC-0001\/card\.avif/u);
  assert.doesNotMatch(sql, /96ef8a25a7f4433e936a40e6aa6e33c0/u);
});

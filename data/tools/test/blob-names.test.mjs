import assert from "node:assert/strict";
import test from "node:test";
import {
  getIngredientImageBlobName,
  getRecipeCardImageBlobName,
  getRecipeMainImageBlobName,
} from "../shared/blob-names.mjs";

test("builds the exact deterministic ingredient Blob name", () => {
  assert.equal(
    getIngredientImageBlobName("ING-0001"),
    "dataset/ingredients/ING-0001/image.avif",
  );
});

test("builds the exact deterministic recipe Blob names", () => {
  assert.equal(
    getRecipeMainImageBlobName("REC-0001"),
    "dataset/recipes/REC-0001/main.avif",
  );
  assert.equal(
    getRecipeCardImageBlobName("REC-0001"),
    "dataset/recipes/REC-0001/card.avif",
  );
});

test("rejects invalid seed IDs instead of accepting path components", () => {
  for (const invalid of ["ING-1", "ing-0001", "../ING-0001", "REC-0001"]) {
    assert.throws(() => getIngredientImageBlobName(invalid), /Invalid ingredient seed ID/u);
  }

  for (const invalid of ["REC-1", "rec-0001", "../REC-0001", "ING-0001"]) {
    assert.throws(() => getRecipeMainImageBlobName(invalid), /Invalid recipe seed ID/u);
    assert.throws(() => getRecipeCardImageBlobName(invalid), /Invalid recipe seed ID/u);
  }
});

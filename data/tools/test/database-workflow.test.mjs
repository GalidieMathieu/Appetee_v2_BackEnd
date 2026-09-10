import assert from "node:assert/strict";
import { mkdir, mkdtemp, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import path from "node:path";
import test from "node:test";
import { assertNoResetArguments, resetLocalDatabase } from "../database/reset-local.mjs";
import { loadCanonicalExpectations } from "../database/verify-local.mjs";

const localConfig = async () => ({
  connectionString: "Server=localhost;Database=appetee;User ID=root;Password=never-print-me;",
});

test("reset CLI rejects every bypass-style argument", () => {
  assert.doesNotThrow(() => assertNoResetArguments([]));
  for (const args of [["--force"], ["--allow-remote"], ["--", "--force"]]) {
    assert.throws(() => assertNoResetArguments(args), /does not accept flags or bypass arguments/u);
  }
});

test("validation failure cannot reach database recreation", async () => {
  let destructiveCalls = 0;
  await assert.rejects(
    resetLocalDatabase({
      getDatabaseConfig: localConfig,
      runValidation: async () => { throw new Error("validation failed"); },
      runGeneration: async () => assert.fail("generation must not run"),
      recreateDatabase: async () => { destructiveCalls += 1; },
      write: () => {},
    }),
    /validation failed/u,
  );
  assert.equal(destructiveCalls, 0);
});

test("generation failure cannot reach database recreation", async () => {
  let destructiveCalls = 0;
  await assert.rejects(
    resetLocalDatabase({
      getDatabaseConfig: localConfig,
      runValidation: async () => {},
      runGeneration: async () => { throw new Error("generation failed"); },
      recreateDatabase: async () => { destructiveCalls += 1; },
      write: () => {},
    }),
    /generation failed/u,
  );
  assert.equal(destructiveCalls, 0);
});

test("verification expectations are computed from canonical JSON records", async (t) => {
  const dataDir = await mkdtemp(path.join(tmpdir(), "appetee-data-"));
  t.after(() => rm(dataDir, { recursive: true, force: true }));
  const ingredientRecords = [
    { directory: "0001_First", data: { seedId: "ING-0001", name: "First" } },
    { directory: "0002_Second", data: { seedId: "ING-0002", name: "Second" } },
  ];
  const recipeRecords = [
    {
      directory: "0001_First_Recipe",
      data: {
        seedId: "REC-0001",
        name: "First Recipe",
        ingredients: [{}, {}],
        diets: ["Vegan"],
        badges: ["Meal Prep", "Budget Friendly"],
      },
    },
    {
      directory: "0002_Second_Recipe",
      data: {
        seedId: "REC-0002",
        name: "Second Recipe",
        ingredients: [{}],
        diets: [],
        badges: [],
      },
    },
  ];

  for (const record of ingredientRecords) {
    const directory = path.join(dataDir, "ingredients", record.directory);
    await mkdir(directory, { recursive: true });
    await writeFile(path.join(directory, "ingredient.json"), JSON.stringify(record.data), "utf8");
  }
  for (const record of recipeRecords) {
    const directory = path.join(dataDir, "recipes", record.directory);
    await mkdir(directory, { recursive: true });
    await writeFile(path.join(directory, "recipe.json"), JSON.stringify(record.data), "utf8");
  }

  const expected = await loadCanonicalExpectations({ dataDir });
  assert.deepEqual(expected.counts, {
    ingredients: 2,
    ingredientNutrition: 2,
    recipes: 2,
    recipeIngredients: 3,
    dietRecipes: 1,
    recipeBadges: 2,
  });
  assert.equal(expected.ingredientImages.get("Second"), "dataset/ingredients/ING-0002/image.avif");
  assert.deepEqual(expected.recipeImages.get("First Recipe"), {
    main: "dataset/recipes/REC-0001/main.avif",
    card: "dataset/recipes/REC-0001/card.avif",
  });
});

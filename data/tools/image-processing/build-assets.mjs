import { createHash } from "node:crypto";
import { readdir, readFile, stat, writeFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";
import sharp from "sharp";

const toolDir = path.dirname(fileURLToPath(import.meta.url));
const dataDir = path.resolve(toolDir, "..", "..");

async function records(group, jsonName) {
  const root = path.join(dataDir, group);
  const entries = (await readdir(root, { withFileTypes: true })).filter((entry) => entry.isDirectory()).sort((a, b) => a.name.localeCompare(b.name));
  const output = [];
  for (const entry of entries) {
    const file = path.join(root, entry.name, jsonName);
    try { output.push({ dir: path.join(root, entry.name), data: JSON.parse(await readFile(file, "utf8")) }); } catch {}
  }
  return output;
}

async function inspectAsset(seedId, file, expectedWidth, expectedHeight, maximumBytes) {
  const buffer = await readFile(file);
  const info = await stat(file);
  const metadata = await sharp(buffer).metadata();
  if (metadata.format !== "heif" || metadata.compression !== "av1") throw new Error(`${seedId}: ${file} is not AVIF`);
  if (metadata.width !== expectedWidth || metadata.height !== expectedHeight) throw new Error(`${seedId}: ${file} is ${metadata.width}x${metadata.height}`);
  if (info.size > maximumBytes) throw new Error(`${seedId}: ${file} exceeds ${maximumBytes} bytes`);
  return { seedId, bytes: info.size, hash: createHash("sha256").update(buffer).digest("hex") };
}

async function inspectRecordAsset(record, file, expectedWidth, expectedHeight, maximumBytes) {
  try { return await inspectAsset(record.data.seedId, file, expectedWidth, expectedHeight, maximumBytes); }
  catch (error) {
    if (record.data.image?.needsGeneratedImage && error.code === "ENOENT") return null;
    throw error;
  }
}

function sizeRange(items) {
  if (!items.length) return { minimumBytes: null, maximumBytes: null };
  return { minimumBytes: Math.min(...items.map((item) => item.bytes)), maximumBytes: Math.max(...items.map((item) => item.bytes)) };
}

function duplicateGroups(items) {
  const byHash = new Map();
  for (const item of items) byHash.set(item.hash, [...(byHash.get(item.hash) ?? []), item.seedId]);
  return [...byHash.entries()].filter(([, seedIds]) => seedIds.length > 1).map(([hash, seedIds]) => ({ hash, seedIds }));
}

async function main() {
  const recipeRecords = await records("recipes", "recipe.json");
  const ingredientRecords = await records("ingredients", "ingredient.json");
  const recipeMain = [];
  const recipeCard = [];
  const ingredients = [];
  for (const record of recipeRecords) {
    const main = await inspectRecordAsset(record, path.join(record.dir, "assets", "main.avif"), 1200, 800, 200 * 1024);
    const card = await inspectRecordAsset(record, path.join(record.dir, "assets", "card.avif"), 480, 320, 80 * 1024);
    if (main) recipeMain.push(main);
    if (card) recipeCard.push(card);
  }
  for (const record of ingredientRecords) {
    const asset = await inspectRecordAsset(record, path.join(record.dir, "assets", "image.avif"), 256, 256, 40 * 1024);
    if (asset) ingredients.push(asset);
  }

  const duplicateRecipeImages = duplicateGroups(recipeMain);
  const duplicateIngredientImages = duplicateGroups(ingredients);
  if (duplicateRecipeImages.length || duplicateIngredientImages.length) throw new Error("Duplicate record-specific image content detected; see image-manifest.json after resolving it");
  const pendingRecipeImages = recipeRecords.filter((item) => item.data.image?.needsGeneratedImage).length;
  const pendingIngredientImages = ingredientRecords.filter((item) => item.data.image?.needsGeneratedImage).length;
  const manifest = {
    generatedAt: new Date().toISOString(),
    policy: "Record-specific AVIF assets are preserved; this audit never overwrites them with a shared fallback.",
    provenance: {
      sourcePage: [...recipeRecords, ...ingredientRecords].filter((item) => item.data.image?.sourcePageUrl).length,
      generated: [...recipeRecords, ...ingredientRecords].filter((item) => item.data.image?.generationPrompt).length,
      productionApproved: [...recipeRecords, ...ingredientRecords].filter((item) => item.data.image?.productionApproved).length,
    },
    outputs: {
      recipeMain: { count: recipeMain.length, uniqueHashes: new Set(recipeMain.map((item) => item.hash)).size, width: 1200, height: 800, ...sizeRange(recipeMain) },
      recipeCard: { count: recipeCard.length, uniqueHashes: new Set(recipeCard.map((item) => item.hash)).size, width: 480, height: 320, ...sizeRange(recipeCard) },
      ingredient: { count: ingredients.length, uniqueHashes: new Set(ingredients.map((item) => item.hash)).size, width: 256, height: 256, ...sizeRange(ingredients) },
    },
    duplicateRecipeImages,
    duplicateIngredientImages,
    pendingRecordSpecificImages: pendingRecipeImages + pendingIngredientImages,
  };
  await writeFile(path.join(dataDir, "image-manifest.json"), `${JSON.stringify(manifest, null, 2)}\n`);
  const sourceManifest = {
    generatedAt: manifest.generatedAt,
    policy: "Consolidated record-level provenance for non-production test assets; licenses are not verified for production redistribution.",
    ingredients: ingredientRecords.map((record) => ({ seedId: record.data.seedId, name: record.data.name, status: record.data.image?.needsGeneratedImage ? "pending-real-image" : "record-specific", sourcePageUrl: record.data.image?.sourcePageUrl, originalSourceUrl: record.data.image?.originalSourceUrl, provenance: record.data.image?.provenance, license: record.data.image?.license, productionApproved: Boolean(record.data.image?.productionApproved), assetHash: ingredients.find((asset) => asset.seedId === record.data.seedId)?.hash ?? null })),
    recipes: recipeRecords.map((record) => ({ seedId: record.data.seedId, name: record.data.name, status: record.data.image?.needsGeneratedImage ? "pending-real-image" : "record-specific", sourcePageUrl: record.data.image?.sourcePageUrl, originalSourceUrl: record.data.image?.originalSourceUrl, provenance: record.data.image?.provenance, license: record.data.image?.license, productionApproved: Boolean(record.data.image?.productionApproved), mainAssetHash: recipeMain.find((asset) => asset.seedId === record.data.seedId)?.hash ?? null, cardAssetHash: recipeCard.find((asset) => asset.seedId === record.data.seedId)?.hash ?? null })),
    counts: { recordSpecificIngredients: ingredientRecords.length - pendingIngredientImages, pendingIngredients: pendingIngredientImages, recordSpecificRecipes: recipeRecords.length - pendingRecipeImages, pendingRecipes: pendingRecipeImages },
  };
  await writeFile(path.join(dataDir, "image-source-manifest.json"), `${JSON.stringify(sourceManifest, null, 2)}\n`);
  process.stdout.write(`Audited ${recipeMain.length} available recipe and ${ingredients.length} available ingredient assets; all primary images are distinct.\n`);
}

main().catch((error) => { process.stderr.write(`${error.stack ?? error}\n`); process.exitCode = 1; });

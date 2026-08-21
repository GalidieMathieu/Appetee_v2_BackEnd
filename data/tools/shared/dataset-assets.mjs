import { createHash } from "node:crypto";
import { readdir, readFile, realpath, stat } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";
import {
  getIngredientImageBlobName,
  getRecipeCardImageBlobName,
  getRecipeMainImageBlobName,
} from "./blob-names.mjs";

const sharedDir = path.dirname(fileURLToPath(import.meta.url));
export const defaultDataDir = path.resolve(sharedDir, "..", "..");

function isOutside(parent, candidate) {
  const relative = path.relative(parent, candidate);
  return relative === ".." || relative.startsWith(`..${path.sep}`) || path.isAbsolute(relative);
}

async function readJson(filePath, description) {
  try {
    return JSON.parse(String(await readFile(filePath, "utf8")).replace(/^\uFEFF/u, ""));
  } catch (error) {
    throw new Error(`Could not read ${description} at ${filePath}: ${error.message}`, { cause: error });
  }
}

export async function resolveDatasetAsset(recordPath, relativeAssetPath) {
  if (typeof relativeAssetPath !== "string" || relativeAssetPath.trim().length === 0) {
    throw new Error(`Missing asset path in ${recordPath}`);
  }
  if (path.isAbsolute(relativeAssetPath)) {
    throw new Error(`Asset path must be relative to its record directory: ${relativeAssetPath}`);
  }

  const recordDir = path.dirname(path.resolve(recordPath));
  const localPath = path.resolve(recordDir, relativeAssetPath);
  if (isOutside(recordDir, localPath)) {
    throw new Error(`Asset path escapes its record directory: ${relativeAssetPath}`);
  }
  if (path.extname(localPath) !== ".avif") {
    throw new Error(`Dataset asset must use the .avif extension: ${localPath}`);
  }

  let details;
  try {
    details = await stat(localPath);
  } catch (error) {
    if (error?.code === "ENOENT") {
      throw new Error(`Dataset asset does not exist: ${localPath}`, { cause: error });
    }
    throw error;
  }
  if (!details.isFile()) {
    throw new Error(`Dataset asset is not a regular file: ${localPath}`);
  }

  const [realRecordDir, realAssetPath] = await Promise.all([realpath(recordDir), realpath(localPath)]);
  if (isOutside(realRecordDir, realAssetPath)) {
    throw new Error(`Dataset asset resolves outside its record directory: ${relativeAssetPath}`);
  }

  return realAssetPath;
}

export async function sha256File(filePath) {
  return createHash("sha256").update(await readFile(filePath)).digest("hex");
}

async function recordDirectories(root) {
  return (await readdir(root, { withFileTypes: true }))
    .filter((entry) => entry.isDirectory())
    .sort((left, right) => left.name.localeCompare(right.name));
}

async function addAsset(inventory, seenBlobNames, {
  assetType,
  blobName,
  entityType,
  hashFile,
  image,
  recordPath,
  relativeAssetPath,
  seedId,
}) {
  if (seenBlobNames.has(blobName)) {
    throw new Error(`Duplicate dataset Blob name: ${blobName}`);
  }
  seenBlobNames.add(blobName);

  const localPath = await resolveDatasetAsset(recordPath, relativeAssetPath);
  inventory.push({
    seedId,
    entityType,
    assetType,
    recordPath,
    localPath,
    blobName,
    productionApproved: image?.productionApproved === true,
    sha256: await hashFile(localPath),
  });
}

export async function buildDatasetAssetInventory({
  dataDir = defaultDataDir,
  hashFile = sha256File,
} = {}) {
  const inventory = [];
  const seenBlobNames = new Set();

  for (const entry of await recordDirectories(path.join(dataDir, "ingredients"))) {
    const recordPath = path.join(dataDir, "ingredients", entry.name, "ingredient.json");
    const record = await readJson(recordPath, "ingredient record");
    const blobName = getIngredientImageBlobName(record.seedId);
    await addAsset(inventory, seenBlobNames, {
      assetType: "ingredient",
      blobName,
      entityType: "ingredient",
      hashFile,
      image: record.image,
      recordPath,
      relativeAssetPath: record.image?.path,
      seedId: record.seedId,
    });
  }

  for (const entry of await recordDirectories(path.join(dataDir, "recipes"))) {
    const recordPath = path.join(dataDir, "recipes", entry.name, "recipe.json");
    const record = await readJson(recordPath, "recipe record");
    const variants = [
      ["recipe-main", record.image?.mainPath, getRecipeMainImageBlobName(record.seedId)],
      ["recipe-card", record.image?.cardPath, getRecipeCardImageBlobName(record.seedId)],
    ];
    for (const [assetType, relativeAssetPath, blobName] of variants) {
      await addAsset(inventory, seenBlobNames, {
        assetType,
        blobName,
        entityType: "recipe",
        hashFile,
        image: record.image,
        recordPath,
        relativeAssetPath,
        seedId: record.seedId,
      });
    }
  }

  return inventory;
}

export async function loadDatasetVersion({ dataDir = defaultDataDir } = {}) {
  try {
    const version = await readJson(path.join(dataDir, "version.json"), "dataset version");
    return typeof version.datasetVersion === "string" && version.datasetVersion.trim()
      ? version.datasetVersion.trim()
      : undefined;
  } catch (error) {
    if (error?.cause?.code === "ENOENT") return undefined;
    throw error;
  }
}

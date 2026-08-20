import { createHash } from "node:crypto";
import { mkdir, readdir, readFile, writeFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";
import sharp from "sharp";
import { syncImageQueues } from "./sync-image-queues.mjs";

const toolDir = path.dirname(fileURLToPath(import.meta.url));
const dataDir = path.resolve(toolDir, "..", "..");
const checkedAt = new Date().toISOString();
const userAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/127.0 Safari/537.36";
const requestedSeedIds = new Set((process.env.APPETEE_IMAGE_SEED_IDS ?? "").split(",").map((value) => value.trim()).filter(Boolean));
const requestedConcurrency = Math.max(1, Number(process.env.APPETEE_IMAGE_CONCURRENCY ?? 2));
const sourceOverrides = new Map(Object.entries(JSON.parse(process.env.APPETEE_IMAGE_SOURCE_OVERRIDES ?? "{}")));

function decodeHtml(value) {
  return value.replaceAll("&amp;", "&").replaceAll("&#x2F;", "/").replaceAll("&quot;", '"').replaceAll("\\u002F", "/").replaceAll("\\/", "/");
}

function metadataImage(html, pageUrl) {
  for (const tag of html.match(/<meta\b[^>]*>/gi) ?? []) {
    const property = tag.match(/(?:property|name)\s*=\s*["']([^"']+)["']/i)?.[1]?.toLowerCase();
    if (!["og:image", "og:image:url", "twitter:image", "twitter:image:src"].includes(property)) continue;
    const content = tag.match(/content\s*=\s*["']([^"']+)["']/i)?.[1];
    if (content) return new URL(decodeHtml(content), pageUrl).href;
  }
  const jsonLdPatterns = [
    /"image"\s*:\s*\{[^{}]{0,500}?"url"\s*:\s*"(https?:\\?\/\\?\/[^"\\]+(?:\\.[^"\\]+)*)"/i,
    /"image"\s*:\s*\[\s*"(https?:\\?\/\\?\/[^"\\]+)"/i,
    /"image"\s*:\s*"(https?:\\?\/\\?\/[^"\\]+)"/i,
  ];
  for (const pattern of jsonLdPatterns) {
    const value = html.match(pattern)?.[1];
    if (value) return decodeHtml(value);
  }
  return null;
}

async function fetchPageImage(pageUrl, explicitImageUrl = null) {
  let imageUrl = explicitImageUrl;
  let resolvedPageUrl = pageUrl;
  let pageStatus = null;
  if (!imageUrl) {
    const page = await fetch(pageUrl, { redirect: "follow", headers: { "user-agent": userAgent, "accept-language": "en-US,en;q=0.8" } });
    const contentType = page.headers.get("content-type") ?? "";
    if (!contentType.includes("text/html")) throw new Error(`page returned ${page.status} ${contentType}`);
    const html = await page.text();
    resolvedPageUrl = page.url || pageUrl;
    pageStatus = page.status;
    imageUrl = metadataImage(html, resolvedPageUrl);
    if (!imageUrl) throw new Error(`no public image metadata (HTTP ${page.status})`);
  }
  const imageResponse = await fetch(imageUrl, { redirect: "follow", headers: { "user-agent": userAgent, referer: resolvedPageUrl } });
  if (!imageResponse.ok) throw new Error(`image returned HTTP ${imageResponse.status}`);
  const buffer = Buffer.from(await imageResponse.arrayBuffer());
  if (buffer.length < 1024) throw new Error("image payload too small");
  const metadata = await sharp(buffer).metadata();
  if (!metadata.width || !metadata.height) throw new Error("image decoder found no dimensions");
  if (metadata.width < 300 || metadata.height < 200) throw new Error(`source image too small (${metadata.width}x${metadata.height})`);
  if (/(?:logo|avatar|gravatar|sharegraphic|twittercard|favicon|404[-_.])/iu.test(imageUrl)) throw new Error("source metadata resolved to a logo, avatar, social card, or error image");
  return { imageUrl, buffer, pageStatus, width: metadata.width, height: metadata.height };
}

async function avif(source, width, height, maxBytes, startQuality) {
  for (let quality = startQuality; quality >= 25; quality -= 5) {
    const buffer = await sharp(source).rotate().resize(width, height, { fit: "cover", position: "centre" }).avif({ quality, effort: 4, chromaSubsampling: "4:2:0" }).toBuffer();
    if (buffer.length <= maxBytes) return { buffer, quality };
  }
  throw new Error(`unable to encode ${width}x${height} below ${maxBytes} bytes`);
}

async function loadRecords(group, jsonName) {
  const root = path.join(dataDir, group);
  const entries = (await readdir(root, { withFileTypes: true })).filter((entry) => entry.isDirectory()).sort((a, b) => a.name.localeCompare(b.name));
  const records = [];
  for (const entry of entries) {
    const file = path.join(root, entry.name, jsonName);
    try {
      const data = JSON.parse(await readFile(file, "utf8"));
      if (data.image?.needsGeneratedImage && (!requestedSeedIds.size || requestedSeedIds.has(data.seedId))) records.push({ group, dir: path.join(root, entry.name), file, data });
    } catch {}
  }
  return records;
}

async function usedSourcePages(group, jsonName) {
  const root = path.join(dataDir, group);
  const used = new Map();
  for (const entry of (await readdir(root, { withFileTypes: true })).filter((item) => item.isDirectory())) {
    try {
      const data = JSON.parse(await readFile(path.join(root, entry.name, jsonName), "utf8"));
      if (data.image?.needsGeneratedImage) continue;
      const sourcePageUrl = data.image?.sourcePageUrl;
      if (sourcePageUrl) used.set(sourcePageUrl, data.seedId);
    } catch {}
  }
  return used;
}

function uniqueSourceRecords(records, usedPages) {
  const selected = [];
  const deferred = [];
  const selectedByPage = new Map();
  for (const record of records) {
    const override = sourceOverrides.get(record.data.seedId);
    const pageUrl = override?.pageUrl ?? (record.group === "recipes" ? record.data.source.url : record.data.market.productUrl);
    const existingSeedId = usedPages.get(pageUrl) ?? selectedByPage.get(pageUrl);
    if (existingSeedId) {
      deferred.push({ seedId: record.data.seedId, status: "pending-related-image", pageUrl, error: `exact source-page image already assigned to ${existingSeedId}` });
      continue;
    }
    selectedByPage.set(pageUrl, record.data.seedId);
    selected.push(record);
  }
  return { selected, deferred };
}

async function processRecord(record, seenHashes) {
  const isRecipe = record.group === "recipes";
  const override = sourceOverrides.get(record.data.seedId);
  const pageUrl = override?.pageUrl ?? (isRecipe ? record.data.source.url : record.data.market.productUrl);
  const fetched = await fetchPageImage(pageUrl, override?.imageUrl ?? null);
  const sourceHash = createHash("sha256").update(fetched.buffer).digest("hex");
  if (seenHashes.has(sourceHash)) throw new Error(`duplicate source image also used by ${seenHashes.get(sourceHash)}`);
  seenHashes.set(sourceHash, record.data.seedId);

  if (isRecipe) {
    const main = await avif(fetched.buffer, 1200, 800, 200 * 1024, 75);
    const card = await avif(main.buffer, 480, 320, 80 * 1024, 78);
    await mkdir(path.join(record.dir, "assets"), { recursive: true });
    await writeFile(path.join(record.dir, "assets", "main.avif"), main.buffer);
    await writeFile(path.join(record.dir, "assets", "card.avif"), card.buffer);
    record.data.image = {
      mainPath: "assets/main.avif",
      cardPath: "assets/card.avif",
      imageType: "external-real-photograph",
      aiGenerated: false,
      provider: new URL(fetched.imageUrl).hostname,
      creator: null,
      credit: null,
      provenance: "Exact recipe source-page photograph retained for private, non-production test data at the repository owner's direction.",
      sourcePageUrl: pageUrl,
      originalSourceUrl: fetched.imageUrl,
      license: null,
      licenseUrl: null,
      usageAuthorization: "Repository-owner-authorized private/test use only.",
      attributionRequired: null,
      reuseVerified: false,
      productionApproved: false,
      acquiredAt: checkedAt,
      needsGeneratedImage: false,
    };
    await writeFile(record.file, `${JSON.stringify(record.data, null, 2)}\n`);
    return { seedId: record.data.seedId, status: "downloaded", pageUrl, imageUrl: fetched.imageUrl, sourceHash, mainBytes: main.buffer.length, cardBytes: card.buffer.length };
  }

  const output = await avif(fetched.buffer, 256, 256, 40 * 1024, 75);
  await mkdir(path.join(record.dir, "assets"), { recursive: true });
  await writeFile(path.join(record.dir, "assets", "image.avif"), output.buffer);
  record.data.image = {
    path: "assets/image.avif",
    imageType: "external-real-product-photograph",
    aiGenerated: false,
    provider: new URL(fetched.imageUrl).hostname,
    creator: null,
    credit: null,
    provenance: "Public Walmart product image copied for non-production test data at the repository owner's direction.",
    sourcePageUrl: pageUrl,
    originalSourceUrl: fetched.imageUrl,
    license: null,
    licenseUrl: null,
    usageAuthorization: "Repository-owner-authorized private/test use only.",
    attributionRequired: null,
    reuseVerified: false,
    productionApproved: false,
    acquiredAt: checkedAt,
    needsGeneratedImage: false,
  };
  await writeFile(record.file, `${JSON.stringify(record.data, null, 2)}\n`);
  return { seedId: record.data.seedId, status: "downloaded", pageUrl, imageUrl: fetched.imageUrl, sourceHash, bytes: output.buffer.length };
}

async function processPool(records, concurrency, seenHashes) {
  const results = [];
  let cursor = 0;
  async function worker() {
    while (cursor < records.length) {
      const record = records[cursor++];
      try { results.push(await processRecord(record, seenHashes)); }
      catch (error) { results.push({ seedId: record.data.seedId, status: "pending-real-image", pageUrl: record.group === "recipes" ? record.data.source.url : record.data.market.productUrl, error: error.message }); }
      await new Promise((resolve) => setTimeout(resolve, 250));
    }
  }
  await Promise.all(Array.from({ length: concurrency }, () => worker()));
  return results.sort((a, b) => a.seedId.localeCompare(b.seedId));
}

async function main() {
  const ingredientRecords = await loadRecords("ingredients", "ingredient.json");
  const recipeRecords = await loadRecords("recipes", "recipe.json");
  const uniqueIngredients = uniqueSourceRecords(ingredientRecords, await usedSourcePages("ingredients", "ingredient.json"));
  const uniqueRecipes = uniqueSourceRecords(recipeRecords, await usedSourcePages("recipes", "recipe.json"));
  const ingredientResults = [...await processPool(uniqueIngredients.selected, requestedConcurrency, new Map()), ...uniqueIngredients.deferred].sort((a, b) => a.seedId.localeCompare(b.seedId));
  const recipeResults = [...await processPool(uniqueRecipes.selected, requestedConcurrency, new Map()), ...uniqueRecipes.deferred].sort((a, b) => a.seedId.localeCompare(b.seedId));
  let previousManifest = { ingredients: [], recipes: [] };
  try { previousManifest = JSON.parse(await readFile(path.join(dataDir, "generated", "manifests", "source-acquisition.json"), "utf8")); } catch {}
  const mergedIngredientsBySeedId = new Map((previousManifest.ingredients ?? []).map((item) => [item.seedId, item]));
  const mergedRecipesBySeedId = new Map((previousManifest.recipes ?? []).map((item) => [item.seedId, item]));
  for (const result of ingredientResults) mergedIngredientsBySeedId.set(result.seedId, result);
  for (const result of recipeResults) mergedRecipesBySeedId.set(result.seedId, result);
  const mergedIngredientResults = [...mergedIngredientsBySeedId.values()].sort((a, b) => a.seedId.localeCompare(b.seedId));
  const mergedRecipeResults = [...mergedRecipesBySeedId.values()].sort((a, b) => a.seedId.localeCompare(b.seedId));
  const manifest = {
    acquiredAt: checkedAt,
    policy: "Exact source-page real photographs are private/test-only unless reuse is independently verified; every download requires visual dish review, unobtrusive photographer credit watermarks are allowed, missing images remain pending, and AI generation is prohibited.",
    ingredients: mergedIngredientResults,
    recipes: mergedRecipeResults,
    counts: {
      downloadedIngredients: mergedIngredientResults.filter((item) => item.status === "downloaded").length,
      pendingIngredients: mergedIngredientResults.filter((item) => item.status !== "downloaded").length,
      downloadedRecipes: mergedRecipeResults.filter((item) => item.status === "downloaded").length,
      pendingRecipes: mergedRecipeResults.filter((item) => item.status !== "downloaded").length,
    },
    currentRunCounts: {
      downloadedIngredients: ingredientResults.filter((item) => item.status === "downloaded").length,
      pendingIngredients: ingredientResults.filter((item) => item.status !== "downloaded").length,
      downloadedRecipes: recipeResults.filter((item) => item.status === "downloaded").length,
      pendingRecipes: recipeResults.filter((item) => item.status !== "downloaded").length,
    },
  };
  await writeFile(path.join(dataDir, "generated", "manifests", "source-acquisition.json"), `${JSON.stringify(manifest, null, 2)}\n`);
  const queueCounts = await syncImageQueues();
  process.stdout.write(`${JSON.stringify({ ...manifest.currentRunCounts, cumulativeCounts: manifest.counts, queuedIngredients: queueCounts.ingredients, queuedRecipes: queueCounts.recipes })}\n`);
}

main().catch((error) => { process.stderr.write(`${error.stack ?? error}\n`); process.exitCode = 1; });

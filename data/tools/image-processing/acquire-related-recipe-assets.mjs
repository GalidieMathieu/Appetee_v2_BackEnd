import { createHash } from "node:crypto";
import { mkdir, readdir, readFile, writeFile } from "node:fs/promises";
import path from "node:path";
import sharp from "sharp";
import { syncImageQueues } from "./sync-image-queues.mjs";

const dataDir = path.resolve(import.meta.dirname, "..", "..");
const recipeRoot = path.join(dataDir, "recipes");
const requestedSeedIds = new Set((process.env.APPETEE_IMAGE_SEED_IDS ?? "").split(",").map((value) => value.trim()).filter(Boolean));
const concurrency = Math.max(1, Number(process.env.APPETEE_IMAGE_CONCURRENCY ?? 4));
const limit = Math.max(1, Number(process.env.APPETEE_RELATED_LIMIT ?? Number.MAX_SAFE_INTEGER));
const userAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/127.0 Safari/537.36";
const ignoredWords = new Set(["recipe", "recipes", "easy", "best", "healthy", "homemade", "style", "with", "and", "the", "a", "an", "for", "of"]);
const rejectedUrlPattern = /(?:logo|avatar|gravatar|sharegraphic|twittercard|favicon|sprite|icon|placeholder|404[-_.]|\.svg(?:\?|$))/iu;

const decodeHtml = (value) => value.replaceAll("&quot;", '"').replaceAll("&#34;", '"').replaceAll("&amp;", "&").replaceAll("&#39;", "'").replaceAll("&lt;", "<").replaceAll("&gt;", ">");
const normalize = (value) => decodeHtml(String(value)).toLowerCase().replace(/<[^>]+>/gu, " ").replace(/[^a-z0-9]+/gu, " ").replace(/\s+/gu, " ").trim();
const hash = (buffer) => createHash("sha256").update(buffer).digest("hex");

async function avif(source, width, height, maxBytes, startQuality) {
  for (let quality = startQuality; quality >= 25; quality -= 5) {
    const buffer = await sharp(source).rotate().resize(width, height, { fit: "cover", position: "centre" }).avif({ quality, effort: 8, chromaSubsampling: "4:2:0" }).toBuffer();
    if (buffer.length <= maxBytes) return { buffer, quality };
  }
  throw new Error(`unable to encode ${width}x${height} below ${maxBytes} bytes`);
}

async function loadState() {
  const pending = [];
  const usedUrls = new Set();
  const outputHashes = new Map();
  for (const entry of (await readdir(recipeRoot, { withFileTypes: true })).filter((item) => item.isDirectory()).sort((a, b) => a.name.localeCompare(b.name))) {
    const file = path.join(recipeRoot, entry.name, "recipe.json");
    try {
      const data = JSON.parse(await readFile(file, "utf8"));
      if (data.image?.needsGeneratedImage) {
        if (!requestedSeedIds.size || requestedSeedIds.has(data.seedId)) pending.push({ dir: path.dirname(file), file, data });
      } else {
        if (data.image?.originalSourceUrl) usedUrls.add(data.image.originalSourceUrl);
        try { outputHashes.set(hash(await readFile(path.join(path.dirname(file), "assets", "main.avif"))), data.seedId); } catch {}
      }
    } catch {}
  }
  return { pending: pending.slice(0, limit), usedUrls, outputHashes };
}

function parseResults(html, recipeName) {
  const targetWords = normalize(recipeName).split(" ").filter((word) => word.length > 2 && !ignoredWords.has(word));
  const results = [];
  for (const match of html.matchAll(/class="iusc"[^>]*\sm="([^"]+)"/giu)) {
    try {
      const item = JSON.parse(decodeHtml(match[1]));
      if (!item.murl || !item.purl || rejectedUrlPattern.test(item.murl)) continue;
      const haystack = normalize(`${item.t ?? ""} ${item.desc ?? ""} ${item.murl} ${item.purl}`);
      const hits = targetWords.filter((word) => haystack.includes(word)).length;
      const score = targetWords.length ? hits / targetWords.length : 0;
      results.push({ ...item, score });
    } catch {}
  }
  return results.sort((a, b) => b.score - a.score).slice(0, 16);
}

async function search(recipe) {
  const searchQuery = `${recipe.name} plated recipe food`;
  const searchUrl = `https://www.bing.com/images/search?q=${encodeURIComponent(searchQuery)}&form=HDRSC2&first=1`;
  const response = await fetch(searchUrl, { signal: AbortSignal.timeout(20000), headers: { "user-agent": userAgent, "accept-language": "en-US,en;q=0.9" } });
  if (!response.ok) throw new Error(`Bing Image Search returned HTTP ${response.status}`);
  return { searchQuery, results: parseResults(await response.text(), recipe.name) };
}

async function fetchImage(url, referer) {
  const response = await fetch(url, { redirect: "follow", signal: AbortSignal.timeout(20000), headers: { "user-agent": userAgent, referer, accept: "image/avif,image/webp,image/apng,image/*,*/*;q=0.8" } });
  if (!response.ok) throw new Error(`image returned HTTP ${response.status}`);
  const contentType = response.headers.get("content-type") ?? "";
  if (!contentType.startsWith("image/")) throw new Error(`image returned ${contentType || "unknown content type"}`);
  const buffer = Buffer.from(await response.arrayBuffer());
  if (buffer.length < 4096) throw new Error("image payload too small");
  const metadata = await sharp(buffer).metadata();
  if (!metadata.width || !metadata.height || metadata.width < 250 || metadata.height < 180) throw new Error(`image dimensions are too small (${metadata.width ?? 0}x${metadata.height ?? 0})`);
  const ratio = metadata.width / metadata.height;
  if (ratio < 0.45 || ratio > 2.4) throw new Error(`image aspect ratio ${ratio.toFixed(2)} is unsuitable`);
  return { buffer, width: metadata.width, height: metadata.height };
}

async function processRecord(record, usedUrls, outputHashes) {
  let searched;
  try { searched = await search(record.data); }
  catch (error) { return { seedId: record.data.seedId, name: record.data.name, status: "pending-related-image", error: error.message }; }
  for (const result of searched.results) {
    for (const candidateUrl of [result.turl, result.murl].filter(Boolean)) {
      if (usedUrls.has(candidateUrl) || rejectedUrlPattern.test(candidateUrl)) continue;
      usedUrls.add(candidateUrl);
      try {
        const fetched = await fetchImage(candidateUrl, result.purl);
        const sourceHash = hash(fetched.buffer);
        const main = await avif(fetched.buffer, 1200, 800, 200 * 1024, 75);
        const mainHash = hash(main.buffer);
        if (outputHashes.has(mainHash)) throw new Error(`encoded image duplicates ${outputHashes.get(mainHash)}`);
        const card = await avif(main.buffer, 480, 320, 80 * 1024, 78);
        await mkdir(path.join(record.dir, "assets"), { recursive: true });
        await writeFile(path.join(record.dir, "assets", "main.avif"), main.buffer);
        await writeFile(path.join(record.dir, "assets", "card.avif"), card.buffer);
        outputHashes.set(mainHash, record.data.seedId);
        const acquiredAt = new Date().toISOString();
        record.data.image = {
          mainPath: "assets/main.avif",
          cardPath: "assets/card.avif",
          imageType: "external-real-related-photograph",
          aiGenerated: false,
          provider: new URL(candidateUrl).hostname,
          discoveryProvider: "Bing Image Search",
          creator: null,
          credit: null,
          provenance: "Related real food photograph found through Bing Image Search and retained for private, non-production test data at the repository owner's direction.",
          sourcePageUrl: result.purl,
          originalSourceUrl: candidateUrl,
          discoveredOriginalUrl: result.murl,
          searchQuery: searched.searchQuery,
          searchResultTitle: result.t ?? null,
          license: null,
          licenseUrl: null,
          usageAuthorization: "Repository-owner-authorized private/test use only.",
          attributionRequired: null,
          reuseVerified: false,
          productionApproved: false,
          acquiredAt,
          needsGeneratedImage: false,
        };
        await writeFile(record.file, `${JSON.stringify(record.data, null, 2)}\n`);
        return { seedId: record.data.seedId, name: record.data.name, status: "downloaded-related-real-image", searchQuery: searched.searchQuery, sourcePageUrl: result.purl, imageUrl: candidateUrl, discoveredOriginalUrl: result.murl, sourceHash, mainHash, sourceWidth: fetched.width, sourceHeight: fetched.height, mainBytes: main.buffer.length, cardBytes: card.buffer.length };
      } catch {
        usedUrls.delete(candidateUrl);
      }
    }
  }
  return { seedId: record.data.seedId, name: record.data.name, status: "pending-related-image", searchQuery: searched.searchQuery, error: "No distinct decodable related photograph passed the size and format checks." };
}

const state = await loadState();
const results = [];
let cursor = 0;
let completed = 0;
let downloaded = 0;
async function worker() {
  while (cursor < state.pending.length) {
    const record = state.pending[cursor++];
    const result = await processRecord(record, state.usedUrls, state.outputHashes);
    results.push(result);
    completed += 1;
    if (result.status === "downloaded-related-real-image") downloaded += 1;
    if (completed % 25 === 0) process.stdout.write(`processed=${completed} downloaded=${downloaded} pending=${completed - downloaded}\n`);
    await new Promise((resolve) => setTimeout(resolve, 200));
  }
}
await Promise.all(Array.from({ length: concurrency }, () => worker()));
results.sort((a, b) => a.seedId.localeCompare(b.seedId));
let previousResults = [];
try { previousResults = JSON.parse(await readFile(path.join(dataDir, "image-related-acquisition-manifest.json"), "utf8")).recipes ?? []; } catch {}
for (const entry of (await readdir(recipeRoot, { withFileTypes: true })).filter((item) => item.isDirectory())) {
  try {
    const data = JSON.parse(await readFile(path.join(recipeRoot, entry.name, "recipe.json"), "utf8"));
    if (data.image?.discoveryProvider !== "Bing Image Search" || data.image?.needsGeneratedImage) continue;
    previousResults.push({ seedId: data.seedId, name: data.name, status: "downloaded-related-real-image", searchQuery: data.image.searchQuery, sourcePageUrl: data.image.sourcePageUrl, imageUrl: data.image.originalSourceUrl, discoveredOriginalUrl: data.image.discoveredOriginalUrl, recoveredFromRecipeRecord: true });
  } catch {}
}
const mergedBySeedId = new Map(previousResults.map((item) => [item.seedId, item]));
for (const result of results) mergedBySeedId.set(result.seedId, result);
const mergedResults = [...mergedBySeedId.values()].sort((a, b) => a.seedId.localeCompare(b.seedId));
const manifest = { acquiredAt: new Date().toISOString(), policy: "Distinct related real-food photographs are preferred over AI generation and retained only for repository-owner-authorized private test use; public redistribution rights are not verified.", counts: { attempted: mergedResults.length, downloaded: mergedResults.filter((item) => item.status === "downloaded-related-real-image").length, pending: mergedResults.filter((item) => item.status !== "downloaded-related-real-image").length }, recipes: mergedResults };
await writeFile(path.join(dataDir, "image-related-acquisition-manifest.json"), `${JSON.stringify(manifest, null, 2)}\n`);
const queues = await syncImageQueues();
process.stdout.write(`${JSON.stringify({ ...manifest.counts, queuedRecipes: queues.recipes })}\n`);

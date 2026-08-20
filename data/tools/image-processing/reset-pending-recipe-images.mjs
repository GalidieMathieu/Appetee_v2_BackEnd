import { readdir, readFile, rm, writeFile } from "node:fs/promises";
import path from "node:path";

const dataDir = path.resolve(import.meta.dirname, "..", "..");
const recipeRoot = path.join(dataDir, "recipes");
const requested = new Set((process.env.APPETEE_IMAGE_SEED_IDS ?? "").split(",").map((value) => value.trim()).filter(Boolean));
if (!requested.size) throw new Error("APPETEE_IMAGE_SEED_IDS is required");
let reset = 0;
for (const entry of await readdir(recipeRoot, { withFileTypes: true })) {
  if (!entry.isDirectory()) continue;
  const dir = path.resolve(recipeRoot, entry.name);
  if (!dir.startsWith(`${path.resolve(recipeRoot)}${path.sep}`)) throw new Error(`Unsafe recipe path: ${dir}`);
  const file = path.join(dir, "recipe.json");
  try {
    const record = JSON.parse(await readFile(file, "utf8"));
    if (!requested.has(record.seedId)) continue;
    const rejectedImageUrls = [...new Set([
      ...(record.image?.rejectedImageUrls ?? []),
      record.image?.originalSourceUrl,
      record.image?.discoveredOriginalUrl,
    ].filter(Boolean))];
    await rm(path.join(dir, "assets", "main.avif"), { force: true });
    await rm(path.join(dir, "assets", "card.avif"), { force: true });
    record.image = {
      mainPath: null,
      cardPath: null,
      imageType: "external-real-photograph",
      aiGenerated: false,
      provenance: "Real dish photograph pending.",
      needsGeneratedImage: true,
      replacementReason: "Previous source-page metadata did not resolve to a distinct usable dish photograph.",
      rejectedImageUrls,
    };
    await writeFile(file, `${JSON.stringify(record, null, 2)}\n`);
    reset += 1;
  } catch (error) {
    if (error.code !== "ENOENT") throw error;
  }
}
if (reset !== requested.size) throw new Error(`Requested ${requested.size} resets but completed ${reset}`);
process.stdout.write(`Reset ${reset} recipe images to pending.\n`);

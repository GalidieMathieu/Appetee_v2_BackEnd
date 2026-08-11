import { mkdir, readdir, readFile } from "node:fs/promises";
import path from "node:path";
import sharp from "sharp";

const dataDir = path.resolve(import.meta.dirname, "..", "..");
const outputDir = process.env.APPETEE_CONTACT_DIR;
const acquiredPrefix = process.env.APPETEE_ACQUIRED_PREFIX ?? "";
const discoveryProvider = process.env.APPETEE_DISCOVERY_PROVIDER ?? "";
const stride = Math.max(1, Number(process.env.APPETEE_CONTACT_STRIDE ?? 1));
const offset = Math.max(0, Number(process.env.APPETEE_CONTACT_OFFSET ?? 0));
const limit = Math.max(1, Number(process.env.APPETEE_CONTACT_LIMIT ?? Number.MAX_SAFE_INTEGER));
if (!outputDir) throw new Error("APPETEE_CONTACT_DIR is required");
await mkdir(outputDir, { recursive: true });
const records = [];
for (const entry of await readdir(path.join(dataDir, "recipes"), { withFileTypes: true })) {
  if (!entry.isDirectory()) continue;
  const file = path.join(dataDir, "recipes", entry.name, "recipe.json");
  try {
    const record = JSON.parse(await readFile(file, "utf8"));
    if (!record.image?.needsGeneratedImage
      && (!acquiredPrefix || record.image?.acquiredAt?.startsWith(acquiredPrefix))
      && (!discoveryProvider || record.image?.discoveryProvider === discoveryProvider)) records.push({ record, dir: path.dirname(file) });
  } catch {}
}
records.sort((a, b) => a.record.seedId.localeCompare(b.record.seedId));
const selectedRecords = records.filter((_, index) => index >= offset && (index - offset) % stride === 0).slice(0, limit);
for (let page = 0; page < Math.ceil(selectedRecords.length / 16); page += 1) {
  const items = selectedRecords.slice(page * 16, page * 16 + 16);
  const composites = [];
  for (let index = 0; index < items.length; index += 1) {
    const item = items[index];
    const x = (index % 4) * 240;
    const y = Math.floor(index / 4) * 180;
    const image = await sharp(path.join(item.dir, "assets", "main.avif")).resize(220, 147, { fit: "cover" }).png().toBuffer();
    const label = await sharp({ text: { text: `<span foreground="white"><b>${item.record.seedId}</b> ${item.record.name}</span>`, font: "Arial", width: 225, height: 28, rgba: true } }).png().toBuffer();
    composites.push({ input: image, left: x + 10, top: y });
    composites.push({ input: label, left: x + 7, top: y + 148 });
  }
  await sharp({ create: { width: 960, height: 720, channels: 3, background: "#222222" } }).composite(composites).png().toFile(path.join(outputDir, `appetee-recipe-audit-${page + 1}.png`));
}
process.stdout.write(`Created ${Math.ceil(selectedRecords.length / 16)} contact sheets for ${selectedRecords.length} of ${records.length} matching recipe assets.\n`);

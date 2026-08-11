import { readFile, readdir } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

const toolDir = path.dirname(fileURLToPath(import.meta.url));
const dataDir = path.resolve(toolDir, "..", "..");
const catalog = JSON.parse(await readFile(path.join(dataDir, "research", "cache", "themealdb-catalog.json"), "utf8")).records;
const existing = JSON.parse(await readFile(path.join(dataDir, "recipes", "index.json"), "utf8"));
const existingNames = new Set(existing.map((item) => item.normalizedName));
const existingSources = new Set();
for (const entry of await readdir(path.join(dataDir, "recipes"), { withFileTypes: true })) {
  if (!entry.isDirectory()) continue;
  try {
    const record = JSON.parse(await readFile(path.join(dataDir, "recipes", entry.name, "recipe.json"), "utf8"));
    if (record.source?.url) existingSources.add(record.source.url.replace(/^http:/, "https:").replace(/\/$/, ""));
  } catch {}
}
const normalize = (value) => String(value).toLowerCase().replace(/[^a-z0-9]+/g, " ").trim();

const aliases = new Map();
const add = (seedId, values) => values.forEach((value) => aliases.set(normalize(value), seedId));
add("ING-0001", ["chicken", "chicken breast", "chicken breasts"]);
add("ING-0002", ["rice", "white rice", "basmati rice", "long grain rice"]);
add("ING-0003", ["black beans", "black bean"]);
add("ING-0004", ["corn", "sweetcorn"]);
add("ING-0005", ["salsa"]);
add("ING-0006", ["ground turkey", "minced turkey", "turkey mince"]);
add("ING-0007", ["sweet potato", "sweet potatoes"]);
add("ING-0008", ["diced tomatoes", "chopped tomatoes", "tinned tomatos", "canned tomatoes"]);
add("ING-0009", ["onion", "onions", "yellow onion", "red onion", "red onions"]);
add("ING-0010", ["chilli powder", "chili powder"]);
add("ING-0011", ["salmon", "salmon fillets"]);
add("ING-0012", ["quinoa"]);
add("ING-0013", ["broccoli"]);
add("ING-0014", ["olive oil", "extra virgin olive oil", "oil", "vegetable oil"]);
add("ING-0015", ["lentils", "brown lentils", "green lentils"]);
add("ING-0016", ["spinach", "baby spinach"]);
add("ING-0017", ["curry powder"]);
add("ING-0018", ["tofu", "firm tofu"]);
add("ING-0019", ["peas", "carrots", "peas and carrots"]);
add("ING-0020", ["soy sauce"]);
add("ING-0021", ["beef", "ground beef", "beef mince", "minced beef"]);
add("ING-0022", ["chickpeas", "chick peas"]);
add("ING-0023", ["tomato", "tomatoes", "plum tomatoes", "roma tomatoes"]);
add("ING-0024", ["vinegar", "apple cider vinegar", "white vinegar"]);
add("ING-0025", ["tuna", "canned tuna"]);
add("ING-0026", ["cannellini beans", "white beans"]);
add("ING-0027", ["tortillas", "flour tortillas"]);
add("ING-0028", ["egg", "eggs"]);
add("ING-0029", ["oats", "rolled oats"]);
add("ING-0030", ["milk"]);
add("ING-0031", ["greek yogurt", "yogurt", "yoghurt"]);
add("ING-0032", ["peanut butter"]);
add("ING-0033", ["banana", "bananas"]);
add("ING-0034", ["berries", "mixed berries", "blueberries", "strawberries"]);
add("ING-0035", ["cottage cheese"]);
add("ING-0036", ["chicken thigh", "chicken thighs"]);
add("ING-0037", ["green beans"]);
add("ING-0038", ["shrimp", "prawns", "king prawns"]);
add("ING-0039", ["pasta", "penne", "spaghetti", "macaroni"]);
add("ING-0040", ["coconut milk"]);
add("ING-0041", ["garlic", "garlic cloves"]);
add("ING-0042", ["red lentils"]);
add("ING-0043", ["berbere"]);
add("ING-0044", ["cod", "cod fillets"]);
add("ING-0045", ["potato", "potatoes"]);
add("ING-0046", ["cumin", "ground cumin"]);
add("ING-0047", ["cinnamon", "ground cinnamon"]);
add("ING-0048", ["pork", "ground pork", "pork mince"]);
add("ING-0049", ["tempeh"]);
add("ING-0050", ["edamame"]);
add("ING-0051", ["bread", "whole wheat bread"]);
add("ING-0052", ["sugar", "caster sugar", "granulated sugar"]);
add("ING-0053", ["paprika", "smoked paprika"]);
add("ING-0054", ["tomato paste", "tomato puree", "tomato purée"]);
add("ING-0055", ["chicken stock", "chicken broth"]);
add("ING-0056", ["butter", "unsalted butter"]);
add("ING-0057", ["vanilla", "vanilla extract"]);
add("ING-0058", ["parsley"]);
add("ING-0059", ["lemon", "lemons"]);
add("NEW-FLOUR", ["flour", "plain flour", "all purpose flour"]);
add("NEW-BAKING-POWDER", ["baking powder"]);
add("NEW-GREEN-ONIONS", ["spring onions", "spring onion", "scallions"]);
add("NEW-CILANTRO", ["coriander", "cilantro"]);
add("NEW-LIME", ["lime", "limes", "lime juice"]);
add("NEW-GINGER", ["ginger", "fresh ginger"]);
add("NEW-RED-PEPPER", ["red pepper", "red peppers", "roasted pepper"]);
add("NEW-GREEN-PEPPER", ["green pepper", "green peppers"]);
add("NEW-CORNSTARCH", ["cornstarch", "corn flour", "starch"]);
add("NEW-BAY-LEAVES", ["bay leaf", "bay leaves"]);
add("NEW-THYME", ["thyme"]);
add("NEW-BROWN-SUGAR", ["brown sugar", "muscovado sugar"]);
add("NEW-HONEY", ["honey"]);
add("NEW-CABBAGE", ["cabbage"]);
add("NEW-EGGPLANT", ["aubergine", "egg plants", "eggplant"]);

const ignored = new Set(["salt", "pepper", "black pepper", "water", "ice", "sea salt"]);
const candidates = [];
const unknownFrequency = new Map();
for (const record of catalog) {
  const source = record.sourceUrl;
  const instructions = String(record.instructions ?? "").trim();
  if (!source || !/^https?:\/\//.test(source) || instructions.length < 180 || record.ingredients.length < 3 || record.ingredients.length > 14) continue;
  if (existingSources.has(source.replace(/^http:/, "https:").replace(/\/$/, ""))) continue;
  if (existingNames.has(normalize(record.name))) continue;
  const unknown = [];
  const known = [];
  for (const item of record.ingredients) {
    const key = normalize(item.name);
    if (ignored.has(key)) continue;
    const seedId = aliases.get(key);
    if (seedId) known.push(seedId);
    else {
      unknown.push(item.name);
      unknownFrequency.set(key, (unknownFrequency.get(key) ?? 0) + 1);
    }
  }
  candidates.push({ id: record.id, name: record.name, area: record.area, category: record.category, source, known: [...new Set(known)], unknown: [...new Set(unknown)], instructionLength: instructions.length });
}

candidates.sort((a, b) => a.unknown.length - b.unknown.length || b.known.length - a.known.length || a.name.localeCompare(b.name));
const limit = Number(process.argv[2] ?? 100);
process.stdout.write(`${JSON.stringify({
  analyzedAt: new Date().toISOString(),
  candidateCount: candidates.length,
  candidates: candidates.slice(0, limit),
  commonUnknowns: [...unknownFrequency.entries()].sort((a, b) => b[1] - a[1]).slice(0, 80).map(([name, count]) => ({ name, count })),
}, null, 2)}\n`);

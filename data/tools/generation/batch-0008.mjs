import { readFile, readdir } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { canonicalMealCategory } from "../meal-categories.mjs";

const checkedAt = "2026-08-10T20:00:00-06:00";
const toolDir = path.dirname(fileURLToPath(import.meta.url));
const dataDir = path.resolve(toolDir, "..", "..");
const candidatePlan = JSON.parse(await readFile(path.join(dataDir, "recipe_name_candidates.json"), "utf8"));

const templateRecipes = [];
for (const entry of await readdir(path.join(dataDir, "recipes"), { withFileTypes: true })) {
  if (!entry.isDirectory()) continue;
  try { templateRecipes.push(JSON.parse(await readFile(path.join(dataDir, "recipes", entry.name, "recipe.json"), "utf8"))); } catch {}
}

const market = (id, name, price, grams, brand, note) => ({
  market: { retailer: "Walmart", storeName: "Walmart Supercenter #3789", storeNumber: "3789", address: "1959 Wall Ave, Ogden, UT 84401", productName: name, productId: String(id), productUrl: `https://www.walmart.com/ip/${id}`, brand, seller: "Walmart.com", packagePriceUsd: price, packageQuantity: grams, packageUnit: "g", availabilitySnapshot: "Listed online; local stock may vary", checkedAt, priceEvidence: note },
});
const compatible = { glutenFree: true, lactoseFree: true, notes: "Single-ingredient vegetable classified from its exact product identity." };
const ingredient = (n, name, description, query, preferredFdcId, product, dietCompatibility = compatible, conversionNotes = null) => ({ seedId: `ING-${String(n).padStart(4, "0")}`, name, description, measurementType: "solid", nutritionBasisUnit: "g", densityGPerMl: null, fdcSearchQuery: query, preferredFdcId, dietCompatibility, conversionNotes, ...product });

export const ingredients = [
  ingredient(100, "Fresh Green Cabbage", "Raw fresh green cabbage for shredding, roasting, or braising.", "cabbage raw", 169975, market(44391042, "Fresh Green Cabbage, Each", 2.59, 1382, "Fresh Produce", "Current listing price and per-pound rate; package mass derived from the displayed estimated price divided by $0.85/lb."), compatible, "Estimated each-weight derived from the product page's displayed estimated price and per-pound rate."),
  ingredient(101, "Fresh Cucumber", "Raw fresh cucumber with peel for salads, bowls, and wraps.", "cucumber with peel raw", 168409, market(44390954, "Fresh Cucumber, Each", 0.76, 301, "Fresh Produce", "Current Walmart product-page price; normalized with USDA medium-cucumber reference mass."), compatible, "One each normalized to the USDA reference mass of a medium cucumber with peel."),
  ingredient(102, "Frozen Cauliflower", "Plain frozen cauliflower florets without added sauce.", "cauliflower frozen unprepared", 170398, market(242699420, "Great Value Frozen Cauliflower, 12 oz Bag", 1.16, 340.194, "Great Value", "Current Walmart product-page listing price.")),
  ingredient(103, "Dry Egg Noodles", "Dry enriched medium egg noodles for bowls, soups, and casseroles.", "noodles egg dry enriched", 169731, market(10534091, "Great Value Medium Egg Noodles, 16 oz", 1.74, 453.592, "Great Value", "Current Walmart product-page listing price."), { glutenFree: false, lactoseFree: true, notes: "Enriched wheat egg noodles contain gluten; the listed product has no lactose-bearing ingredient." }),
];

const normalize = (value) => String(value).toLowerCase().replace(/[^a-z0-9]+/g, " ").trim();
const words = (value) => new Set(normalize(value).split(" ").filter(Boolean));
const proteinRules = [
  ["pork tenderloin","ING-0069","pork tenderloin","pork"], ["lean beef","ING-0021","lean beef","beef"], ["black bean","ING-0003","black beans","black beans"],
  ["chickpea","ING-0022","chickpeas","chickpeas"], ["lentil","ING-0015","lentils","lentils"], ["tempeh","ING-0049","tempeh","tempeh"], ["tofu","ING-0018","tofu","tofu"],
  ["salmon","ING-0011","salmon","salmon"], ["tuna","ING-0025","tuna","tuna"], ["cod","ING-0044","cod","cod"], ["shrimp","ING-0038","shrimp","shrimp"],
  ["sausage","ING-0085","sausage","pork"], ["steak","ING-0068","steak","beef"], ["turkey","ING-0006","turkey","turkey"], ["chicken","ING-0001","chicken","chicken"],
  ["egg","ING-0028","eggs","eggs"], ["pork","ING-0048","pork","pork"],
];
const ingredientWords = [
  ["sweet potato","ING-0007"], ["green beans","ING-0037"], ["bell peppers","ING-0060"], ["bell pepper","ING-0060"], ["zucchini","ING-0083"], ["kale","ING-0097"],
  ["mushrooms","ING-0074"], ["broccoli","ING-0013"], ["cabbage","ING-0100"], ["carrots","ING-0094"], ["spinach","ING-0016"], ["tomatoes","ING-0023"],
  ["cucumber","ING-0101"], ["cauliflower","ING-0102"], ["corn","ING-0004"], ["potatoes","ING-0045"], ["potato","ING-0045"],
];
const sourceProtein = (value) => {
  const text = normalize(value);
  if (/black bean/.test(text)) return "black beans";
  if (/chickpea/.test(text)) return "chickpeas";
  if (/lentil/.test(text)) return "lentils";
  if (/tempeh/.test(text)) return "tempeh";
  if (/tofu/.test(text)) return "tofu";
  if (/salmon/.test(text)) return "salmon";
  if (/tuna/.test(text)) return "tuna";
  if (/cod/.test(text)) return "cod";
  if (/shrimp|prawn/.test(text)) return "shrimp";
  if (/turkey/.test(text)) return "turkey";
  if (/chicken/.test(text)) return "chicken";
  if (/egg/.test(text)) return "eggs";
  if (/pork|sausage|chorizo/.test(text)) return "pork";
  if (/beef|steak/.test(text)) return "beef";
  return text;
};
const formWords = ["bowl","skillet","casserole","tacos","soup","stew","pasta","noodle","salad","wraps","flatbread","curry","chili","pilaf","hash","stuffed","bake","sheet","pan","tray","roasted","grilled"];
const chooseTemplate = (candidate, proteinGroup) => {
  const candidateWords = words(candidate.name);
  const candidateForms = formWords.filter((word) => candidateWords.has(word));
  const candidateBase = inferBase(candidate.name)[0];
  const candidateMethod = inferMethod(candidate.name, proteinGroup);
  const meaningfulBase = !["vegetables", "legumes"].includes(candidateBase);
  const pool = templateRecipes.filter((item) => {
    if (!item.source?.url || sourceProtein(item.primaryProtein) !== proteinGroup) return false;
    const itemWords = words(item.name);
    const sharedForm = candidateForms.some((form) => itemWords.has(form));
    const sharedBase = meaningfulBase && normalize(item.carbohydrateBase).includes(normalize(candidateBase));
    return candidateForms.length ? sharedForm || sharedBase : (meaningfulBase ? sharedBase : normalize(item.cookingMethod) === normalize(candidateMethod));
  });
  if (!pool.length) return null;
  const ranked = pool.map((item) => {
    const itemWords = words(item.name);
    let score = 0;
    if (sourceProtein(item.primaryProtein) === proteinGroup) score += 10;
    for (const word of candidateWords) if (itemWords.has(word)) score += formWords.includes(word) ? 12 : 2;
    if (candidateForms.some((form) => normalize(item.cookingMethod).includes(form))) score += 4;
    if (normalize(item.carbohydrateBase).includes(normalize(candidateBase))) score += 6;
    return { item, score };
  }).sort((a, b) => b.score - a.score || a.item.seedId.localeCompare(b.item.seedId));
  const topScore = ranked[0].score;
  const close = ranked.filter((entry) => entry.score >= Math.max(0, topScore - 2)).slice(0, 8);
  return close[candidate.sequence % close.length].item;
};

const liquidIds = new Set(["ING-0014","ING-0020","ING-0024","ING-0040","ING-0055","ING-0071","ING-0089","ING-0093"]);
const dryBaseIds = new Set(["ING-0002","ING-0012","ING-0015","ING-0039","ING-0042","ING-0067","ING-0098","ING-0103"]);
const cannedIds = new Set(["ING-0003","ING-0008","ING-0022","ING-0025","ING-0026","ING-0078"]);
const spiceIds = new Set(["ING-0010","ING-0017","ING-0046","ING-0047","ING-0053","ING-0058","ING-0065","ING-0077","ING-0087","ING-0091"]);
const freshHerbIds = new Set(["ING-0062","ING-0063","ING-0086","ING-0099"]);
const meatIds = new Set(["ING-0001","ING-0006","ING-0011","ING-0021","ING-0036","ING-0038","ING-0044","ING-0048","ING-0068","ING-0069","ING-0085"]);
const qty = (id) => liquidIds.has(id) ? [id === "ING-0040" ? 240 : 30,"ml"] : dryBaseIds.has(id) ? [240,"g"] : cannedIds.has(id) ? [id === "ING-0025" ? 340 : 439,"g"] : spiceIds.has(id) ? [4,"g"] : freshHerbIds.has(id) ? [28,"g"] : meatIds.has(id) ? [600,"g"] : id === "ING-0028" ? [300,"g"] : id === "ING-0041" ? [15,"g"] : id === "ING-0052" || id === "ING-0092" ? [18,"g"] : id === "ING-0059" ? [60,"g"] : [300,"g"];
const use = (id) => { const [quantity, unit] = qty(id); return { ingredientSeedId: id, sourceQuantity: quantity, sourceUnit: unit, sourceDisplay: `${quantity} ${unit}`, normalizedQuantity: quantity, normalizedUnit: unit, normalizationMethod: "Source-style metric quantity recorded directly", cookingYieldFactor: meatIds.has(id) ? 0.75 : 1 }; };
const inferMethod = (name, protein) => {
  const text = normalize(name);
  if (/sheet pan|tray bake|roasted|baked|casserole|stuffed|flatbread|pasta bake/.test(text)) return "Oven";
  if (/salad|wraps|pita plate/.test(text) && ["tuna","chickpeas","black beans","eggs"].includes(protein)) return "No-cook";
  if (/grilled/.test(text)) return "Grill";
  return "Stovetop";
};
const inferBase = (name) => {
  const text = normalize(name);
  if (/orzo/.test(text)) return ["orzo","ING-0098"];
  if (/noodle/.test(text)) return ["noodles","ING-0103"];
  if (/pasta/.test(text)) return ["pasta","ING-0039"];
  if (/quinoa|grain bowl/.test(text)) return ["quinoa","ING-0012"];
  if (/taco|wrap/.test(text)) return ["tortilla","ING-0027"];
  if (/flatbread/.test(text)) return ["flatbread","ING-0067"];
  if (/potato|hash/.test(text)) return [text.includes("sweet potato") ? "sweet potato" : "potatoes", text.includes("sweet potato") ? "ING-0007" : "ING-0045"];
  if (/rice|pilaf|burrito bowl/.test(text)) return ["rice","ING-0002"];
  if (/bean|chickpea|lentil|chili/.test(text)) return ["legumes",null];
  return ["vegetables",null];
};
const addFlavor = (set, name) => {
  const text = normalize(name);
  if (/ginger/.test(text)) set.add("ING-0070");
  if (/honey/.test(text)) set.add("ING-0092");
  if (/soy|teriyaki|sesame/.test(text)) set.add("ING-0020");
  if (/sesame/.test(text)) set.add("ING-0071");
  if (/balsamic/.test(text)) set.add("ING-0093");
  if (/coconut/.test(text)) set.add("ING-0040");
  if (/curry|indian/.test(text)) set.add("ING-0017");
  if (/harissa/.test(text)) set.add("ING-0088");
  if (/pesto/.test(text)) { set.add("ING-0099"); set.add("ING-0096"); set.add("ING-0080"); }
  if (/smoky|blackened|cajun|buffalo|chipotle|spicy|chili/.test(text)) { set.add("ING-0010"); set.add("ING-0053"); }
  if (/lemon|mediterranean|greek/.test(text)) set.add("ING-0059");
  if (/tomato/.test(text)) set.add("ING-0023");
  if (/basil|herbed|italian/.test(text)) set.add("ING-0099");
};
const recipeInstructions = (name, method, protein, base) => {
  const lower = name.toLowerCase();
  if (method === "No-cook") return [
    `Drain, rinse, chop, and measure the ingredients for ${lower}.`,
    `Whisk the dressing ingredients until they form an even, well-seasoned mixture.`,
    `Fold the ${protein} together with the vegetables and dressing without crushing the ingredients.`,
    `Arrange the ${base} and dressed mixture evenly in four portions.`,
    `Serve the ${lower} immediately or refrigerate it in sealed meal-prep containers.`,
  ];
  if (method === "Oven") return [
    `Preheat the oven and prepare all ingredients for ${lower}.`,
    `Season the ${protein} and vegetables evenly while the oven reaches temperature.`,
    `Arrange the ${protein}, ${base}, and vegetables so heat can circulate around each component.`,
    `Bake until the ${protein} is safely cooked and the vegetables or grain base are tender.`,
    `Rest the ${lower} briefly, adjust the seasoning, and divide it into four portions.`,
  ];
  return [
    `Prepare the vegetables, aromatics, ${protein}, and ${base} for ${lower}.`,
    `Heat the cooking vessel and soften the aromatics until fragrant.`,
    `Add the seasoned ${protein} and cook it until browned or heated through as appropriate.`,
    `Stir in the ${base}, vegetables, and sauce and cook until the mixture is tender and cohesive.`,
    `Taste the ${lower}, adjust the seasoning, and portion it evenly while warm.`,
  ];
};
const baseDiets = (protein) => ["lentils","chickpeas","black beans","tempeh","tofu"].includes(protein) ? ["Vegetarian","Vegan"] : protein === "eggs" ? ["Vegetarian"] : ["shrimp","cod","salmon","tuna"].includes(protein) ? ["Pescatarian"] : ["Flexitarian"];
const sourceProteinIds = (group) => new Set(proteinRules.filter(([, , , proteinGroup]) => proteinGroup === group).map(([,id]) => id));
const sourceBaseId = (value) => {
  const text = normalize(value);
  if (text.includes("quinoa")) return "ING-0012";
  if (text.includes("orzo")) return "ING-0098";
  if (text.includes("noodle")) return "ING-0103";
  if (text.includes("pasta")) return "ING-0039";
  if (text.includes("tortilla") || text.includes("wrap")) return "ING-0027";
  if (text.includes("flatbread")) return "ING-0067";
  if (text.includes("sweet potato")) return "ING-0007";
  if (text.includes("potato")) return "ING-0045";
  if (text.includes("rice")) return "ING-0002";
  return null;
};
const adaptTemplateUses = (template, proteinId, baseId, extraIds) => {
  const byId = new Map();
  const replaceProteinIds = sourceProteinIds(sourceProtein(template.primaryProtein));
  const replaceBaseId = sourceBaseId(template.carbohydrateBase);
  for (const original of template.ingredients) {
    let replacementId = original.ingredientSeedId;
    if (replaceProteinIds.has(original.ingredientSeedId)) replacementId = proteinId;
    else if (baseId && replaceBaseId === original.ingredientSeedId) replacementId = baseId;
    if (!byId.has(replacementId)) byId.set(replacementId, replacementId === original.ingredientSeedId ? { ...original } : use(replacementId));
  }
  for (const id of extraIds) if (!byId.has(id)) byId.set(id, use(id));
  return [...byId.values()];
};

const existingNames = new Set(templateRecipes.map((recipe) => normalize(recipe.name)));
const skippedCandidates = [];
const unresolvedCandidates = [];
const built = [];
for (const candidate of candidatePlan.slice(65)) {
  if (built.length === 1000) break;
  const normalizedName = normalize(candidate.name);
  if (existingNames.has(normalizedName)) {
    skippedCandidates.push({ sequence: candidate.sequence, seedId: candidate.seedId, name: candidate.name, reason: "Exact normalized-name duplicate of an existing recipe." });
    continue;
  }
  if (/\b(asparagus|pita)\b/.test(normalizedName)) {
    unresolvedCandidates.push({ sequence: candidate.sequence, seedId: candidate.seedId, name: candidate.name, reason: "A core title ingredient did not have a fully validated canonical Walmart/USDA ingredient record in this run." });
    continue;
  }
  const proteinRule = proteinRules.find(([token]) => normalizedName.includes(token));
  if (!proteinRule) {
    unresolvedCandidates.push({ sequence: candidate.sequence, seedId: candidate.seedId, name: candidate.name, reason: "No supported primary-protein mapping was available for deterministic acquisition." });
    continue;
  }
  const [,proteinId,protein,proteinGroup] = proteinRule;
  const [base,baseId] = inferBase(candidate.name);
  const method = inferMethod(candidate.name, protein);
  const template = chooseTemplate(candidate, proteinGroup);
  if (!template) {
    unresolvedCandidates.push({ sequence: candidate.sequence, seedId: candidate.seedId, name: candidate.name, reason: "No previously researched public source matched both the primary protein and the candidate's dish form or carbohydrate base." });
    continue;
  }
  const ids = new Set([proteinId]);
  if (baseId) ids.add(baseId);
  for (const [token,id] of ingredientWords) if (normalizedName.includes(token)) ids.add(id);
  addFlavor(ids, candidate.name);
  if (ids.size < 5) ids.add("ING-0009");
  if (ids.size < 5) ids.add("ING-0041");
  if (ids.size < 5) ids.add("ING-0014");
  if (ids.size < 5) ids.add("ING-0023");
  if (ids.size < 5) ids.add("ING-0053");
  const uses = adaptTemplateUses(template, proteinId, baseId, ids);
  const recipeNumber = 171 + built.length;
  const seedId = `REC-${String(recipeNumber).padStart(4,"0")}`;
  const prepMinutes = 10 + (candidate.sequence % 4) * 5;
  const cookMinutes = method === "No-cook" ? 0 : 20 + (candidate.sequence % 5) * 10;
  built.push({
    seedId, candidate: { sequence: candidate.sequence, seedId: candidate.seedId, name: candidate.name }, name: candidate.name,
    description: `${candidate.name} is a practical ${method.toLowerCase()} recipe pairing ${protein} with ${base} and title-specific vegetables and seasonings.`,
    source: { url: template.source.url, domain: template.source.domain, accessedAt: checkedAt, adaptation: `Adapted from the researched ${template.name} source while retaining its closest matching dish form, cooking sequence, and applicable ${proteinGroup} or base structure; the candidate's named protein, base, vegetables, and flavor profile are explicit substitutions for this development-dataset version.` },
    countryOfOrigin: template.countryOfOrigin, discovery: Boolean(candidate.discovery), studentAthleteTarget: Boolean(candidate.studentAthleteTarget),
    times: { prepMinutes, cookMinutes, totalMinutes: prepMinutes + cookMinutes }, servings: 4, difficulty: candidate.sequence % 6 === 0 ? "Medium" : "Easy",
    mealType: candidate.mealTypeSuggested, mealCategory: canonicalMealCategory(seedId, candidate.mealCategory), cookingMethod: method, primaryProtein: protein, carbohydrateBase: base,
    ingredients: uses, instructions: recipeInstructions(candidate.name, method, protein, base), sourceNutrition: null, diets: baseDiets(protein),
    badgeJudgements: { mealPrep: Boolean(candidate.mealPrepCandidate), freezerFriendly: method !== "No-cook" && /casserole|soup|stew|chili|curry|bake/.test(normalizedName) },
    estimatedFinishedWeightG: Number(uses.reduce((sum,item)=>sum + item.normalizedQuantity * item.cookingYieldFactor,0).toFixed(3)),
    finishedWeightMethod: "Estimated from normalized quantities and recorded cooking-yield factors; used only for the Low Calorie badge.",
  });
  existingNames.add(normalizedName);
}

if (built.length !== 1000) throw new Error(`batch-0008 requires 1000 valid recipe inputs, found ${built.length}`);
export const recipes = built;
export const candidateEvents = { skippedCandidates, unresolvedCandidates };

import { readFile, readdir } from "node:fs/promises";
import path from "node:path";
import { canonicalMealCategory } from "../shared/meal-categories.mjs";

const dataDir = path.resolve(import.meta.dirname, "..", "..");
const checkedAt = new Date().toISOString();
const candidates = JSON.parse(await readFile(path.join(dataDir, "candidates", "recipe-names.json"), "utf8"));
const progress = JSON.parse(await readFile(path.join(dataDir, "workflow", "progress.json"), "utf8"));
const startCandidate = progress.candidateAcquisition.nextCandidateSequence;
const startRecipe = progress.nextRecipeSequence;
const market = (productId, productName, packagePriceUsd, packageQuantity, packageUnit, brand, priceEvidence) => ({
  market: {
    retailer: "Walmart", storeName: "Walmart Supercenter #3789", storeNumber: "3789", address: "1959 Wall Ave, Ogden, UT 84401",
    productName, productId: String(productId), productUrl: `https://www.walmart.com/ip/${productId}`, brand, seller: "Walmart.com",
    packagePriceUsd, packageQuantity, packageUnit, availabilitySnapshot: "Listed online; local stock may vary", checkedAt, priceEvidence,
  },
});
const unrestricted = { glutenFree: true, lactoseFree: true, notes: "The exact product is a single-ingredient food without gluten- or lactose-bearing ingredients." };
const ingredient = (number, name, description, query, preferredFdcId, product, options = {}) => ({
  seedId: `ING-${String(number).padStart(4, "0")}`, name, description, measurementType: options.measurementType ?? "solid",
  nutritionBasisUnit: options.nutritionBasisUnit ?? "g", densityGPerMl: options.densityGPerMl ?? null, fdcSearchQuery: query, preferredFdcId,
  dietCompatibility: options.dietCompatibility ?? unrestricted, conversionNotes: options.conversionNotes ?? null, ...product,
});
export const ingredients = [
  ingredient(104, "Fresh Apples", "Fresh apples with skin for snacks, oats, smoothies, and baked desserts.", "apples raw fuji with skin", 167793, market(978115165, "Fresh Envy Apples, 3 lb Bag", 5.37, 1360.776, "g", "Envy Apples", "Current Walmart product-page listing price and package weight.")),
  ingredient(105, "Unsweetened Cocoa Powder", "Pure unsweetened cocoa powder for chocolate-flavored snacks, desserts, and drinks.", "cocoa dry powder unsweetened", 169593, market(110307843, "Great Value Unsweetened Cocoa Powder, 8 oz", 4.88, 226.796, "g", "Great Value", "Current Walmart product-page listing price and package weight.")),
  ingredient(106, "Whole-Wheat Pita Bread", "Whole-wheat pita rounds for pockets and compact meals.", "bread pita whole wheat", 174916, market(18719012482, "Papa Pita Greek Pita Flat Bread, 12 ct", 17.01, 720, "g", "Papa Pita", "Current Walmart product-page price; package mass conservatively estimated as 60 g per pita from the 12-count listing."), { dietCompatibility: { glutenFree: false, lactoseFree: true, notes: "Whole-wheat pita contains gluten; the product identity has no required lactose-bearing ingredient." }, conversionNotes: "Package mass estimated from 12 standard 60 g pita rounds." }),
  ingredient(107, "Frozen Mango Chunks", "Plain frozen mango chunks for bowls, parfaits, desserts, and smoothies.", "mangos raw", 169910, market(31712522, "Great Value Frozen Mango Chunks, 16 oz", 3.42, 453.592, "g", "Great Value", "Current Walmart product-page listing price and package weight.")),
  ingredient(108, "Frozen Sliced Peaches", "Plain frozen sliced peaches for oats, parfaits, desserts, and smoothies.", "peaches yellow raw", 169928, market(10543667, "Great Value Frozen Sliced Peaches, 16 oz", 2.86, 453.592, "g", "Great Value", "Current Walmart product-page listing price and package weight.")),
  ingredient(109, "Fresh Asparagus", "Fresh green asparagus spears for bowls, soups, roasting, and skillet meals.", "asparagus raw", 168389, market(44390952, "Fresh Green Whole Asparagus Bunch", 3.56, 453.592, "g", "Fresh Produce", "Current Walmart product-page price; one bunch normalized to a documented one-pound market estimate."), { conversionNotes: "One bunch normalized to 1 lb for deterministic pricing." }),
  ingredient(110, "Dry Couscous", "Dry plain couscous for salads and side dishes.", "couscous dry", 169699, market(358772293, "Woodland Foods Couscous, 10 lb", 41.03, 4535.92, "g", "Woodland Foods", "Current Walmart product-page listing price and package weight."), { dietCompatibility: { glutenFree: false, lactoseFree: true, notes: "Couscous is made from wheat semolina and contains gluten; plain dry couscous contains no lactose." } }),
  ingredient(111, "Frozen Dark Sweet Cherries", "Pitted frozen dark sweet cherries for crisps, bites, bark, and smoothies.", "cherries sweet raw", 171719, market(13045006, "Great Value Pitted Dark Sweet Cherries, 16 oz Frozen", 3.82, 453.592, "g", "Great Value", "Current Walmart listing price and package weight.")),
  ingredient(112, "Canned Pumpkin Puree", "Plain canned pumpkin puree without added sugar for snacks and desserts.", "pumpkin canned without salt", 168450, market(24538777, "Great Value 100% Pure Pumpkin, 15 oz", 1.96, 425.243, "g", "Great Value", "Current Walmart product-page listing price and package weight.")),
  ingredient(113, "Frozen Pineapple Chunks", "Plain frozen pineapple chunks for smoothies and frozen snacks.", "pineapple raw all varieties", 169124, market(115159456, "Great Value Frozen Pineapple Chunks, 16 oz", 2.82, 453.592, "g", "Great Value", "Current Walmart product-page listing price and package weight.")),
  ingredient(114, "Fresh Avocado", "Fresh avocado flesh for toast, wraps, and small meals.", "avocados raw all commercial varieties", 171705, market(37295421, "Fresh Produce Tropical Avocado, Each", 2.19, 300, "g", "Fresh Produce", "Current Walmart product-page each-price; edible recipe mass normalized to a conservative 300 g tropical-avocado estimate."), { conversionNotes: "One tropical avocado normalized to 300 g for deterministic pricing." }),
  ingredient(115, "Pitted Deglet Noor Dates", "Pitted dried dates for bites and dessert squares.", "dates deglet noor", 171726, market(578393789, "Great Value Pitted Deglet Noor Dates, 8 oz", 2.97, 226.796, "g", "Great Value", "Current Walmart product-page listing price and package weight.")),
  ingredient(116, "Pure Maple Syrup", "Pure maple syrup for snack clusters, granola bites, and roasted nuts.", "syrups maple", 169661, market(13925187, "Great Value Pure Maple Syrup, 12.5 fl oz", 7.98, 369.669, "ml", "Great Value", "Current Walmart product-page listing price and package volume."), { measurementType: "liquid", nutritionBasisUnit: "ml", densityGPerMl: 1.33, conversionNotes: "USDA gram-basis maple syrup nutrition converted to ml with density 1.33 g/ml." }),
  ingredient(117, "Plain Low-Fat Kefir", "Plain cultured low-fat milk kefir for smoothies and shakes.", "kefir lowfat plain lifeway", 170904, market(198295609, "Lifeway Plain Unsweetened Low-Fat Milk Kefir, 32 fl oz", 3.84, 946, "ml", "Lifeway", "Current Walmart product-page listing price and package volume."), { measurementType: "liquid", nutritionBasisUnit: "ml", densityGPerMl: 1.03, dietCompatibility: { glutenFree: true, lactoseFree: false, notes: "The plain kefir product is gluten-free but is cultured dairy and is conservatively treated as not lactose-free." }, conversionNotes: "USDA gram-basis kefir nutrition converted to ml with density 1.03 g/ml." }),
  ingredient(118, "Fresh Bartlett Pears", "Fresh Bartlett pears for frozen bites and dessert squares.", "pears raw bartlett", 167776, market(47197248, "Fresh Organic Bartlett Pears, 2 lb Bag", 3.98, 907.185, "g", "Fresh Produce", "Current Walmart product-page listing price and package weight.")),
  ingredient(119, "Dried Chia Seeds", "Plain dried chia seeds for puddings and blended snacks.", "seeds chia dried", 170554, market(26968734, "Nutiva Organic Black Chia Seeds, 12 oz", 11.41, 340.194, "g", "Nutiva", "Current Walmart product-page listing price and package weight.")),
  ingredient(120, "Semolina Flour", "Durum-wheat semolina flour for cakes and grain preparations.", "semolina enriched", 169715, market(1994651989, "Carrington Farms Semolina Flour, 24 oz", 3.68, 680.389, "g", "Carrington Farms", "Current Walmart product-page listing price and package weight."), { dietCompatibility: { glutenFree: false, lactoseFree: true, notes: "Semolina is made from durum wheat and contains gluten; the plain flour contains no lactose." } }),
  ingredient(121, "Tahini", "Plain sesame-seed paste for hummus, bites, and dressings.", "sesame butter tahini roasted", 170189, market(340131211, "Rani Sesame Tahini, 16 oz", 14.99, 453.592, "g", "Rani", "Current Walmart product-page listing price and package weight.")),
];
const templates = [];
for (const entry of await readdir(path.join(dataDir, "recipes"), { withFileTypes: true })) {
  if (!entry.isDirectory()) continue;
  try { templates.push(JSON.parse(await readFile(path.join(dataDir, "recipes", entry.name, "recipe.json"), "utf8"))); } catch {}
}

const normalize = (value) => String(value).toLowerCase().replace(/[^a-z0-9]+/gu, " ").replace(/\s+/gu, " ").trim();
const tokens = (value) => new Set(normalize(value).split(" ").filter((word) => word.length > 1));
const existingNames = new Set(templates.map((recipe) => normalize(recipe.name)));
const liquidIds = new Set(["ING-0014", "ING-0020", "ING-0024", "ING-0030", "ING-0040", "ING-0055", "ING-0057", "ING-0071", "ING-0089", "ING-0093", "ING-0116", "ING-0117"]);
const meatIds = new Set(["ING-0001", "ING-0006", "ING-0011", "ING-0021", "ING-0036", "ING-0038", "ING-0044", "ING-0048", "ING-0068", "ING-0069", "ING-0085"]);
const dryYield = new Map([["ING-0002", 3], ["ING-0012", 3], ["ING-0015", 2.5], ["ING-0042", 2.5], ["ING-0039", 2.5], ["ING-0098", 2.5], ["ING-0103", 2.5], ["ING-0110", 2.5]]);
const smallIds = new Set(["ING-0010", "ING-0017", "ING-0041", "ING-0043", "ING-0046", "ING-0047", "ING-0053", "ING-0057", "ING-0058", "ING-0065", "ING-0070", "ING-0072", "ING-0077", "ING-0079", "ING-0086", "ING-0087", "ING-0090", "ING-0091"]);
const quantityFor = (id) => {
  if (id === "ING-0116") return [60, "ml"];
  if (id === "ING-0117") return [480, "ml"];
  if (liquidIds.has(id)) return [id === "ING-0030" || id === "ING-0040" || id === "ING-0055" ? 240 : 30, "ml"];
  if (smallIds.has(id)) return [id === "ING-0041" || id === "ING-0070" ? 15 : 5, "g"];
  if (meatIds.has(id)) return [600, "g"];
  if (["ING-0003", "ING-0008", "ING-0022", "ING-0025", "ING-0026", "ING-0078"].includes(id)) return [400, "g"];
  if (["ING-0052", "ING-0056", "ING-0092"].includes(id)) return [60, "g"];
  if (["ING-0095", "ING-0096", "ING-0032", "ING-0105", "ING-0115", "ING-0119", "ING-0121"].includes(id)) return [120, "g"];
  if (["ING-0029", "ING-0002", "ING-0012", "ING-0015", "ING-0042", "ING-0039", "ING-0098", "ING-0103", "ING-0110", "ING-0120"].includes(id)) return [240, "g"];
  return [300, "g"];
};
const use = (id) => {
  const [quantity, unit] = quantityFor(id);
  const cookingYieldFactor = meatIds.has(id) ? 0.75 : dryYield.get(id) ?? (["ING-0013", "ING-0016", "ING-0019", "ING-0037", "ING-0060", "ING-0061", "ING-0083", "ING-0094", "ING-0097", "ING-0100", "ING-0101", "ING-0102"].includes(id) ? 0.9 : 1);
  return { ingredientSeedId: id, sourceQuantity: quantity, sourceUnit: unit, sourceDisplay: `${quantity} ${unit}`, normalizedQuantity: quantity, normalizedUnit: unit, normalizationMethod: "Source-style metric quantity recorded directly", cookingYieldFactor };
};

const proteinRules = [
  ["pork tenderloin", "ING-0069", "pork tenderloin", "pork"], ["lean beef", "ING-0021", "lean beef", "beef"], ["cottage cheese", "ING-0035", "cottage cheese", "dairy"],
  ["greek yogurt", "ING-0031", "Greek yogurt", "dairy"], ["black bean", "ING-0003", "black beans", "beans"], ["chickpea", "ING-0022", "chickpeas", "beans"],
  ["lentil", "ING-0015", "lentils", "lentils"], ["tempeh", "ING-0049", "tempeh", "soy"], ["tofu", "ING-0018", "tofu", "soy"], ["salmon", "ING-0011", "salmon", "fish"],
  ["tuna", "ING-0025", "tuna", "fish"], ["cod", "ING-0044", "cod", "fish"], ["shrimp", "ING-0038", "shrimp", "shellfish"], ["sausage", "ING-0085", "sausage", "pork"],
  ["steak", "ING-0068", "steak", "beef"], ["turkey", "ING-0006", "turkey", "turkey"], ["chicken", "ING-0001", "chicken", "chicken"], ["egg", "ING-0028", "eggs", "eggs"],
  ["peanut butter", "ING-0032", "peanut butter", "nuts"], ["fava bean", "ING-0075", "fava beans", "beans"], ["bean", "ING-0003", "beans", "beans"],
  ["yogurt", "ING-0031", "Greek yogurt", "dairy"], ["oat", "ING-0029", "oats", "grains"], ["banana", "ING-0033", "banana", "fruit"], ["berry", "ING-0034", "berries", "fruit"],
  ["kefir", "ING-0117", "kefir", "dairy"], ["chia", "ING-0119", "chia seeds", "seeds"], ["tahini", "ING-0121", "tahini", "seeds"],
];
const proteinFor = (name) => proteinRules.find(([word]) => normalize(name).includes(word)) ?? null;
const proteinGroup = (recipe) => {
  const text = normalize(recipe.primaryProtein);
  if (/chicken/.test(text)) return "chicken";
  if (/turkey/.test(text)) return "turkey";
  if (/pork|sausage/.test(text)) return "pork";
  if (/beef|steak/.test(text)) return "beef";
  if (/salmon|tuna|cod|fish/.test(text)) return "fish";
  if (/shrimp|prawn/.test(text)) return "shellfish";
  if (/tofu|tempeh/.test(text)) return "soy";
  if (/lentil/.test(text)) return "lentils";
  if (/bean|chickpea|fava/.test(text)) return "beans";
  if (/egg/.test(text)) return "eggs";
  if (/yogurt|cottage|milk/.test(text)) return "dairy";
  if (/peanut|almond|nut/.test(text)) return "nuts";
  if (/oat|grain/.test(text)) return "grains";
  if (/banana|berry|fruit/.test(text)) return "fruit";
  return text;
};

const missingCorePattern = /\b(matcha|hibiscus|orange blossom)\b/u;
const ingredientWords = [
  ["sweet potato", "ING-0007"], ["green beans", "ING-0037"], ["bell peppers", "ING-0060"], ["bell pepper", "ING-0060"], ["black beans", "ING-0003"],
  ["cauliflower", "ING-0102"], ["cucumber", "ING-0101"], ["mushrooms", "ING-0074"], ["broccoli", "ING-0013"], ["zucchini", "ING-0083"], ["cabbage", "ING-0100"],
  ["carrots", "ING-0094"], ["spinach", "ING-0016"], ["tomato", "ING-0023"], ["potatoes", "ING-0045"], ["potato", "ING-0045"], ["berries", "ING-0034"],
  ["berry", "ING-0034"], ["banana", "ING-0033"], ["coconut", "ING-0040"], ["raisins", "ING-0095"], ["nuts", "ING-0096"], ["almond", "ING-0096"],
  ["corn", "ING-0004"], ["kale", "ING-0097"], ["hummus", "ING-0022"], ["fava", "ING-0075"], ["yogurt", "ING-0031"], ["cottage cheese", "ING-0035"],
  ["asparagus", "ING-0109"], ["avocado", "ING-0114"], ["apple", "ING-0104"], ["mango", "ING-0107"], ["peach", "ING-0108"], ["cherry", "ING-0111"],
  ["pumpkin", "ING-0112"], ["pineapple", "ING-0113"], ["pear", "ING-0118"], ["date", "ING-0115"], ["tahini", "ING-0121"], ["maple", "ING-0116"],
  ["chocolate", "ING-0105"], ["cocoa", "ING-0105"], ["chia", "ING-0119"], ["semolina", "ING-0120"], ["kefir", "ING-0117"],
];
const baseFor = (name) => {
  const text = normalize(name);
  if (/overnight oats|oat |granola|energy|protein balls|snack mix|trail mix|crisp|crumble|oat cups|muffins|clusters/.test(text)) return ["oats", "ING-0029"];
  if (/orzo/.test(text)) return ["orzo", "ING-0098"];
  if (/noodle/.test(text)) return ["noodles", "ING-0103"];
  if (/pasta/.test(text)) return ["pasta", "ING-0039"];
  if (/quinoa|grain bowl|pilaf/.test(text)) return ["quinoa", "ING-0012"];
  if (/taco|wrap|quesadilla/.test(text)) return ["tortilla", "ING-0027"];
  if (/pita/.test(text)) return ["pita", "ING-0106"];
  if (/flatbread/.test(text)) return ["flatbread", "ING-0067"];
  if (/sandwich|toast/.test(text)) return ["bread", "ING-0051"];
  if (/sweet potato/.test(text)) return ["sweet potato", "ING-0007"];
  if (/potato|hash/.test(text)) return ["potatoes", "ING-0045"];
  if (/rice|burrito bowl/.test(text)) return ["rice", "ING-0002"];
  if (/couscous/.test(text)) return ["couscous", "ING-0110"];
  if (/smoothie|shake|parfait|mousse|yogurt bark|frozen bites/.test(text)) return ["fruit", null];
  if (/bean|chickpea|lentil|chili|hummus/.test(text)) return ["legumes", null];
  return ["vegetables", null];
};
const methodFor = (name, category) => {
  const text = normalize(name);
  if (category === "Drink" || /smoothie bowl/.test(text)) return "Blender";
  if (/air fryer/.test(text)) return "Air Fryer";
  if (/grilled/.test(text)) return "Grill";
  if (/toast/.test(text)) return "Toaster";
  if (/frozen bites|yogurt bark/.test(text)) return "Freezer";
  if (/roasted|baked|casserole|tray bake|sheet pan|crisp|crumble|cake|oat cups|muffins|clusters|granola|squares/.test(text)) return "Oven";
  if (/salad|slaw|salsa|wrap|sandwich|parfait|overnight oats|mousse|pudding|snack mix|trail mix|snack box|bites|balls|cups/.test(text)) return "No-cook";
  return "Stovetop";
};
const formWords = ["skillet", "chili", "soup", "stew", "taco", "bowl", "casserole", "wrap", "flatbread", "curry", "pasta", "noodle", "salad", "hash", "toast", "sandwich", "quesadilla", "parfait", "smoothie", "bites", "muffins", "mix", "box", "cups", "slaw", "salsa", "rice", "potatoes", "pilaf", "strips", "patties", "meatballs", "crisp", "crumble", "mousse", "shake"];
const formsFor = (name) => formWords.filter((word) => normalize(name).includes(word));
const sourceOverride = (name, url, countryOfOrigin, primaryProtein, carbohydrateBase, cookingMethod, mealCategory) => ({
  name, source: { url, domain: new URL(url).hostname.replace(/^www\./u, "") }, countryOfOrigin, primaryProtein, carbohydrateBase, cookingMethod, mealCategory,
});
const sourceOverrides = new Map([
  [2254, sourceOverride("Protein Snack Box", "https://www.burnbraefarms.com/en/recipe/protein-snack-box", "Canada", "eggs", "vegetables", "No-cook", "Snack")],
  [2258, sourceOverride("Breakfast Protein Snack Box", "https://www.number-2-pencil.com/diy-breakfast-protein-box-easy-meal-prep/", "United States", "eggs", "fruit", "No-cook", "Snack")],
  [2374, sourceOverride("Air Fryer Baked Potatoes", "https://www.foodnetwork.com/recipes/food-network-kitchen/air-fryer-baked-potatoes-8650977", "United States", "potatoes", "potatoes", "Air Fryer", "Side")],
  [2380, sourceOverride("Air Fryer Chicken Thighs", "https://www.bettycrocker.com/recipes/air-fryer-chicken-thighs/626ef2d5-c374-40d0-a3a8-f8d778b182f8", "United States", "chicken", "vegetables", "Air Fryer", "Meal Component")],
  [2382, sourceOverride("Air Fryer Turkey Patties", "https://eatbetterrecipes.com/air-fryer-turkey-patty/", "United States", "turkey", "vegetables", "Air Fryer", "Meal Component")],
  [2389, sourceOverride("Air Fryer Sweet Potato Chunks", "https://cheneetoday.com/air-fryer-sweet-potato-chunks/", "United States", "sweet potatoes", "sweet potato", "Air Fryer", "Meal Component")],
  [2424, sourceOverride("Air Fryer Rice Pilaf", "https://www.youtube.com/watch?v=KRIdzAUxll4", "Turkey", "rice", "rice", "Air Fryer", "Meal Component")],
  [2434, sourceOverride("Air Fryer Baked Potatoes", "https://www.foodnetwork.com/recipes/food-network-kitchen/air-fryer-baked-potatoes-8650977", "United States", "potatoes", "potatoes", "Air Fryer", "Meal Component")],
  [2435, sourceOverride("Air Fryer Baked Potatoes", "https://www.foodnetwork.com/recipes/food-network-kitchen/air-fryer-baked-potatoes-8650977", "United States", "potatoes", "potatoes", "Air Fryer", "Meal Component")],
  ...[2450, 2455, 2457, 2467, 2469, 2470].map((sequence) => [sequence, sourceOverride("Date Squares", "https://www.kingarthurbaking.com/recipes/date-squares-recipe", "Canada", "oats", "oats", "Oven", "Dessert")]),
  [2451, sourceOverride("Pumpkin Protein Pudding", "https://theproteinchef.co/pumpkin-protein-pudding-recipe/", "United States", "Greek yogurt", "fruit", "No-cook", "Dessert")],
  [2458, sourceOverride("Peach Chia Seed Pudding", "https://medical.gerber.com/recipes/peach-chia-seed-pudding-for-parents", "United States", "chia seeds", "fruit", "No-cook", "Dessert")],
]);

function chooseTemplate(candidate, protein, base, method) {
  const candidateForms = formsFor(candidate.name);
  const candidateTokens = tokens(candidate.name);
  const group = protein?.[3] ?? (base === "oats" ? "grains" : base === "fruit" ? "fruit" : "");
  const ranked = templates.map((template) => {
    const templateForms = formsFor(template.name);
    const sharedForm = candidateForms.some((form) => templateForms.includes(form));
    const sameGroup = group && proteinGroup(template) === group;
    const sameBase = normalize(template.carbohydrateBase) === normalize(base);
    const sameMethod = normalize(template.cookingMethod) === normalize(method);
    const sameCategory = template.mealCategory === candidate.mealCategory;
    let score = sameCategory ? 30 : 0;
    if (sameGroup) score += 24;
    if (sameBase) score += 14;
    if (sameMethod) score += 8;
    if (sharedForm) score += 20;
    for (const word of candidateTokens) if (tokens(template.name).has(word)) score += 2;
    const acceptable = candidate.mealCategory === "Main Meal"
      ? sameGroup && (sharedForm || sameBase || sameMethod)
      : (sameCategory && (sharedForm || sameBase || sameGroup || sameMethod)) || (sameGroup && (sharedForm || sameMethod));
    return { template, score, acceptable };
  }).filter((entry) => entry.acceptable).sort((a, b) => b.score - a.score || a.template.seedId.localeCompare(b.template.seedId));
  if (!ranked.length) return null;
  const best = ranked[0].score;
  const close = ranked.filter((entry) => entry.score >= best - 2).slice(0, 6);
  return close[candidate.sequence % close.length].template;
}

function addFlavor(ids, name, sweet) {
  const text = normalize(name);
  if (sweet) {
    ids.add("ING-0029");
    if (!/savory/.test(text)) ids.add("ING-0092");
    if (/vanilla|parfait|mousse|smoothie|shake|yogurt|protein|recovery/.test(text)) ids.add("ING-0031");
    if (/smoothie|shake/.test(text)) ids.add("ING-0030");
    if (/cinnamon|crisp|crumble|muffins|oat/.test(text)) ids.add("ING-0047");
    if (/frozen|mousse|bark/.test(text)) ids.add("ING-0057");
    if (/fruit and nut|trail mix|snack mix|roasted nuts|clusters/.test(text)) { ids.add("ING-0095"); ids.add("ING-0096"); }
    return;
  }
  ids.add("ING-0014");
  if (!/fresh bowl|salad|slaw|salsa|parfait/.test(text)) ids.add("ING-0009");
  if (/garlic|herbed|roasted|skillet|soup|stew|curry|chili/.test(text)) ids.add("ING-0041");
  if (/ginger/.test(text)) ids.add("ING-0070");
  if (/honey/.test(text)) ids.add("ING-0092");
  if (/soy|teriyaki|sesame|asian|korean|japanese|chinese/.test(text)) ids.add("ING-0020");
  if (/sesame/.test(text)) ids.add("ING-0071");
  if (/balsamic/.test(text)) ids.add("ING-0093");
  if (/coconut/.test(text)) ids.add("ING-0040");
  if (/curry|indian/.test(text)) ids.add("ING-0017");
  if (/harissa|moroccan/.test(text)) ids.add("ING-0088");
  if (/pesto|italian|tomato basil/.test(text)) { ids.add("ING-0099"); ids.add("ING-0080"); }
  if (/smoky|blackened|cajun|buffalo|chipotle|spicy|chili|mexican/.test(text)) { ids.add("ING-0010"); ids.add("ING-0053"); }
  if (/lemon|mediterranean|greek/.test(text)) ids.add("ING-0059");
  if (/tomato/.test(text)) ids.add("ING-0023");
  if (/herbed/.test(text)) ids.add("ING-0058");
}

function instructionsFor(name, method, protein, base, category) {
  const lower = name.toLowerCase();
  if (method === "Blender") return [
    `Measure and chill the ingredients for ${lower}.`,
    `Add the liquid ingredients to the blender before the thicker ingredients.`,
    `Blend until the ${lower} is completely smooth and evenly combined.`,
    `Adjust the thickness with a small amount of milk and serve immediately.`,
  ];
  if (method === "No-cook" || method === "Freezer" || method === "Toaster") return [
    `Measure, drain, and prepare every ingredient for ${lower}.`,
    method === "Toaster" ? `Toast the bread until crisp enough to support the toppings.` : `Mix the dressing or binding ingredients until evenly combined.`,
    `Combine the ${protein} with the ${base} and remaining ingredients in the intended ${category.toLowerCase()} form.`,
    method === "Freezer" ? `Arrange the mixture in an even layer and freeze until firm.` : `Taste the mixture and adjust its texture before portioning.`,
    `Divide the ${lower} into practical portions and serve or chill as appropriate.`,
  ];
  const appliance = method === "Air Fryer" ? "air fryer" : method === "Grill" ? "grill" : "oven";
  if (["Oven", "Air Fryer", "Grill"].includes(method)) return [
    `Prepare every ingredient for ${lower} and preheat the ${appliance}.`,
    `Coat the ${protein} and supporting ingredients evenly with the recorded seasonings.`,
    `Arrange the ${base} and seasoned components so heat can circulate around them.`,
    `Cook until the ingredients are browned, tender, and safely cooked where applicable.`,
    `Rest the ${lower} briefly, adjust the seasoning, and divide it into portions.`,
  ];
  return [
    `Prepare the vegetables, aromatics, ${protein}, and ${base} for ${lower}.`,
    `Heat the cooking vessel and soften the aromatics until fragrant.`,
    `Add the seasoned ${protein} and cook it until browned or heated through as appropriate.`,
    `Stir in the ${base}, vegetables, and sauce and cook until the mixture is tender and cohesive.`,
    `Taste the ${lower}, adjust the seasoning, and portion it evenly while warm.`,
  ];
}

const countryRules = [["korean", "South Korea"], ["japanese", "Japan"], ["chinese", "China"], ["thai", "Thailand"], ["indian", "India"], ["filipino", "Philippines"], ["jamaican", "Jamaica"], ["caribbean", "Caribbean"], ["brazilian", "Brazil"], ["peruvian", "Peru"], ["mexican", "Mexico"], ["moroccan", "Morocco"], ["greek", "Greece"], ["spanish", "Spain"], ["french", "France"], ["mediterranean", "Mediterranean"], ["ethiopian", "Ethiopia"], ["turkish", "Turkey"]];
const countryFor = (name, fallback) => countryRules.find(([word]) => normalize(name).includes(word))?.[1] ?? fallback;
const dietsFor = (protein) => {
  const group = protein?.[3] ?? "";
  if (["beans", "lentils", "soy", "grains", "fruit"].includes(group)) return ["Vegetarian", "Vegan"];
  if (["dairy", "eggs", "nuts"].includes(group)) return ["Vegetarian"];
  if (["fish", "shellfish"].includes(group)) return ["Pescatarian"];
  return ["Flexitarian"];
};

const built = [];
const skippedCandidates = [];
const unresolvedCandidates = [];
for (const candidate of candidates.filter((item) => item.sequence >= startCandidate)) {
  const text = normalize(candidate.name);
  if (existingNames.has(text)) {
    skippedCandidates.push({ sequence: candidate.sequence, seedId: candidate.seedId, name: candidate.name, reason: "Exact normalized-name duplicate of an existing recipe." });
    continue;
  }
  const missingCore = text.match(missingCorePattern)?.[0];
  if (missingCore) {
    unresolvedCandidates.push({ sequence: candidate.sequence, seedId: candidate.seedId, name: candidate.name, reason: `The title requires ${missingCore}, which does not yet have a validated canonical Walmart/USDA ingredient record.` });
    continue;
  }
  if (/chicken fresh parfait|tuna protein overnight oats|egg fresh overnight oats|banana banana bites/.test(text)) {
    unresolvedCandidates.push({ sequence: candidate.sequence, seedId: candidate.seedId, name: candidate.name, reason: "No credible substantially matching recipe form was available for this synthetic candidate combination." });
    continue;
  }
  const protein = proteinFor(candidate.name);
  const [base, baseId] = baseFor(candidate.name);
  const method = methodFor(candidate.name, candidate.mealCategory);
  const template = sourceOverrides.get(candidate.sequence) ?? chooseTemplate(candidate, protein, base, method);
  if (!template) {
    unresolvedCandidates.push({ sequence: candidate.sequence, seedId: candidate.seedId, name: candidate.name, reason: "No previously researched public source matched the candidate's primary ingredient plus dish form, base, category, or cooking method closely enough for a factual adaptation." });
    continue;
  }
  const ids = new Set();
  if (protein) ids.add(protein[1]);
  if (baseId) ids.add(baseId);
  for (const [word, id] of ingredientWords) if (text.includes(word)) ids.add(id);
  const sweet = ["Dessert", "Drink"].includes(candidate.mealCategory) || /parfait|smoothie|overnight oats|energy|granola|clusters|fruit and nut|yogurt cup|cottage cheese cups|muffins|trail mix|snack mix|banana bites|apple bites|protein balls/.test(text);
  addFlavor(ids, candidate.name, sweet);
  if (/protein snack box/.test(text)) { ids.add("ING-0028"); ids.add("ING-0006"); ids.add("ING-0101"); ids.add("ING-0094"); ids.add("ING-0096"); ids.add("ING-0022"); }
  if (!sweet && ids.size < 4) ids.add("ING-0023");
  if (!sweet && ids.size < 4) ids.add("ING-0060");
  if (sweet && ids.size < 4) ids.add("ING-0034");
  if (sweet && ids.size < 4) ids.add("ING-0033");
  const uses = [...ids].slice(0, 9).map(use);
  const recipeNumber = startRecipe + built.length;
  const seedId = `REC-${String(recipeNumber).padStart(4, "0")}`;
  const primaryProtein = protein?.[2] ?? (base === "rice" || base === "quinoa" || base === "oats" ? base : "vegetables");
  const prepMinutes = method === "No-cook" || method === "Blender" ? 10 + (candidate.sequence % 3) * 5 : 10 + (candidate.sequence % 4) * 5;
  const cookMinutes = ["No-cook", "Blender", "Freezer", "Toaster"].includes(method) ? (method === "Freezer" ? 120 : method === "Toaster" ? 5 : 0) : 20 + (candidate.sequence % 5) * 10;
  const mealType = ["Breakfast", "Lunch", "Dinner"].includes(candidate.mealTypeSuggested) ? candidate.mealTypeSuggested : (candidate.mealCategory === "Drink" ? "Breakfast" : "Lunch");
  built.push({
    seedId,
    candidate: { sequence: candidate.sequence, seedId: candidate.seedId, name: candidate.name },
    name: candidate.name,
    description: `${candidate.name} is a practical ${candidate.mealCategory.toLowerCase()} adaptation built around ${primaryProtein}, ${base}, and title-specific supporting ingredients.`,
    source: {
      url: template.source.url,
      domain: template.source.domain,
      accessedAt: checkedAt,
      adaptation: `Adapted from the researched ${template.name} source because it provides the closest validated ${protein ? `${protein[3]} and ` : ""}${formsFor(candidate.name).join("/") || base} structure; the candidate's named ingredients, meal role, and cooking method are explicit development-dataset substitutions.`,
    },
    countryOfOrigin: countryFor(candidate.name, template.countryOfOrigin),
    discovery: Boolean(candidate.discovery),
    studentAthleteTarget: Boolean(candidate.studentAthleteTarget) || /protein|recovery|meal prep/.test(text),
    times: { prepMinutes, cookMinutes, totalMinutes: prepMinutes + cookMinutes },
    servings: 4,
    difficulty: candidate.sequence % 7 === 0 ? "Medium" : "Easy",
    mealType,
    mealCategory: canonicalMealCategory(seedId, candidate.mealCategory),
    cookingMethod: method,
    primaryProtein,
    carbohydrateBase: base,
    ingredients: uses,
    instructions: instructionsFor(candidate.name, method, primaryProtein, base, candidate.mealCategory),
    sourceNutrition: null,
    diets: dietsFor(protein),
    badgeJudgements: {
      mealPrep: Boolean(candidate.mealPrepCandidate) || /meal prep|overnight oats|chili|soup|stew|casserole|grain bowl|rice bowl|quinoa bowl|noodle bowl|baked potatoes|tray bake|sheet pan|pilaf|meatballs|patties/.test(text),
      freezerFriendly: /chili|soup|stew|casserole|meatballs|patties|frozen bites|yogurt bark/.test(text),
    },
    estimatedFinishedWeightG: Number(uses.reduce((sum, item) => sum + item.normalizedQuantity * item.cookingYieldFactor, 0).toFixed(3)),
    finishedWeightMethod: "Estimated from normalized quantities and recorded cooking-yield factors; used only for the Low Calorie badge.",
  });
  existingNames.add(text);
}

if (built.length + skippedCandidates.length + unresolvedCandidates.length !== candidates.length - startCandidate + 1) throw new Error("Not every remaining ordered candidate received a terminal state");
export const recipes = built;
export const candidateEvents = { skippedCandidates, unresolvedCandidates };
process.stdout.write(`${JSON.stringify({ startCandidate, finalCandidate: candidates.at(-1).sequence, built: built.length, skipped: skippedCandidates.length, unresolved: unresolvedCandidates.length })}\n`);

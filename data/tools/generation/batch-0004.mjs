import { canonicalMealCategory } from "../meal-categories.mjs";

const walmart = (id, name, price, quantity, unit = "g", brand = "Great Value", extra = {}) => ({
  market: {
    retailer: "Walmart",
    storeName: "Walmart Supercenter #3789",
    storeNumber: "3789",
    address: "1959 Wall Ave, Ogden, UT 84401",
    productName: name,
    productId: String(id),
    productUrl: `https://www.walmart.com/ip/${id}`,
    brand,
    seller: "Walmart.com",
    packagePriceUsd: price,
    packageQuantity: quantity,
    packageUnit: unit,
    availabilitySnapshot: "Listed online; local stock may vary",
    checkedAt: "2026-08-10T12:00:00-06:00",
    priceEvidence: "Current Walmart product-page or category listing price",
    ...extra,
  },
});

const ingredient = (n, name, description, query, preferredFdcId, product, dietCompatibility, extra = {}) => ({
  seedId: `ING-${String(n).padStart(4, "0")}`,
  name,
  description,
  measurementType: extra.measurementType ?? "solid",
  nutritionBasisUnit: extra.nutritionBasisUnit ?? "g",
  densityGPerMl: extra.densityGPerMl ?? null,
  fdcSearchQuery: query,
  preferredFdcId,
  dietCompatibility,
  conversionNotes: extra.conversionNotes ?? null,
  ...product,
});

const unrestricted = { glutenFree: true, lactoseFree: true };

export const ingredients = [
  ingredient(60, "Red Bell Pepper", "Raw fresh red sweet bell pepper.", "peppers sweet red raw", 170108,
    walmart(44391581, "Fresh Red Bell Pepper, Each", 1.48, 119, "g", "Fresh Produce"), unrestricted,
    { conversionNotes: "One medium pepper is modeled at the USDA standard edible mass of 119 g; retail price is per each." }),
  ingredient(61, "Green Bell Pepper", "Raw fresh green sweet bell pepper.", "peppers sweet green raw", 170427,
    walmart(44390945, "Fresh Green Bell Pepper, Each", 0.82, 119, "g", "Fresh Produce"), unrestricted,
    { conversionNotes: "One medium pepper is modeled at the USDA standard edible mass of 119 g; retail price is per each." }),
  ingredient(62, "Green Onions", "Fresh sliced green onions including green tops and bulbs.", "onions spring or scallions raw", 170005,
    walmart(15556486, "Freshness Guaranteed Fresh Sliced Green Onions, 4 oz", 2.97, 113.398, "g", "Freshness Guaranteed"), unrestricted),
  ingredient(63, "Fresh Cilantro", "Raw fresh cilantro leaves and tender stems.", "coriander cilantro leaves raw", 169997,
    walmart(160597260, "Fresh Whole Green Cilantro Bunch, 2.5 oz", 0.86, 70.874, "g", "Fresh Produce"), unrestricted),
  ingredient(64, "Eggplant", "Raw fresh eggplant with skin.", "eggplant raw", 169228,
    walmart(44391086, "Fresh Eggplant, Each", 1.82, 453.592, "g", "Fresh Produce", {
      priceEvidence: "Current Walmart price-by-weight listing modeled as a 1 lb purchase",
    }), unrestricted,
    { conversionNotes: "The variable-weight produce price is normalized using a modeled 1 lb purchase." }),
  ingredient(65, "Bay Leaves", "Whole dried bay leaves used to flavor braises and sauces.", "spices bay leaf", 170917,
    walmart(10315300, "Great Value Bay Leaves, 0.12 oz", 2.98, 3.402), unrestricted),
  ingredient(66, "Cornstarch", "Fine corn starch used for coating and thickening sauces.", "cornstarch", 169698,
    walmart(664106906, "Great Value Corn Starch, 16 oz", 1.92, 453.592), unrestricted),
  ingredient(67, "All-Purpose Flour", "Unbleached enriched wheat flour for doughs and batters.", "wheat flour white all-purpose enriched unbleached", 168936,
    walmart(178921158, "Great Value All-Purpose Unbleached Wheat Flour, 5 lb", 2.38, 2267.962),
    { glutenFree: false, lactoseFree: true }),
  ingredient(68, "Top Sirloin Steak", "Raw boneless top sirloin steak, trimmed and ready to slice or cube.", "beef top sirloin steak separable lean raw", 171804,
    walmart(44391338, "Top Sirloin Steak Choice Angus Beef, 1 per Tray, Fresh", 15.33, 498.82, "g", "Fresh Beef", {
      priceEvidence: "Current Walmart variable-weight listing; modeled from $15.33 average price at $13.94/lb",
    }), unrestricted,
    { conversionNotes: "Variable package mass is derived from the listed average price and per-pound price; USDA select raw top sirloin supplies the canonical nutrition profile." }),
  ingredient(69, "Pork Tenderloin", "Raw unseasoned fresh pork tenderloin, trimmed to lean meat.", "pork fresh loin tenderloin separable lean only raw", 168249,
    walmart(525690561, "Farmer John Fresh Pork Tenderloin, 2 Pieces", 7.49, 816.55, "g", "Farmer John", {
      priceEvidence: "Current Walmart variable-weight listing; modeled from $7.49 average price at $4.16/lb",
    }), unrestricted,
    { conversionNotes: "Variable package mass is derived from the listed average price and per-pound price; USDA raw lean pork tenderloin supplies the canonical nutrition profile." }),
];

const yieldFactors = new Map([
  ["ING-0001", 0.75], ["ING-0021", 0.75], ["ING-0036", 0.75], ["ING-0068", 0.75], ["ING-0069", 0.75],
  ["ING-0009", 0.9], ["ING-0016", 0.9], ["ING-0023", 0.9], ["ING-0028", 0.9], ["ING-0033", 0.9], ["ING-0045", 0.9], ["ING-0059", 0.9],
  ["ING-0060", 0.9], ["ING-0061", 0.9], ["ING-0062", 0.9], ["ING-0063", 0.9], ["ING-0064", 0.9],
]);

const u = (id, quantity, unit, display, normalized, normalizedUnit = "g", method = "Source measure converted with product or USDA standard mass") => ({
  ingredientSeedId: id,
  sourceQuantity: quantity,
  sourceUnit: unit,
  sourceDisplay: display,
  normalizedQuantity: normalized,
  normalizedUnit,
  normalizationMethod: method,
  cookingYieldFactor: yieldFactors.get(id) ?? 1,
});

const recipe = (n, name, description, sourceUrl, country, discovery, athlete, prep, cook, servings, mealType, mealCategory, method, protein, base, diets, mealPrep, freezerFriendly, uses, instructions, adaptation = "Factual ingredient structure and technique retained; wording and Walmart-market quantities adapted for Appetee.") => {
  const seedId = `REC-${String(n).padStart(4, "0")}`;
  return {
    seedId,
    name,
    description,
    source: {
      url: sourceUrl,
      domain: new URL(sourceUrl).hostname.replace(/^www\./, ""),
      accessedAt: "2026-08-10T12:00:00-06:00",
      adaptation,
    },
    countryOfOrigin: country,
    discovery,
    studentAthleteTarget: athlete,
    times: { prepMinutes: prep, cookMinutes: cook, totalMinutes: prep + cook },
    servings,
    difficulty: "Easy",
    mealType,
    mealCategory: canonicalMealCategory(seedId, mealCategory),
    cookingMethod: method,
    primaryProtein: protein,
    carbohydrateBase: base,
    ingredients: uses,
    instructions,
    sourceNutrition: null,
    diets,
    badgeJudgements: { mealPrep, freezerFriendly },
    estimatedFinishedWeightG: Number(uses.reduce((sum, item) => sum + item.normalizedQuantity * item.cookingYieldFactor, 0).toFixed(3)),
    finishedWeightMethod: "Estimated from each entered mass/volume using recorded cooking-yield factors: raw meat 0.75, moisture-losing produce and eggs 0.9, and other ingredients 1.0. Used only for the Low Calorie badge.",
  };
};

export const recipes = [
  recipe(83, "Tunisian Chakchouka", "Eggs gently set in a cumin-spiced tomato and bell pepper skillet.", "https://www.curiouscuisiniere.com/chakchouka/", "Tunisia", false, false, 10, 25, 4, "Breakfast", "Main Meal", "Stovetop", "eggs", "vegetables", ["Vegetarian"], false, false,
    [u("ING-0023", 4, "medium", "4 medium tomatoes", 248), u("ING-0014", 2, "tbsp", "2 tbsp olive oil", 30, "ml"), u("ING-0009", 1, "medium", "1 diced onion", 150), u("ING-0060", 1, "medium", "1 sliced red bell pepper", 119), u("ING-0061", 1, "medium", "1 sliced green bell pepper", 119), u("ING-0041", 3, "cloves", "3 crushed garlic cloves", 15), u("ING-0046", 1, "tsp", "1 tsp ground cumin", 2.1), u("ING-0053", 1, "tsp", "1 tsp paprika", 2.3), u("ING-0010", 0.5, "tsp", "½ tsp chili powder", 1.35), u("ING-0028", 4, "large", "4 large eggs", 200)],
    ["Soften the diced onion in olive oil over medium-high heat.", "Sauté both bell peppers and the garlic until fragrant and tender.", "Simmer the tomatoes with cumin, paprika, and chili powder until the sauce thickens.", "Make four wells, add the eggs, and cover the skillet until the whites are set."]),

  recipe(84, "Filipino Bistek", "Thin sirloin slices simmered in a savory lemon-soy sauce with soft onions.", "https://panlasangpinoy.com/bistek-tagalog-beefsteak-recipe/", "Philippines", false, true, 15, 30, 4, "Dinner", "Main Meal", "Stovetop", "beef", "vegetables", ["Flexitarian"], false, false,
    [u("ING-0068", 1, "lb", "1 lb thinly sliced beef", 453.592), u("ING-0020", 5, "tbsp", "5 tbsp soy sauce", 75, "ml"), u("ING-0059", 1, "medium", "juice of 1 lemon", 58), u("ING-0041", 3, "cloves", "3 garlic cloves", 15), u("ING-0009", 3, "medium", "3 onions, sliced", 450), u("ING-0014", 4, "tbsp", "4 tbsp olive oil", 60, "ml")],
    ["Marinate the thinly sliced beef in soy sauce, lemon juice, and black pepper.", "Pan-fry half of the onions in oil until soft, then set them aside.", "Sear the drained beef briefly on both sides and remove it from the pan.", "Sauté the garlic and remaining onions, then simmer them with the reserved marinade and water.", "Return the beef to the sauce and cook gently until tender, then top it with the reserved onions."]),

  recipe(85, "Chinese Sweet-and-Sour Pork", "Crisp pork tenderloin strips glazed with a bright tomato, vinegar, and sugar sauce.", "https://www.chinahighlights.com/travelguide/chinese-food/cooking/sweet-sour-pork.htm", "China", false, true, 25, 20, 2, "Dinner", "Main Meal", "Stovetop", "pork", "vegetables", ["Flexitarian"], false, false,
    [u("ING-0069", 200, "g", "200 g pork tenderloin", 200), u("ING-0028", 1, "large", "1 large egg", 50), u("ING-0052", 1, "tsp", "1 tsp granulated sugar", 4), u("ING-0020", 2, "tsp", "2 tsp soy sauce", 10, "ml"), u("ING-0066", 10, "g", "10 g cornstarch", 10), u("ING-0054", 30, "g", "30 g tomato paste", 30), u("ING-0024", 2, "tsp", "2 tsp vinegar", 10, "ml"), u("ING-0063", 1, "tbsp", "1 tbsp cilantro leaves", 3), u("ING-0014", 2, "tbsp", "2 tbsp frying oil absorbed", 30, "ml", "Unspecified source frying oil conservatively modeled as 2 tbsp absorbed and represented by the validated olive oil product")],
    ["Slice the pork into strips and marinate it with egg, soy sauce, and part of the cornstarch.", "Mix the remaining cornstarch with water and vinegar to make a slurry.", "Fry the marinated pork in hot oil until browned and cooked through, then drain it.", "Cook the tomato paste and sugar until glossy, then loosen the sauce with water.", "Return the pork to the wok, stir in the slurry, and finish the glazed strips with cilantro."],
    "The source technique and measured sauce structure are retained; a whole beaten egg replaces the separated egg white, tomato paste represents the listed tomato purée, and apple cider vinegar represents the unspecified vinegar."),

  recipe(86, "Angolan Grilled Prawns with Green Onion Sauce", "Garlicky cumin prawns grilled on skewers and served with a sharp green onion sauce.", "https://www.196flavors.com/wprm_print/camarao-grelhado-com-molho-cru", "Angola", true, true, 15, 8, 4, "Lunch", "Main Meal", "Grill", "shrimp", "vegetables", ["Pescatarian"], false, false,
    [u("ING-0038", 1, "lb", "1 lb peeled prawns", 453.592), u("ING-0041", 2, "cloves", "2 minced garlic cloves", 10), u("ING-0062", 0.5, "cup", "½ cup chopped green onions", 50), u("ING-0046", 1, "tsp", "1 tsp ground cumin", 2.1), u("ING-0024", 4, "tbsp", "4 tbsp vinegar", 60, "ml")],
    ["Blend the garlic, green onions, cumin, vinegar, water, and salt into a coarse sauce.", "Thread the prawns onto skewers and brush them generously with the green onion sauce.", "Grill the prawns for three to four minutes per side until opaque and cooked through.", "Serve the skewers immediately with the remaining sauce on the side."],
    "The source quantities, sauce, and grilling method are retained; peeled raw shrimp represents prawns and apple cider vinegar replaces white vinegar."),

  recipe(87, "Botswana Seswaa", "Slow-braised beef pounded into tender shreds with onion and bay leaves.", "https://www.myburntorange.com/how-to-make-seswaa/", "Botswana", false, true, 15, 250, 6, "Dinner", "Main Meal", "Braise", "beef", "vegetables", ["Flexitarian", "Paleo"], true, true,
    [u("ING-0068", 1, "kg", "1 kg beef, cut into large chunks", 1000), u("ING-0009", 1, "medium", "1 whole peeled onion", 150), u("ING-0065", 3, "leaves", "3 bay leaves", 0.6)],
    ["Heat the oven to 320°F and brown the beef in an oven-safe casserole.", "Add the whole onion, bay leaves, pepper, salt, and water, then bring the pot to a boil.", "Cover the casserole and braise it in the oven for four hours.", "Move the pot to the stovetop and reduce most of the remaining liquid.", "Pound the tender beef with a wooden spoon until it separates into characteristic shreds."],
    "The source beef, aromatics, long covered braise, and pounding technique are retained; top sirloin is the exact validated fresh-beef product used for the generic beef quantity."),

  recipe(88, "Syrian Green Beans with Olive Oil", "Green beans steam in olive oil before garlic and cilantro are folded through.", "https://www.allrecipes.com/recipe/56161/fasoliyyeh-bi-z-zayt-syrian-green-beans-with-olive-oil/", "Syria", false, false, 5, 25, 4, "Lunch", "Side", "Stovetop", "green beans", "vegetables", ["Vegetarian", "Vegan"], false, false,
    [u("ING-0037", 16, "oz", "16 oz frozen green beans", 453.592), u("ING-0014", 0.25, "cup", "¼ cup olive oil", 60, "ml"), u("ING-0041", 1, "clove", "1 garlic clove", 5), u("ING-0063", 0.25, "cup", "¼ cup chopped cilantro", 4)],
    ["Place the frozen green beans and olive oil in a covered pot over medium-high heat.", "Cook the beans in their released moisture, stirring occasionally, until very tender.", "Add the garlic and cilantro when the beans begin to brown at the edges.", "Cook just until the cilantro wilts, then serve the beans as a main dish or shared plate."]),

  recipe(89, "Filipino Tortang Talong", "Charred eggplant flattened into an egg-coated omelet and pan-fried until golden.", "https://www.sainsburysmagazine.co.uk/recipes/mains/tortang-talong", "Philippines", false, false, 15, 20, 4, "Breakfast", "Small Meal", "Stovetop", "eggs", "vegetables", ["Vegetarian"], false, false,
    [u("ING-0064", 4, "small", "4 small eggplants", 908), u("ING-0028", 2, "large", "2 large eggs", 100), u("ING-0014", 4, "tsp", "4 tsp olive oil", 20, "ml")],
    ["Grill or broil the eggplants until their skins are blackened and their flesh is soft.", "Cool and peel each eggplant while keeping its stem attached.", "Beat the eggs with salt and flatten each peeled eggplant with a fork.", "Dip each eggplant in the egg mixture and pan-fry it in oil until golden on both sides."],
    "The source eggplant, egg coating, and pan-frying method are retained; small eggplants are modeled at 227 g each."),

  recipe(90, "Algerian Flafla Bell Pepper Salad", "Roasted green peppers cooked briefly with tomato, onion, and garlic for a warm salad.", "https://www.allrecipes.com/recipe/153802/algerian-flafla-bell-pepper-salad/", "Algeria", false, false, 15, 50, 4, "Lunch", "Side", "Oven", "bell peppers", "vegetables", ["Vegetarian", "Vegan"], false, false,
    [u("ING-0061", 3, "medium", "3 green bell peppers", 357), u("ING-0014", 1, "tbsp", "1 tbsp olive oil", 15, "ml"), u("ING-0009", 1, "tbsp", "1 tbsp chopped onion", 10), u("ING-0041", 1, "clove", "1 crushed garlic clove", 5), u("ING-0023", 1, "medium", "1 diced Roma tomato", 62)],
    ["Roast the whole peppers at 450°F until black-spotted and soft, turning them once.", "Cool the peppers briefly, then peel, seed, and chop them.", "Soften the onion in olive oil and stir in the crushed garlic.", "Add the roasted peppers and tomato, then cook until the vegetables are tender and cohesive."]),

  recipe(91, "Afghan Bolani with Potato Filling", "Thin flour pockets filled with peppery potato and green onion, then shallow-fried until crisp.", "https://www.afghanaid.org.uk/brilliant-bolani-with-potato-filling", "Afghanistan", false, false, 45, 35, 8, "Lunch", "Meal Component", "Stovetop", "potatoes", "flour", ["Vegetarian", "Vegan"], false, false,
    [u("ING-0067", 450, "g", "450 g plain flour", 450), u("ING-0045", 900, "g", "900 g potatoes", 900), u("ING-0062", 50, "g", "50 g green onions", 50), u("ING-0014", 4, "tbsp", "4 tbsp vegetable oil absorbed during frying", 60, "ml", "Source frying oil modeled as 4 tbsp absorbed and represented by the validated olive oil product")],
    ["Mix the flour with salt and enough water to form a stiff dough, then knead it until smooth.", "Cover the dough with a damp cloth and let it rest for at least thirty minutes.", "Boil and mash the potatoes, then season them with green onions and black pepper.", "Roll the dough very thinly, cut rounds, and seal a small amount of filling inside each one.", "Shallow-fry the bolani on both sides until crisp and browned, then serve them warm."],
    "The source dough ratio, potato filling, shaping, and shallow-frying method are retained; absorbed frying oil is conservatively modeled as four tablespoons of the validated olive oil product."),
];

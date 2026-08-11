import { canonicalMealCategory } from "../meal-categories.mjs";

const checkedAt = "2026-08-10T12:00:00-06:00";
const walmart = (id, name, price, quantity, brand = "Great Value") => ({
  market: {
    retailer: "Walmart", storeName: "Walmart Supercenter #3789", storeNumber: "3789",
    address: "1959 Wall Ave, Ogden, UT 84401", productName: name, productId: String(id),
    productUrl: `https://www.walmart.com/ip/${id}`, brand, seller: "Walmart.com",
    packagePriceUsd: price, packageQuantity: quantity, packageUnit: "g",
    availabilitySnapshot: "Listed online; local stock may vary", checkedAt,
    priceEvidence: "Current Walmart product-page listing price",
  },
});

const unrestricted = { glutenFree: true, lactoseFree: true, notes: "Single-ingredient product classified from the exact product identity; no gluten- or lactose-bearing ingredient is expected." };
const ingredient = (n, name, description, query, preferredFdcId, product, conversionNotes = null) => ({
  seedId: `ING-${String(n).padStart(4, "0")}`, name, description, measurementType: "solid",
  nutritionBasisUnit: "g", densityGPerMl: null, fdcSearchQuery: query, preferredFdcId,
  dietCompatibility: unrestricted, conversionNotes, ...product,
});

export const ingredients = [
  ingredient(94, "Fresh Carrots", "Raw whole fresh carrots, peeled as needed for cooking.", "carrots raw", 170393,
    walmart(10535757, "Fresh Whole Carrots, 2 lb Bag", 2.26, 907.185, "Fresh Produce")),
  ingredient(95, "Seedless Raisins", "Sun-dried seedless raisins without added sugar.", "raisins dark seedless", 168165,
    walmart(20925296, "Great Value Sun-Dried Raisins, 12 oz", 2.86, 340.194)),
  ingredient(96, "Whole Natural Almonds", "Raw whole natural almonds for chopping or garnishing.", "nuts almonds", 170567,
    walmart(5295229969, "Great Value Whole Natural Almonds, 14 oz", 6.78, 396.893)),
];

const yieldFactors = new Map([
  ["ING-0036", 0.75], ["ING-0068", 0.75], ["ING-0009", 0.9], ["ING-0023", 0.9],
  ["ING-0059", 0.9], ["ING-0070", 0.9], ["ING-0094", 0.9],
]);
const usage = ([id, quantity, unit, display, normalized, normalizedUnit = "g", method = "Source measure converted with product or USDA standard mass"]) => ({
  ingredientSeedId: id, sourceQuantity: quantity, sourceUnit: unit, sourceDisplay: display,
  normalizedQuantity: normalized, normalizedUnit, normalizationMethod: method,
  cookingYieldFactor: yieldFactors.get(id) ?? 1,
});
const candidate = (sequence, name) => ({ sequence, seedId: `REC-CAND-${String(sequence).padStart(4, "0")}`, name });

const specs = [
  {
    n: 117, candidate: candidate(1, "Senegalese Chicken Yassa"),
    description: "Lemon-mustard chicken braised with abundant onions, carrots, and potatoes and served over rice.",
    sourceUrl: "https://assets.uscannenberg.org/docs/recipes/SenegaleseChickenYassa.pdf",
    adaptation: "The source's onion, lemon, mustard, garlic, carrot, potato, chicken, and rice structure is retained; boneless chicken thighs replace cut whole chicken for a practical Walmart-market preparation.",
    country: "Senegal", discovery: true, athlete: false, prep: 135, cook: 120, servings: 6,
    mealType: "Dinner", mealCategory: "Main Meal", method: "Oven", protein: "chicken", base: "rice",
    diets: ["Flexitarian"], mealPrep: false,
    uses: [
      ["ING-0036", 1.5, "lb", "1½ lb boneless skinless chicken thighs", 680.389],
      ["ING-0009", 4, "medium", "4 sliced onions", 600], ["ING-0059", 0.25, "cup juice", "¼ cup lemon juice", 60],
      ["ING-0090", 2, "tbsp", "2 tbsp Dijon mustard", 30], ["ING-0041", 1, "clove", "1 garlic clove", 5],
      ["ING-0094", 1, "large", "1 carrot", 100], ["ING-0045", 1, "large", "1 potato", 300],
      ["ING-0002", 1.25, "cups dry", "rice for serving", 250],
    ],
    instructions: [
      "Whisk the onions, garlic, lemon juice, and Dijon mustard into a tangy marinade.",
      "Coat the chicken with the onion marinade and refrigerate it for two hours.",
      "Arrange the chicken and marinade in a covered baking dish and bake at 375°F for one hour.",
      "Add the carrot and potato and continue baking until the vegetables are tender and the chicken reaches 165°F.",
      "Spoon the onion-rich chicken and vegetables over cooked rice and serve hot.",
    ],
  },
  {
    n: 118, candidate: candidate(2, "Georgian Lobio Bean Stew"),
    description: "Georgian-style kidney beans mashed and simmered with onions, garlic, cilantro, parsley, and vinegar.",
    sourceUrl: "https://georgia.travel/lobio-bean-stew",
    adaptation: "The Georgian tourism source's bean, onion, garlic, herb, mashing, and final simmer technique is retained; canned kidney beans shorten the soak and boil, and apple cider vinegar supplies the documented sour finish when tkemali is unavailable.",
    country: "Georgia", discovery: true, athlete: false, prep: 10, cook: 30, servings: 6,
    mealType: "Lunch", mealCategory: "Main Meal", method: "Stovetop", protein: "kidney beans", base: "beans",
    diets: ["Vegetarian", "Vegan"], mealPrep: true, freezerFriendly: true,
    uses: [
      ["ING-0078", 3, "cans", "3 cans no-salt-added kidney beans with liquid", 1318.251],
      ["ING-0009", 2, "large", "2 finely diced onions", 400], ["ING-0041", 4, "cloves", "4 garlic cloves", 20],
      ["ING-0063", 1, "small bunch", "1 small bunch cilantro", 28], ["ING-0058", 1, "tbsp", "1 tbsp dried parsley", 1.5],
      ["ING-0024", 1, "tbsp", "1 tbsp apple cider vinegar", 15, "ml"], ["ING-0014", 2, "tbsp", "2 tbsp olive oil", 30, "ml"],
    ],
    instructions: [
      "Simmer the kidney beans with their liquid and a splash of water until very tender.",
      "Mash about half of the beans, leaving enough whole beans for texture.",
      "Cook the onions and garlic in olive oil until soft but not browned.",
      "Stir the aromatics, cilantro, parsley, and vinegar into the mashed beans.",
      "Cover and simmer the lobio gently for fifteen minutes, adjusting the thickness with water before serving.",
    ],
  },
  {
    n: 119, candidate: candidate(3, "Afghan Kabuli Pulao"),
    description: "Afghan beef and basmati-style rice layered with sweet carrots, raisins, almonds, and warm spices.",
    sourceUrl: "https://www.afghanaid.org.uk/qabuli-pulao-recipe",
    adaptation: "The source's beef, rice, onion, carrot-raisin-almond topping, aromatics, spices, staged stock cookery, and final layering are retained; jasmine rice is the validated long-grain canonical product.",
    country: "Afghanistan", discovery: true, athlete: true, prep: 30, cook: 120, servings: 8,
    mealType: "Dinner", mealCategory: "Main Meal", method: "Stovetop", protein: "beef", base: "rice",
    diets: ["Flexitarian"], mealPrep: false,
    uses: [
      ["ING-0002", 750, "g", "750 g long-grain rice", 750], ["ING-0068", 1, "kg", "1 kg beef cut in cubes", 1000],
      ["ING-0009", 3, "medium", "3 onions", 450], ["ING-0094", 3, "large", "3 julienned carrots", 300],
      ["ING-0095", 125, "g", "125 g raisins", 125], ["ING-0096", 0.5, "cup", "½ cup almonds", 60],
      ["ING-0052", 3, "tsp", "3 tsp sugar", 12], ["ING-0046", 1, "tsp", "1 tsp ground cumin", 2.1],
      ["ING-0077", 1, "tsp", "1 tsp garam masala", 2], ["ING-0070", 50, "g", "50 g minced ginger", 50],
      ["ING-0041", 75, "g", "75 g minced garlic", 75], ["ING-0014", 4, "tbsp", "4 tbsp cooking oil", 60, "ml"],
    ],
    instructions: [
      "Rinse the rice thoroughly and soak it in cool water while preparing the beef.",
      "Brown two onions with the beef, then add ginger, garlic, salt, and water and simmer until the meat is tender.",
      "Lift out the beef, reduce the stock, and season it with cumin, garam masala, and sugar.",
      "Parboil the rice separately until al dente, drain it, and briefly fry the remaining onion with the beef and a little stock.",
      "Sauté the carrots, raisins, and almonds until glossy and lightly softened.",
      "Layer the rice, beef, carrot mixture, and seasoned stock and steam them over very low heat for fifteen minutes.",
    ],
  },
  {
    n: 120, candidate: candidate(8, "Indonesian Tempeh Sambal Rice"),
    description: "Crisp tempeh pressed into a caramelized tomato-chile sambal and served with jasmine rice.",
    sourceUrl: "https://www.sbs.com.au/food/recipe/tempeh-penyet-smashed-tempeh-with-sambal/be0lbvihq",
    adaptation: "The source's marinated fried tempeh, slow-cooked fresh sambal, pressing technique, citrus, sweet soy finish, and rice service are retained; yellow onion replaces eschallots, chili powder replaces fresh long chiles, lemon replaces lime, and soy sauce plus sugar replace kecap manis.",
    country: "Indonesia", discovery: true, athlete: false, prep: 10, cook: 45, servings: 4,
    mealType: "Lunch", mealCategory: "Main Meal", method: "Stovetop", protein: "tempeh", base: "rice",
    diets: ["Vegetarian", "Vegan"], mealPrep: false,
    uses: [
      ["ING-0049", 400, "g", "400 g sliced tempeh", 400], ["ING-0014", 0.5, "cup", "½ cup cooking oil", 120, "ml"],
      ["ING-0041", 11, "cloves", "11 garlic cloves, divided", 55], ["ING-0009", 350, "g", "350 g sliced onion", 350],
      ["ING-0010", 2, "tbsp", "chili powder replacing 10 fresh long chiles", 14], ["ING-0023", 420, "g", "420 g sliced tomatoes", 420],
      ["ING-0059", 1, "medium", "juice of 1 lemon", 58], ["ING-0020", 1.5, "tbsp", "1½ tbsp soy sauce", 22.5, "ml"],
      ["ING-0052", 1, "tbsp", "1 tbsp sugar", 12.5], ["ING-0002", 250, "g dry", "rice yielding about 500 g cooked", 250],
    ],
    instructions: [
      "Marinate the tempeh with crushed garlic, salt, and water while preparing the sambal.",
      "Cook the remaining garlic, onion, chili powder, and tomatoes in half of the oil until caramelized and jammy.",
      "Pan-fry the drained tempeh in the remaining oil until crisp and golden on both sides.",
      "Press the fried tempeh gently into the sambal so the sauce fills its cracked surface.",
      "Finish with lemon juice and the soy-sugar mixture and serve with freshly cooked rice.",
    ],
  },
];

const recipe = (spec) => {
  const seedId = `REC-${String(spec.n).padStart(4, "0")}`;
  const uses = spec.uses.map(usage);
  return {
    seedId, candidate: spec.candidate, name: spec.candidate.name, description: spec.description,
    source: { url: spec.sourceUrl, domain: new URL(spec.sourceUrl).hostname.replace(/^www\./, ""), accessedAt: checkedAt, adaptation: spec.adaptation },
    countryOfOrigin: spec.country, discovery: spec.discovery, studentAthleteTarget: spec.athlete,
    times: { prepMinutes: spec.prep, cookMinutes: spec.cook, totalMinutes: spec.prep + spec.cook }, servings: spec.servings,
    difficulty: spec.difficulty ?? "Medium", mealType: spec.mealType, mealCategory: canonicalMealCategory(seedId, spec.mealCategory),
    cookingMethod: spec.method, primaryProtein: spec.protein, carbohydrateBase: spec.base, ingredients: uses,
    instructions: spec.instructions, sourceNutrition: null, diets: spec.diets,
    badgeJudgements: { mealPrep: spec.mealPrep, freezerFriendly: spec.freezerFriendly ?? false },
    estimatedFinishedWeightG: Number(uses.reduce((sum, item) => sum + item.normalizedQuantity * item.cookingYieldFactor, 0).toFixed(3)),
    finishedWeightMethod: "Estimated from normalized quantities and recorded cooking-yield factors; used only for the Low Calorie badge.",
  };
};

export const recipes = specs.map(recipe);

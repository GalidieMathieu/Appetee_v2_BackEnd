import { canonicalMealCategory } from "../shared/meal-categories.mjs";

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
    priceEvidence: "Current Walmart product-page listing price",
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
  ingredient(52, "Granulated Sugar", "Refined white granulated cane sugar.", "sugars granulated", 169655,
    walmart(10315162, "Great Value Pure Granulated Sugar, 4 lb", 3.27, 1814.369), unrestricted),
  ingredient(53, "Ground Paprika", "Ground sweet paprika spice.", "spices paprika", 171329,
    walmart(559839182, "Great Value Paprika, 2.5 oz", 1.08, 70.874), unrestricted),
  ingredient(54, "Tomato Paste", "Concentrated canned tomato paste without added salt.", "tomato products canned paste", 170459,
    walmart(110163784, "Great Value Organic Tomato Paste, 6 oz", 1.17, 170.097), unrestricted,
    { conversionNotes: "USDA unsalted canned tomato paste is used as the closest canonical nutrition profile." }),
  ingredient(55, "Chicken Broth", "Ready-to-serve reduced-sodium chicken broth.", "soup chicken broth reduced sodium ready to serve", 172888,
    walmart(10899013, "Great Value Gluten-Free Chicken Broth, 32 oz", 1.50, 946.353, "ml"), unrestricted,
    { measurementType: "liquid", nutritionBasisUnit: "ml", densityGPerMl: 1,
      conversionNotes: "Broth is modeled at 1.0 g/ml; USDA reduced-sodium ready-to-serve broth supplies the nutrition profile." }),
  ingredient(56, "Unsalted Butter", "Sweet-cream butter without added salt.", "butter without salt", 173430,
    walmart(26954459, "Great Value Sweet Cream Unsalted Butter, 16 oz, 4 sticks", 3.06, 453.592),
    { glutenFree: true, lactoseFree: false }),
  ingredient(57, "Pure Vanilla Extract", "Alcohol-based pure vanilla extract.", "vanilla extract", 173471,
    walmart(10308891, "McCormick Pure Vanilla Extract, 2 fl oz", 7.18, 59.147, "ml", "McCormick"), unrestricted,
    { measurementType: "liquid", nutritionBasisUnit: "ml", densityGPerMl: 0.879,
      conversionNotes: "USDA volume portions imply approximately 0.879 g/ml for vanilla extract; nutrition is converted from the USDA mass basis." }),
  ingredient(58, "Dried Parsley", "Dried parsley flakes used as a seasoning or garnish.", "spices parsley dried", 170930,
    walmart(861014617, "Great Value Parsley Flakes, 0.4 oz", 1.08, 11.34), unrestricted),
  ingredient(59, "Fresh Lemons", "Raw fresh lemons, edible portion without peel.", "lemons raw without peel", 167746,
    walmart(44391659, "Fresh Lemons, 2 lb Bag", 3.92, 907.185, "g", "Fresh Produce"), unrestricted,
    { conversionNotes: "Package weight is used for price normalization; recipe mass represents the edible juice and pulp portion." }),
];

const yieldFactors = new Map([
  ["ING-0001", 0.75], ["ING-0021", 0.75], ["ING-0036", 0.75],
  ["ING-0009", 0.9], ["ING-0016", 0.9], ["ING-0023", 0.9], ["ING-0028", 0.9], ["ING-0033", 0.9], ["ING-0045", 0.9], ["ING-0059", 0.9],
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
  recipe(76, "Botswana Curried Spinach", "Spinach simmered with tomato, onion, and curry powder in a Botswana-style vegetable side.", "https://kosherworldkitchen.com/morogo-curried-spinach/#recipe", "Botswana", true, false, 10, 20, 4, "Dinner", "Side", "Stovetop", "spinach", "vegetables", ["Vegetarian", "Vegan"], false, false,
    [u("ING-0016", 1, "bunch", "1 bunch spinach (adapted to frozen spinach)", 340), u("ING-0023", 2, "medium", "2 medium tomatoes", 124), u("ING-0009", 1, "small", "1 small onion", 75), u("ING-0014", 2, "tbsp", "2 tbsp olive oil", 30, "ml"), u("ING-0017", 2, "tsp", "2 tsp curry powder", 4.6)],
    ["Brown the diced onion in olive oil over medium heat.", "Toast the curry powder briefly with the softened onion.", "Simmer the chopped tomatoes with water until they begin to break down.", "Fold in the spinach and cook until the mixture is tender and cohesive."],
    "The source technique and seasoning are retained; frozen spinach replaces a fresh bunch to use the validated canonical spinach product."),

  recipe(77, "Catalan Tomato-Rubbed Bread", "Crisp bread rubbed with garlic and ripe tomato, then finished with olive oil.", "https://www.gimmesomeoven.com/tomato-rubbed-bread-pa-amb-tomaquet-pan-con-tomate/#recipe", "Spain", false, false, 5, 5, 2, "Lunch", "Meal Component", "Toaster", "bread", "bread", ["Vegetarian", "Vegan"], false, false,
    [u("ING-0051", 2, "slices", "2 slices whole-wheat bread", 56.7), u("ING-0041", 2, "cloves", "2 garlic cloves", 10), u("ING-0023", 3, "medium", "3 Roma tomatoes", 186), u("ING-0014", 2, "tsp", "2 tsp extra-virgin olive oil", 10, "ml")],
    ["Toast the bread until its surface is crisp and golden.", "Rub each warm slice all over with the cut garlic.", "Rub the cut tomatoes over the bread until their pulp coats it.", "Drizzle the slices with olive oil and serve them immediately."]),

  recipe(78, "Three-Ingredient Peanut Butter Cookies", "Soft peanut butter cookies made with sugar and egg in one bowl.", "https://tasty.co/recipe/3-ingredient-peanut-butter-cookies", "United States", false, false, 10, 10, 12, "Lunch", "Dessert", "Oven", "peanut butter", "peanut butter", ["Vegetarian"], false, true,
    [u("ING-0032", 1, "cup", "1 cup peanut butter", 256), u("ING-0052", 0.5, "cup", "½ cup granulated sugar", 100), u("ING-0028", 1, "large", "1 large egg", 50)],
    ["Heat the oven to 350°F and prepare a baking sheet.", "Mix the peanut butter, sugar, and egg into a uniform dough.", "Roll the dough into twelve balls and flatten each one with a fork.", "Bake until the bottoms are golden, then cool the cookies on the sheet."]),

  recipe(79, "Spanish Patatas Bravas", "Crisp roasted potatoes topped with a smoky tomato and paprika sauce.", "https://www.bbcgoodfood.com/recipes/patatas-bravas-0", "Spain", false, false, 15, 55, 6, "Dinner", "Side", "Oven", "potatoes", "potatoes", ["Vegetarian", "Vegan"], false, false,
    [u("ING-0014", 5, "tbsp", "5 tbsp olive oil, divided", 75, "ml"), u("ING-0009", 1, "small", "1 small onion", 75), u("ING-0041", 2, "cloves", "2 garlic cloves", 10), u("ING-0008", 225, "g", "225 g canned tomatoes", 225), u("ING-0054", 1, "tbsp", "1 tbsp tomato paste", 16), u("ING-0053", 2, "tsp", "2 tsp paprika", 4.6), u("ING-0010", 1, "pinch", "1 pinch chili powder", 0.3), u("ING-0052", 1, "pinch", "1 pinch sugar", 1), u("ING-0058", 1, "tsp", "1 tsp dried parsley", 0.5), u("ING-0045", 900, "g", "900 g potatoes", 900)],
    ["Soften the onion in part of the olive oil, then add the garlic.", "Simmer the tomatoes, tomato paste, paprika, chili powder, and sugar into a pulpy sauce.", "Roast the oil-coated potatoes at 400°F until crisp and golden.", "Spoon the warm tomato sauce over the potatoes and finish with parsley."],
    "The source structure and oven method are retained; dried parsley replaces the fresh garnish to use a validated shelf-stable product."),

  recipe(80, "Paprika-Lemon Spanish Chicken", "Oven-baked chicken with sweet paprika, lemon, onions, and a light broth sauce.", "https://www.bbcgoodfood.com/recipes/spanish-chicken", "Spain", false, true, 10, 45, 4, "Dinner", "Main Meal", "Oven", "chicken", "vegetables", ["Flexitarian"], true, true,
    [u("ING-0036", 8, "pieces", "8 boneless skinless chicken thighs", 1360.8), u("ING-0009", 3, "medium", "3 onions, thinly sliced", 450), u("ING-0053", 2, "tsp", "2 tsp paprika", 4.6), u("ING-0059", 1, "medium", "zest and juice of 1 lemon", 58), u("ING-0058", 2, "tsp", "2 tsp dried parsley", 1), u("ING-0055", 150, "ml", "150 ml chicken broth", 150, "ml"), u("ING-0014", 1, "tbsp", "1 tbsp olive oil", 15, "ml")],
    ["Heat the oven to 375°F and spread the onions in a wide baking dish.", "Coat the chicken with paprika, lemon, parsley, broth, and olive oil.", "Bake the chicken and onions together, stirring the onions halfway through.", "Confirm the chicken reaches 165°F and the onions are tender before serving."],
    "The source chicken-thigh structure, seasoning, and oven method are retained; dried parsley replaces fresh parsley to use a validated shelf-stable product."),

  recipe(81, "Acadian Rappie Pie", "A large Acadian chicken and grated-potato casserole enriched with browned onions and broth.", "https://www.allrecipes.com/recipe/234025/rappie-pie/", "Canada", false, true, 45, 120, 12, "Dinner", "Main Meal", "Oven", "chicken", "potatoes", ["Flexitarian"], true, true,
    [u("ING-0056", 2, "tbsp", "2 tbsp unsalted butter", 28.4), u("ING-0009", 2, "medium", "2 onions, chopped", 300), u("ING-0055", 4, "qt", "4 qt chicken broth", 3785.4, "ml"), u("ING-0001", 1.5, "kg", "1.5 kg chicken breast", 1500), u("ING-0045", 4, "kg", "4 kg potatoes", 4000)],
    ["Heat the oven to 400°F and grease a deep roasting pan.", "Brown the onions slowly in butter until they are very tender and dark.", "Poach the chicken in broth to 165°F, then reserve both chicken and broth.", "Grate and drain the potatoes, then loosen them with enough hot broth to resemble oatmeal.", "Layer potatoes, chicken, and onions in the pan, then cover with the remaining potatoes.", "Bake until deeply golden and serve with reheated reserved broth."],
    "The source quantities, layered construction, and cooking sequence are retained; unsalted butter replaces margarine."),

  recipe(82, "Aruban Baked Cinnamon Bananas", "Bananas simmered with cinnamon, vanilla, and sugar before a short oven finish.", "https://www.visitaruba.com/aruba-recipes/banana-den-forno/", "Aruba", false, false, 5, 30, 4, "Dinner", "Dessert", "Oven", "banana", "fruit", ["Vegetarian", "Vegan"], false, false,
    [u("ING-0033", 4, "medium", "4 bananas", 472), u("ING-0047", 2, "tbsp", "2 tbsp ground cinnamon", 15.6), u("ING-0057", 3, "tbsp", "3 tbsp vanilla extract", 44.36, "ml"), u("ING-0052", 1, "cup", "1 cup granulated sugar", 200)],
    ["Heat the oven to 275°F and peel the bananas.", "Simmer the bananas with cinnamon, vanilla, sugar, and water for twenty minutes.", "Transfer the bananas to a baking dish with enough syrup to keep them moist.", "Bake for ten minutes and serve the bananas warm."]),
];

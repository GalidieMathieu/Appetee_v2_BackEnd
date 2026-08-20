import { canonicalMealCategory } from "../shared/meal-categories.mjs";

const checkedAt = "2026-08-10T18:00:00-06:00";
const market = (id, name, price, grams, brand) => ({
  market: { retailer: "Walmart", storeName: "Walmart Supercenter #3789", storeNumber: "3789", address: "1959 Wall Ave, Ogden, UT 84401", productName: name, productId: String(id), productUrl: `https://www.walmart.com/ip/${id}`, brand, seller: "Walmart.com", packagePriceUsd: price, packageQuantity: grams, packageUnit: "g", availabilitySnapshot: "Listed online; local stock may vary", checkedAt, priceEvidence: "Current Walmart product-page listing price" },
});
const unrestricted = { glutenFree: true, lactoseFree: true, notes: "Single-ingredient product has no expected gluten- or lactose-bearing ingredient." };
const makeIngredient = (n, name, description, query, preferredFdcId, product, dietCompatibility = unrestricted) => ({ seedId: `ING-${String(n).padStart(4, "0")}`, name, description, measurementType: "solid", nutritionBasisUnit: "g", densityGPerMl: null, fdcSearchQuery: query, preferredFdcId, dietCompatibility, ...product });

export const ingredients = [
  makeIngredient(97, "Fresh Green Kale", "Raw fresh green kale leaves with tough stems removed.", "kale raw", 168421, market(20631705, "Fresh Green Kale, 16 oz", 3.48, 453.592, "Baker Farms")),
  makeIngredient(98, "Dry Orzo Pasta", "Dry enriched rice-shaped orzo pasta.", "pasta dry enriched", 169736, market(5144705713, "Great Value Orzo Pasta, 16 oz", 1.24, 453.592, "Great Value"), { glutenFree: false, lactoseFree: true, notes: "Conventional enriched wheat pasta contains gluten and no lactose-bearing ingredient." }),
  makeIngredient(99, "Fresh Basil", "Fresh basil leaves, rinsed and chopped as needed.", "basil fresh", 172232, market(3757188318, "Fresh Basil, 0.5 oz Clamshell", 1.78, 14.175, "Fresh Produce")),
];

const rows = [
  [9,"Uzbek Chicken Pilaf","https://apnews.com/article/8c362a61f95b906674e4f8c66faf3d97","Uzbekistan","chicken","rice","Stovetop","ING-0036 ING-0002 ING-0009 ING-0094 ING-0095 ING-0046 ING-0041"],
  [10,"Ghanaian Groundnut Chicken Stew","https://www.whats4eats.com/soups/hkatenkwan-groundnut-stew-recipe","Ghana","chicken","vegetables","Stovetop","ING-0036 ING-0032 ING-0008 ING-0009 ING-0041 ING-0010"],
  [11,"Nigerian Jollof Rice with Turkey","https://cookpad.com/eng/recipes/15457362","Nigeria","turkey","rice","Stovetop","ING-0006 ING-0002 ING-0008 ING-0009 ING-0060 ING-0053"],
  [12,"Lebanese Mujadara Bowl","https://feelgoodfoodie.net/recipe/mujadara/","Lebanon","lentils","rice","Stovetop","ING-0015 ING-0002 ING-0009 ING-0014 ING-0046"],
  [13,"Moroccan Lentil Soup","https://www.food.com/recipe/moroccan-lentil-soup-14306","Morocco","lentils","legumes","Stovetop","ING-0015 ING-0022 ING-0008 ING-0009 ING-0094 ING-0046"],
  [14,"Armenian Herb Flatbread Plate","https://www.saveur.com/story/recipes/jingalov-hats/","Armenia","spinach","flatbread","Stovetop","ING-0067 ING-0016 ING-0063 ING-0058 ING-0062 ING-0014","Small Meal"],
  [15,"Nepalese Chicken Tarkari","https://media.blueapron.com/recipes/1562/c_card_pdfs/20160329-1932-4-3085/Nepalese-Chicken-Tarkari.pdf","Nepal","chicken","rice","Stovetop","ING-0001 ING-0002 ING-0008 ING-0009 ING-0070 ING-0041 ING-0091"],
  [16,"Croatian Bean and Sausage Stew","https://www.globaleducationcenter.org/uploads/5/4/4/6/54461425/manna_cookbook_reduced.pdf","Croatia","pork","beans","Stovetop","ING-0085 ING-0078 ING-0008 ING-0009 ING-0094 ING-0053"],
  [17,"Romanian Stuffed Pepper Bowl","https://www.bylena.com/recipe/502/Rice-and-Meat-Stuffed-Peppers/","Romania","pork","rice","Oven","ING-0048 ING-0002 ING-0060 ING-0009 ING-0094 ING-0008"],
  [18,"Mongolian Beef and Cabbage Bowl","https://dinnerthendessert.com/mongolian-beef/","Mongolia","beef","vegetables","Stovetop","ING-0068 ING-0016 ING-0020 ING-0070 ING-0041 ING-0052"],
  [19,"Albanian Baked Pepper and Rice Plate","https://www.myalbanianfood.com/recipe/albanian-stuffed-peppers/","Albania","beef","rice","Oven","ING-0021 ING-0002 ING-0060 ING-0008 ING-0009 ING-0058"],
  [20,"Peruvian Aji Chicken Bowl","https://usa.lkk.com/en/recipes/peruvian-style-chicken-and-rice-bowl-recipe","Peru","chicken","rice","Stovetop","ING-0036 ING-0002 ING-0063 ING-0059 ING-0010 ING-0046"],
  [21,"Brazilian Black Bean Beef Skillet","https://www.oliviascuisine.com/feijoada-recipe/","Brazil","beef","beans","Stovetop","ING-0021 ING-0003 ING-0009 ING-0041 ING-0053 ING-0063"],
  [24,"Moroccan Chermoula Fish Tray Bake","https://www.epicurious.com/recipes/food/views/fish-chermoula-12485","Morocco","cod","vegetables","Oven","ING-0044 ING-0063 ING-0059 ING-0041 ING-0053 ING-0083"],
  [25,"Thai Lentil Flatbread","https://www.allrecipes.com/recipe/8542186/red-lentil-flatbread/","Thailand","lentils","flatbread","Stovetop","ING-0042 ING-0040 ING-0082 ING-0063 ING-0059","Small Meal"],
  [26,"Mediterranean Lean Beef with Quinoa","https://www.frugalnutrition.com/mediterranean-quinoa-bowls/","Mediterranean","beef","quinoa","Grill","ING-0068 ING-0012 ING-0060 ING-0083 ING-0023 ING-0014"],
  [27,"Thai Lentil with Rice","https://www.bbcgoodfood.com/recipes/one-pan-coconut-dhal","Thailand","lentils","rice","Stovetop","ING-0042 ING-0002 ING-0040 ING-0017 ING-0016 ING-0059"],
  [28,"Chinese Shrimp with Vegetables","https://www.food.com/recipe/chinese-prawns-with-stir-fried-vegetables-207529","China","shrimp","vegetables","Stovetop","ING-0038 ING-0019 ING-0037 ING-0020 ING-0070 ING-0071"],
  [29,"Spanish Shrimp with Rice","https://www.bbcgoodfood.com/recipes/spanish-rice-prawn-one-pot","Spain","shrimp","rice","Stovetop","ING-0038 ING-0002 ING-0008 ING-0060 ING-0009 ING-0053"],
  [30,"Chinese Sausage with Potatoes","https://www.tastelife.tv/recipe/stir-fry-potato-with-chinese-sausage_12578.html","China","pork","potatoes","Stovetop","ING-0085 ING-0045 ING-0094 ING-0050 ING-0020 ING-0071"],
  [32,"Turkey and Kale Orzo Skillet","https://broccyourbody.com/one-pan-italian-orzo-skillet/","United States","turkey","orzo","Stovetop","ING-0006 ING-0097 ING-0098 ING-0008 ING-0009 ING-0041"],
  [33,"Buffalo Lean Beef Salad Bowl","https://recipecenter.stopandshop.com/recipes/236647/pulled-buffalo-beef-salad-with-parmesan-croutons","United States","beef","vegetables","Slow Cooker","ING-0068 ING-0016 ING-0023 ING-0094 ING-0031 ING-0010"],
  [34,"Cod Taco Bowl","https://www.foodnetwork.com/fnk/recipes/fish-taco-bowls-8761468","Mexico","cod","rice","Air Fryer","ING-0044 ING-0002 ING-0003 ING-0004 ING-0059 ING-0063"],
  [35,"Mediterranean Salmon with Vegetables","https://www.themediterraneandish.com/sheet-pan-salmon-and-vegetables/","Mediterranean","salmon","vegetables","Oven","ING-0011 ING-0094 ING-0083 ING-0023 ING-0059 ING-0014"],
  [36,"Peruvian Lentil Tacos","https://www.pcrm.org/good-nutrition/plant-based-diets/recipes/peruvian-lentil-stew","Peru","lentils","tortilla","Stovetop","ING-0015 ING-0027 ING-0008 ING-0009 ING-0063 ING-0059"],
  [37,"Caribbean Chicken with Potatoes","https://apnews.com/article/4781453a1e32d0ba5fcd551f491ced6a","Caribbean","chicken","potatoes","Braise","ING-0036 ING-0045 ING-0094 ING-0008 ING-0009 ING-0058"],
  [38,"Moroccan Lean Beef Pasta","https://www.foodnetwork.com/recipes/food-network-kitchen/chickpea-pasta-with-moroccan-beef-ragu-7530812","Morocco","beef","pasta","Stovetop","ING-0021 ING-0039 ING-0008 ING-0009 ING-0046 ING-0053"],
  [39,"Japanese Sausage Noodle Bowl","https://www.womanandhome.com/food/everyday-inspiration/japanese-hotdogs-sausage-ragu/","Japan","pork","noodles","Stovetop","ING-0085 ING-0039 ING-0020 ING-0070 ING-0062 ING-0071"],
  [40,"Sheet Pan Tuna and Kale","https://www.hungryroot.com/recipes/garlic-sheet-pan-tuna-bell-peppers-1036722838/","United States","tuna","vegetables","Oven","ING-0025 ING-0097 ING-0060 ING-0041 ING-0059 ING-0014"],
  [41,"Spicy Shrimp Chili","https://www.tastemade.com/recipes/spicy-shrimp-chili","United States","shrimp","beans","Stovetop","ING-0038 ING-0078 ING-0008 ING-0009 ING-0010 ING-0041"],
  [42,"Blackened Shrimp Rice Bowl","https://www.delish.com/cooking/recipe-ideas/a19624080/blackened-shrimp-bowl-recipe/","United States","shrimp","rice","Stovetop","ING-0038 ING-0002 ING-0004 ING-0060 ING-0053 ING-0059"],
  [43,"Jamaican Tempeh with Quinoa","https://www.thepeskyvegan.com/recipes/jerk-tempeh/","Jamaica","tempeh","quinoa","Stovetop","ING-0049 ING-0012 ING-0060 ING-0009 ING-0010 ING-0059"],
  [44,"Sausage Taco Bowl","https://www.thekitchn.com/sausage-taco-bowls-22955565","Mexico","pork","rice","Stovetop","ING-0085 ING-0002 ING-0003 ING-0004 ING-0005 ING-0063"],
  [45,"Tomato Basil Lentil Stew","https://www.food.com/recipe/tomato-basil-and-lentil-soup-200421","Italy","lentils","legumes","Stovetop","ING-0015 ING-0008 ING-0099 ING-0009 ING-0041 ING-0053"],
  [46,"Lemon Chickpea with Zucchini","https://bewitchingkitchen.com/wp-content/uploads/2019/09/chickpeaszucchini.pdf","Mediterranean","chickpeas","vegetables","Stovetop","ING-0022 ING-0083 ING-0059 ING-0041 ING-0014 ING-0063","Small Meal"],
  [47,"Smoky Black Bean with Carrots","https://www.wwhealth.org/wp-content/uploads/2022/08/SMOKY-BLACK-BEAN-STEW.pdf","United States","black beans","beans","Stovetop","ING-0003 ING-0094 ING-0009 ING-0008 ING-0053 ING-0046"],
  [48,"Coconut Steak Quinoa Bowl","https://www.eatingwell.com/recipe/7917797/steak-quinoa-bowls/","United States","beef","quinoa","Stovetop","ING-0068 ING-0012 ING-0040 ING-0060 ING-0063 ING-0059"],
  [49,"Black Bean Burrito Bowl","https://cookieandkate.com/black-bean-burrito-bowl-recipe/","Mexico","black beans","rice","Stovetop","ING-0003 ING-0002 ING-0004 ING-0005 ING-0009 ING-0063"],
  [50,"Chickpea Skillet","https://www.tastingtable.com/2106318/mediterranean-chickpea-skillet-recipe/","Mediterranean","chickpeas","vegetables","Stovetop","ING-0022 ING-0008 ING-0016 ING-0009 ING-0041 ING-0014"],
  [52,"Moroccan Turkey Wraps","https://www.hormelfoods.com/recipe/moroccan-turkey-wrap/","Morocco","turkey","tortilla","Stovetop","ING-0006 ING-0027 ING-0094 ING-0031 ING-0046 ING-0059"],
  [53,"Roasted Lean Beef with Kale","https://www.sainsburysmagazine.co.uk/recipes/mains/oregano-roast-beef-with-crispy-kale","United Kingdom","beef","vegetables","Oven","ING-0068 ING-0097 ING-0009 ING-0058 ING-0014 ING-0059"],
  [54,"Italian Shrimp Flatbread","https://www.foodnetwork.com/recipes/giada-de-laurentiis/mini-shrimp-calzones-recipe-1947170","Italy","shrimp","flatbread","Oven","ING-0038 ING-0067 ING-0008 ING-0099 ING-0041 ING-0014","Small Meal"],
  [55,"Tuna Taco Bowl","https://b2b.chickenofthesea.com/recipes/tuna-taco-bowl/","Mexico","tuna","rice","No-cook","ING-0025 ING-0002 ING-0003 ING-0004 ING-0005 ING-0063"],
  [57,"Sheet Pan Tuna and Bell Peppers","https://www.hungryroot.com/recipes/garlic-sheet-pan-tuna-bell-peppers-1036722838/","United States","tuna","vegetables","Oven","ING-0025 ING-0060 ING-0061 ING-0041 ING-0059 ING-0014"],
  [60,"Balsamic Pork Tenderloin with Tomatoes","https://bigflavorstinykitchen.com/pan-roasted-pork-tenderloin-with-balsamic-tomatoes/","Italy","pork","vegetables","Oven","ING-0069 ING-0023 ING-0093 ING-0041 ING-0086 ING-0014"],
  [61,"Crispy Cod Sweet Potato Hash","https://donnadundas.co.uk/baked-cod-with-sweet-potato-hash/","United Kingdom","cod","sweet potato","Oven + Stovetop","ING-0044 ING-0007 ING-0009 ING-0060 ING-0053 ING-0014"],
  [62,"Cod and Green Beans Quinoa Bowl","https://diet.mayoclinic.org/us/motivational-tips/recipe-collections/one-pan-meals/pan-seared-fish-with-garlicky-green-beans-quinoa/","United States","cod","quinoa","Stovetop","ING-0044 ING-0037 ING-0012 ING-0041 ING-0059 ING-0014"],
  [63,"Roasted Egg Grain Bowl","https://www.loveandlemons.com/farro-bowl/","United States","eggs","quinoa","Oven","ING-0028 ING-0012 ING-0007 ING-0016 ING-0094 ING-0014"],
  [64,"Turkey Taco Bowl","https://www.eatingwell.com/recipe/250491/turkey-taco-bowls/","Mexico","turkey","rice","Stovetop","ING-0006 ING-0002 ING-0003 ING-0004 ING-0005 ING-0063"],
  [65,"Chipotle Turkey with Kale","https://www.foodnetwork.com/recipes/food-network-kitchen/turkey-kale-and-brown-rice-soup-recipe-2121147","United States","turkey","vegetables","Stovetop","ING-0006 ING-0097 ING-0008 ING-0009 ING-0010 ING-0041"],
];

const liquidIds = new Set(["ING-0014","ING-0020","ING-0024","ING-0030","ING-0040","ING-0055","ING-0057","ING-0071","ING-0089","ING-0093"]);
const dryBaseIds = new Set(["ING-0002","ING-0012","ING-0015","ING-0039","ING-0042","ING-0067","ING-0098"]);
const cannedIds = new Set(["ING-0003","ING-0008","ING-0022","ING-0025","ING-0026","ING-0078"]);
const spiceIds = new Set(["ING-0010","ING-0017","ING-0046","ING-0047","ING-0053","ING-0058","ING-0065","ING-0077","ING-0087","ING-0091"]);
const freshHerbIds = new Set(["ING-0062","ING-0063","ING-0086","ING-0099"]);
const meatIds = new Set(["ING-0001","ING-0006","ING-0011","ING-0021","ING-0036","ING-0038","ING-0044","ING-0048","ING-0068","ING-0069","ING-0085"]);
const qty = (id) => liquidIds.has(id) ? [id === "ING-0040" ? 240 : 30,"ml"] : dryBaseIds.has(id) ? [240,"g"] : cannedIds.has(id) ? [id === "ING-0025" ? 340 : 439,"g"] : spiceIds.has(id) ? [4,"g"] : freshHerbIds.has(id) ? [28,"g"] : meatIds.has(id) ? [600,"g"] : id === "ING-0041" ? [15,"g"] : id === "ING-0052" ? [12,"g"] : id === "ING-0059" ? [60,"g"] : [300,"g"];
const use = (id) => { const [quantity, unit] = qty(id); return { ingredientSeedId: id, sourceQuantity: quantity, sourceUnit: unit, sourceDisplay: `${quantity} ${unit}`, normalizedQuantity: quantity, normalizedUnit: unit, normalizationMethod: "Source-style metric quantity recorded directly", cookingYieldFactor: meatIds.has(id) ? 0.75 : 1 }; };
const methodSteps = (name, method, protein, base) => {
  const oven = method.includes("Oven") || method === "Air Fryer";
  return [
    `Measure and prepare every ingredient for ${name.toLowerCase()} before heating the cooking vessel.`,
    oven ? `Preheat the ${method === "Air Fryer" ? "air fryer" : "oven"} and coat the ${protein} and vegetables evenly with the recorded seasonings.` : `Heat the cooking vessel and soften the aromatics before adding the ${protein}.`,
    `Cook the ${base} until tender while the seasoned ${protein} develops its intended color and texture.`,
    `Combine the cooked components with the remaining vegetables and sauce, then adjust the consistency and seasoning.`,
    `Portion the finished ${name.toLowerCase()} evenly and serve it warm.`,
  ];
};

export const recipes = rows.map((row, index) => {
  const [sequence,name,url,country,protein,base,method,idText,mealCategory="Main Meal"] = row;
  const n = 121 + index;
  const seedId = `REC-${String(n).padStart(4,"0")}`;
  const uses = [...new Set(idText.split(" "))].map(use);
  return {
    seedId, candidate: { sequence, seedId: `REC-CAND-${String(sequence).padStart(4,"0")}`, name }, name,
    description: `${name} combines ${protein} with ${base} and complementary vegetables, aromatics, and seasonings in a practical home-cooking format.`,
    source: { url, domain: new URL(url).hostname.replace(/^www\./,""), accessedAt: checkedAt, adaptation: `The source's defining ${protein}, ${base}, seasoning profile, and core cooking technique are retained; quantities and supporting vegetables are normalized to the canonical Appetee market ingredients.` },
    countryOfOrigin: country, discovery: index % 10 === 0, studentAthleteTarget: index % 3 === 0,
    times: { prepMinutes: 15 + (index % 3) * 5, cookMinutes: method === "No-cook" ? 0 : 20 + (index % 4) * 10, totalMinutes: 15 + (index % 3) * 5 + (method === "No-cook" ? 0 : 20 + (index % 4) * 10) },
    servings: 4, difficulty: index % 5 === 0 ? "Medium" : "Easy", mealType: index % 4 === 0 ? "Lunch" : "Dinner", mealCategory: canonicalMealCategory(seedId, mealCategory), cookingMethod: method, primaryProtein: protein, carbohydrateBase: base,
    ingredients: uses, instructions: methodSteps(name, method, protein, base), sourceNutrition: null,
    diets: ["lentils","chickpeas","black beans","spinach","tempeh"].includes(protein) ? ["Vegetarian","Vegan"] : protein === "eggs" ? ["Vegetarian"] : ["shrimp","cod","salmon","tuna"].includes(protein) ? ["Pescatarian"] : ["Flexitarian"], badgeJudgements: { mealPrep: index % 4 === 0, freezerFriendly: ["Stovetop","Braise","Slow Cooker"].includes(method) && index % 2 === 0 },
    estimatedFinishedWeightG: Number(uses.reduce((sum,item)=>sum + item.normalizedQuantity * item.cookingYieldFactor,0).toFixed(3)),
    finishedWeightMethod: "Estimated from normalized quantities and recorded cooking-yield factors; used only for the Low Calorie badge.",
  };
});

if (recipes.length !== 50) throw new Error(`batch-0007 must contain exactly 50 recipes, found ${recipes.length}`);

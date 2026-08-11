import { writeFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

const toolDir = path.dirname(fileURLToPath(import.meta.url));
const dataDir = path.resolve(toolDir, "..", "..");
const userAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/127.0 Safari/537.36";

const candidates = [
  [26, "Banana Oatmeal Bites", "food.com"], [27, "Greek Yogurt Berry Bark", "eatingwell.com"],
  [28, "Peanut Butter Banana Toast", "bbcgoodfood.com"], [29, "Cottage Cheese Tomato Toast", "food.com"],
  [30, "Chili Cumin Roasted Chickpeas", "hsph.harvard.edu"], [31, "Soy Sauce Edamame Snack", "eatsimplefood.com"],
  [32, "Peanut Butter Greek Yogurt Dip", "peacehealth.org"], [33, "Crispy Tofu Bites", "cookieandkate.com"],
  [34, "Black Bean Salsa Tortilla Cups", "eatingwell.com"], [35, "Banana Oat Pancakes", "eatingwell.com"],
  [36, "Berry Baked Oatmeal", "wellplated.com"], [37, "Savory Spinach Oatmeal with Eggs", "healthynibblesandbits.com"],
  [38, "Cottage Cheese Egg Bake", "eatingwell.com"], [39, "Turkey Sweet Potato Breakfast Hash", "eatingwell.com"],
  [40, "Tofu Scramble Breakfast Tacos", "minimalistbaker.com"], [41, "Black Bean Breakfast Quesadillas", "budgetbytes.com"],
  [42, "Quinoa Berry Breakfast Porridge", "cookieandkate.com"], [43, "Greek Yogurt French Toast", "wellplated.com"],
  [44, "Spinach Egg Breakfast Wraps", "eatingwell.com"], [45, "Ground Pork Rice Bowls", "thewoksoflife.com"],
  [46, "Tempeh Quinoa Salad", "eatingbirdfood.com"], [47, "Edamame Tuna Rice Salad", "eatingwell.com"],
  [48, "Chicken Spinach Yogurt Wraps", "eatingwell.com"], [49, "Black Bean Quinoa Wraps", "allrecipes.com"],
  [50, "Chickpea Salad Sandwiches", "loveandlemons.com"], [51, "Tuna Tomato Toast", "eatingwell.com"],
  [52, "Cottage Cheese Egg Salad Sandwiches", "eatingwell.com"], [53, "Salmon Quinoa Spinach Salad", "eatingwell.com"],
  [54, "Tofu Edamame Rice Salad", "eatingwell.com"], [55, "Lentil Sweet Potato Salad", "bbcgoodfood.com"],
  [56, "Turkey Taco Spinach Salad", "eatingwell.com"], [57, "Shrimp Quinoa Bowls", "eatingwell.com"],
  [58, "Japanese Salmon Rice Bowl", "justonecookbook.com"], [59, "Japanese Egg Rice Bowl", "justonecookbook.com"],
  [60, "Pork Fried Rice", "allrecipes.com"], [61, "Pork Sweet Potato Skillet", "eatingwell.com"],
  [62, "Pork Black Bean Tacos", "eatingwell.com"], [63, "Tempeh Broccoli Stir Fry", "minimalistbaker.com"],
  [64, "Tempeh Coconut Curry", "minimalistbaker.com"], [65, "Tempeh Taco Rice Bowls", "eatingbirdfood.com"],
  [66, "Caribbean Coconut Chickpea Curry", "immaeatthat.com"], [67, "Sri Lankan Red Lentil Coconut Curry", "theflavorbender.com"],
  [68, "Egyptian Koshari Lentil Rice", "themediterraneandish.com"], [69, "Indonesian Tahu Kecap", "cookmeindonesian.com"],
  [70, "Brazilian Black Bean Sweet Potato Stew", "bbcgoodfood.com"], [71, "Senegalese Peanut Lentil Stew", "budgetbytes.com"],
  [72, "French Lentil Potato Salad", "davidlebovitz.com"], [73, "Chicken Coconut Curry", "skinnytaste.com"],
  [74, "Beef Tomato Penne", "budgetbytes.com"], [75, "Cod Coconut Tomato Curry", "bbcgoodfood.com"],
];

function decode(value) {
  return value.replaceAll("&amp;", "&").replaceAll("&quot;", '"').replaceAll("&#39;", "'");
}

function resultLinks(html) {
  const links = [];
  for (const block of html.match(/<li[^>]*class="[^"]*\bb_algo\b[^"]*"[^>]*>[\s\S]*?<\/li>/giu) ?? []) {
    const match = block.match(/<h2[^>]*>\s*<a[^>]*href="([^"]+)"[^>]*>([\s\S]*?)<\/a>/iu);
    if (!match) continue;
    links.push({ url: decode(match[1]), title: decode(match[2].replace(/<[^>]+>/gu, "").trim()) });
  }
  return links;
}

async function discover([sequence, name, preferredDomain]) {
  const query = `${name} recipe site:${preferredDomain}`;
  const searchUrl = `https://www.bing.com/search?q=${encodeURIComponent(query)}&setlang=en-US`;
  const response = await fetch(searchUrl, { signal: AbortSignal.timeout(15000), headers: { "user-agent": userAgent, "accept-language": "en-US,en;q=0.8" } });
  const links = resultLinks(await response.text());
  const selected = links.find((item) => { try { return new URL(item.url).hostname.replace(/^www\./u, "").endsWith(preferredDomain); } catch { return false; } });
  if (!selected) return { sequence, name, preferredDomain, query, status: "missing", candidates: links.slice(0, 3) };
  let pageStatus = null;
  try { pageStatus = (await fetch(selected.url, { signal: AbortSignal.timeout(15000), redirect: "follow", headers: { "user-agent": userAgent } })).status; } catch {}
  return { sequence, name, preferredDomain, query, status: pageStatus && pageStatus < 400 ? "verified" : "selected", pageStatus, ...selected };
}

async function main() {
  const results = [];
  for (const candidate of candidates) {
    const result = await discover(candidate);
    results.push(result);
    process.stdout.write(`REC-${String(result.sequence).padStart(4, "0")} ${result.status}: ${result.url ?? result.name}\n`);
    await new Promise((resolve) => setTimeout(resolve, 200));
  }
  await writeFile(path.join(dataDir, "research", "cache", "recipe-sources-batch-0002.json"), `${JSON.stringify(results, null, 2)}\n`);
}

main().catch((error) => { process.stderr.write(`${error.stack ?? error}\n`); process.exitCode = 1; });

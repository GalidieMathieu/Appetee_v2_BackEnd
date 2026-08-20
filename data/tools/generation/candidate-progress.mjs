import { createHash } from "node:crypto";
import { readFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

const toolDir = path.dirname(fileURLToPath(import.meta.url));
const dataDir = path.resolve(toolDir, "..", "..");
const candidatePath = path.join(dataDir, "candidates", "recipe-names.json");

export async function loadCandidates() {
  const candidates = JSON.parse(await readFile(candidatePath, "utf8"));
  if (!Array.isArray(candidates) || candidates.length === 0) throw new Error("candidates/recipe-names.json must be a non-empty array");
  for (let index = 0; index < candidates.length; index += 1) {
    const candidate = candidates[index];
    if (candidate.sequence !== index + 1 || !candidate.seedId || !candidate.name) throw new Error(`Invalid candidate at array index ${index}`);
  }
  return candidates;
}

const sequenceOf = (entry) => Number(entry?.sequence);
const validEvents = (entries = []) => entries
  .filter((entry) => Number.isInteger(sequenceOf(entry)) && sequenceOf(entry) > 0)
  .sort((a, b) => sequenceOf(a) - sequenceOf(b));

export async function deriveCandidateAcquisition(recipes, previous = {}) {
  const candidates = await loadCandidates();
  const sourceSha256 = createHash("sha256").update(await readFile(candidatePath)).digest("hex");
  if (previous.sourceSha256 && previous.sourceSha256 !== sourceSha256) throw new Error("candidates/recipe-names.json changed after ordered acquisition began");
  const candidateBySequence = new Map(candidates.map((candidate) => [candidate.sequence, candidate]));
  const completed = recipes
    .filter((recipe) => Number.isInteger(recipe.candidate?.sequence ?? recipe.candidateSequence))
    .map((recipe) => ({ sequence: recipe.candidate?.sequence ?? recipe.candidateSequence, recipeSeedId: recipe.seedId, name: recipe.name }))
    .sort((a, b) => a.sequence - b.sequence);
  const skippedCandidates = validEvents(previous.skippedCandidates);
  const unresolvedCandidates = validEvents(previous.unresolvedCandidates);
  const statuses = new Map();
  for (const entry of [...completed, ...skippedCandidates, ...unresolvedCandidates]) {
    const candidate = candidateBySequence.get(entry.sequence);
    if (!candidate) throw new Error(`Candidate progress references unknown sequence ${entry.sequence}`);
    if (statuses.has(entry.sequence)) throw new Error(`Candidate sequence ${entry.sequence} has multiple progress states`);
    statuses.set(entry.sequence, entry);
  }
  let lastCandidateSequenceProcessed = 0;
  while (statuses.has(lastCandidateSequenceProcessed + 1)) lastCandidateSequenceProcessed += 1;
  const beyondGap = [...statuses.keys()].find((sequence) => sequence > lastCandidateSequenceProcessed + 1);
  if (beyondGap) throw new Error(`Candidate progress has an unprocessed gap before sequence ${beyondGap}`);
  return {
    sourceFile: "data/candidates/recipe-names.json",
    sourceSha256,
    candidateCount: candidates.length,
    startedAtRecipeSequence: previous.startedAtRecipeSequence ?? 117,
    lastCandidateSequenceProcessed,
    lastCandidateSequenceCompleted: completed.at(-1)?.sequence ?? 0,
    nextCandidateSequence: lastCandidateSequenceProcessed + 1,
    completedCandidateCount: completed.length,
    completedCandidateSequences: completed,
    skippedCandidates,
    unresolvedCandidates,
    selectionPolicy: "Process unused candidates strictly in ascending sequence; do not modify or reorder the candidate file during normal generation.",
  };
}

const isCli = process.argv[1] && import.meta.url === pathToFileURL(path.resolve(process.argv[1])).href;
if (isCli) {
  const progress = JSON.parse(await readFile(path.join(dataDir, "workflow", "progress.json"), "utf8"));
  const recipeIndex = JSON.parse(await readFile(path.join(dataDir, "recipes", "index.json"), "utf8"));
  const state = await deriveCandidateAcquisition(recipeIndex, progress.candidateAcquisition);
  process.stdout.write(`${JSON.stringify(state, null, 2)}\n`);
}
